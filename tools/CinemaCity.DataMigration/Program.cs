using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using CinemaCity.AuthService.Data;
using CinemaCity.CatalogService.Data;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Offline, one-time import only. Runtime services never receive a source-database connection string.
var sourceString = Required("CINEMACITY_SOURCE_CONNECTION");
var authString = Required("CINEMACITY_AUTH_CONNECTION");
var catalogString = Required("CINEMACITY_CATALOG_CONNECTION");
var monolithString = Environment.GetEnvironmentVariable("CINEMACITY_COMMUNITY_CONNECTION") ?? Environment.GetEnvironmentVariable("CINEMACITY_MONOLITH_CONNECTION");
var builders = new[] { sourceString, authString, catalogString }.Select(s => new NpgsqlConnectionStringBuilder(s)).ToArray();
if (builders.Select(b => $"{b.Host}:{b.Port}/{b.Database}".ToLowerInvariant()).Distinct().Count() != 3)
    throw new InvalidOperationException("Source, Auth and Catalog must be three different databases.");
await EnsureDatabaseAsync(authString);
await EnsureDatabaseAsync(catalogString);
await using (var auth = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(authString).Options)) await auth.Database.MigrateAsync();
await using (var catalog = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(catalogString).Options)) await catalog.Database.MigrateAsync();
await using var source = new NpgsqlConnection(sourceString);
await source.OpenAsync();
await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead);
await ExecuteAsync(source, "SET TRANSACTION READ ONLY", sourceTransaction);
var schema = (string?)await ScalarAsync(source, "SELECT CASE WHEN to_regclass('legacy.movies') IS NOT NULL THEN 'legacy' WHEN to_regclass('public.movies') IS NOT NULL THEN 'public' WHEN to_regclass('projections.movies') IS NOT NULL THEN 'projections' END", sourceTransaction)
    ?? throw new InvalidOperationException("The source is not the CinemaCity monolith: its movies table is missing.");
if (await ScalarAsync(source, $"SELECT to_regclass('{schema}.users')::text", sourceTransaction) is not string)
    throw new InvalidOperationException("The source is not the CinemaCity monolith: its users table is missing.");
var identity = $"{builders[0].Host}:{builders[0].Port}/{builders[0].Database}/{schema}";
await ImportAsync(authString, ["users", "refresh_tokens"], "auth");
await ImportAsync(catalogString, ["genres", "countries", "languages", "persons", "movies", "seasons", "episodes", "movie_genres", "movie_countries", "movie_languages", "movie_cast"], "catalog");
if (!string.IsNullOrWhiteSpace(monolithString))
{
    var monolith = new NpgsqlConnectionStringBuilder(monolithString);
    var monolithIdentity = $"{monolith.Host}:{monolith.Port}/{monolith.Database}".ToLowerInvariant();
    if (builders.Skip(1).Any(b => $"{b.Host}:{b.Port}/{b.Database}".ToLowerInvariant() == monolithIdentity)) throw new InvalidOperationException("The community database cannot be an owner service database.");
    if (monolithIdentity != $"{builders[0].Host}:{builders[0].Port}/{builders[0].Database}".ToLowerInvariant())
    {
        await EnsureDatabaseAsync(monolithString);
        await using (var community = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(monolithString).Options)) await community.Database.MigrateAsync();
        await ImportAsync(monolithString, ["users", "refresh_tokens", "genres", "countries", "languages", "persons", "movies", "seasons", "episodes", "movie_genres", "movie_countries", "movie_languages", "movie_cast", "ratings", "reviews", "comments", "review_likes", "watchlist_items", "user_movie_lists", "user_movie_list_items"], "community");
    }
}
await sourceTransaction.CommitAsync();
Console.WriteLine("CinemaCity import complete. Original IDs, account password hashes and catalog relations are preserved. The source was read-only.");

async Task ImportAsync(string connectionString, string[] tables, string service)
{
    var ownerTables = new HashSet<string> { "users", "refresh_tokens", "genres", "countries", "languages", "persons", "movies", "seasons", "episodes", "movie_genres", "movie_countries", "movie_languages", "movie_cast" };
    string TargetSchema(string table) => service == "community" && ownerTables.Contains(table) ? "projections" : "public";
    string SourceSchema(string table) => ownerTables.Contains(table) ? schema : "public";
    await using var target = new NpgsqlConnection(connectionString);
    await target.OpenAsync();
    await using var transaction = await target.BeginTransactionAsync();
    await ExecuteAsync(target, "CREATE TABLE IF NOT EXISTS cinemacity_imports (source text PRIMARY KEY, completed_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP)", transaction);
    var recorded = await ScalarAsync(target, "SELECT source FROM cinemacity_imports LIMIT 1", transaction);
    if (recorded is string previous)
    {
        // public -> legacy is the expected archive transition of the same source database.
        if (previous[..previous.LastIndexOf('/')] != identity[..identity.LastIndexOf('/')]) throw new InvalidOperationException($"{service} was imported from another database; refusing to mix datasets.");
        Console.WriteLine($"{service}: already imported; preserving all subsequent service changes.");
        return;
    }
    foreach (var table in tables)
    {
        var count = (long)(await ScalarAsync(target, $"SELECT count(*) FROM {Quote(TargetSchema(table))}.\"{table}\"", transaction))!;
        if (count != 0) throw new InvalidOperationException($"{service}.{table} is not empty and has no import marker. Refusing to overwrite existing data.");
    }
    foreach (var table in tables)
    {
        var sourceSchema = SourceSchema(table);
        var targetSchema = TargetSchema(table);
        var sourceColumns = await ColumnsAsync(source, sourceSchema, table, sourceTransaction);
        var targetColumns = await ColumnsAsync(target, targetSchema, table, transaction);
        var columns = sourceColumns.Intersect(targetColumns).ToList();
        var selection = string.Join(",", columns.Select(column => column is "poster_url" or "backdrop_url" or "photo_url" or "avatar_url"
            ? $"CASE WHEN {Quote(column)} ~ '^https?://(localhost|127\\.0\\.0\\.1)(:[0-9]+)?/uploads/' THEN regexp_replace({Quote(column)}, '^https?://[^/]+', '') ELSE {Quote(column)} END AS {Quote(column)}"
            : Quote(column)));
        if (table == "users" && service == "auth") { columns.Add("display_name"); selection += ", username AS display_name"; }
        var insertColumns = string.Join(",", columns.Select(Quote));
        var offset = 0;
        while (true)
        {
            await using var query = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(rows)), '[]'::jsonb)::text FROM (SELECT {selection} FROM {Quote(sourceSchema)}.{Quote(table)} ORDER BY 1 LIMIT 500 OFFSET @offset) rows", source, sourceTransaction);
            query.Parameters.AddWithValue("offset", offset);
            var rows = (string)(await query.ExecuteScalarAsync())!;
            using var parsed = JsonDocument.Parse(rows);
            var count = parsed.RootElement.GetArrayLength();
            if (count == 0) break;
            await using var insert = new NpgsqlCommand($"INSERT INTO {Quote(targetSchema)}.{Quote(table)} ({insertColumns}) SELECT {insertColumns} FROM jsonb_populate_recordset(NULL::{Quote(targetSchema)}.{Quote(table)}, CAST(@rows AS jsonb))", target, transaction);
            insert.Parameters.AddWithValue("rows", rows);
            await insert.ExecuteNonQueryAsync();
            offset += count;
        }
        Console.WriteLine($"{service}: copied {offset} {table} rows.");
        if (columns.Contains("id") && await ScalarAsync(target, $"SELECT pg_get_serial_sequence('{targetSchema}.{table}','id')", transaction) is string)
            await ExecuteAsync(target, $"SELECT setval(pg_get_serial_sequence('{targetSchema}.{table}','id'), COALESCE((SELECT max(id) FROM {Quote(targetSchema)}.{Quote(table)}),0)+1,false)", transaction);
        if (service == "community" && ownerTables.Contains(table))
            await ExecuteAsync(target, $"INSERT INTO legacy.{Quote(table)} SELECT * FROM projections.{Quote(table)}", transaction);
    }
    if (service == "auth")
        await ExecuteAsync(target, "INSERT INTO user_roles (user_id,role_id) SELECT id,CASE WHEN role='Admin' THEN 2 ELSE 1 END FROM users", transaction);
    await using var marker = new NpgsqlCommand("INSERT INTO cinemacity_imports(source) VALUES (@source)", target, transaction);
    marker.Parameters.AddWithValue("source", identity);
    await marker.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"Set {name} using environment variables. Credentials are never accepted on the command line or logged.");
static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
static async Task EnsureDatabaseAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var database = builder.Database ?? throw new InvalidOperationException("Database is required.");
    if (!Regex.IsMatch(database, "^[a-zA-Z_][a-zA-Z0-9_]{0,62}$")) throw new InvalidOperationException("Unsupported database name.");
    builder.Database = "postgres";
    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname=@name", connection);
    exists.Parameters.AddWithValue("name", database);
    if (await exists.ExecuteScalarAsync() is null) await ExecuteAsync(connection, $"CREATE DATABASE {Quote(database)}");
}
static async Task<List<string>> ColumnsAsync(NpgsqlConnection connection, string schema, string table, NpgsqlTransaction transaction)
{
    await using var command = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table ORDER BY ordinal_position", connection, transaction);
    command.Parameters.AddWithValue("schema", schema); command.Parameters.AddWithValue("table", table);
    var columns = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
    return columns;
}
static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
{ await using var command = new NpgsqlCommand(sql, connection, transaction); return await command.ExecuteScalarAsync(); }
static async Task ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
{ await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(); }
