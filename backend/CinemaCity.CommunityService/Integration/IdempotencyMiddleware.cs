using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CinemaCity.API.Integration;

/// <summary>Persists the operation and its replay response in the same PostgreSQL transaction.</summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS public.operation_replays (
            scope text PRIMARY KEY, request_hash text NOT NULL, status integer NOT NULL,
            content_type text, location text, body bytea NOT NULL,
            created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
        """;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (!HttpMethods.IsPost(context.Request.Method) || string.IsNullOrEmpty(key) ||
            !context.Request.Path.StartsWithSegments("/api") || context.User.Identity?.IsAuthenticated != true)
        { await next(context); return; }
        if (key.Length > 128 || key.Any(char.IsControl))
        { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Invalid Idempotency-Key." }); return; }
        var ct = context.RequestAborted;
        context.Request.EnableBuffering();
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(context.Request.Body, ct));
        context.Request.Body.Position = 0;
        var scope = context.User.FindFirstValue(ClaimTypes.NameIdentifier) + ":" + context.Request.Path + context.Request.QueryString + ":" + key;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            gate.Parameters.AddWithValue("key", BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(scope))));
            await gate.ExecuteNonQueryAsync(ct);
        }
        await using (var query = new NpgsqlCommand("SELECT request_hash,status,content_type,location,body FROM public.operation_replays WHERE scope=@scope", connection, (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            query.Parameters.AddWithValue("scope", scope);
            await using var reader = await query.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(0) != hash)
                { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = "Idempotency-Key was used with another request." }, ct); return; }
                context.Response.StatusCode = reader.GetInt32(1);
                if (!reader.IsDBNull(2)) context.Response.ContentType = reader.GetString(2);
                if (!reader.IsDBNull(3)) context.Response.Headers.Location = reader.GetString(3);
                await context.Response.Body.WriteAsync(reader.GetFieldValue<byte[]>(4), ct);
                return;
            }
        }
        var original = context.Response.Body;
        await using var buffered = new MemoryStream();
        context.Response.Body = buffered;
        try
        {
            await next(context);
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                await using var insert = new NpgsqlCommand("INSERT INTO public.operation_replays(scope,request_hash,status,content_type,location,body) VALUES(@scope,@hash,@status,@type,@location,@body)", connection, (NpgsqlTransaction)transaction.GetDbTransaction());
                insert.Parameters.AddWithValue("scope", scope); insert.Parameters.AddWithValue("hash", hash);
                insert.Parameters.AddWithValue("status", context.Response.StatusCode);
                insert.Parameters.AddWithValue("type", (object?)context.Response.ContentType ?? DBNull.Value);
                insert.Parameters.AddWithValue("location", context.Response.Headers.Location.Count > 0 ? context.Response.Headers.Location.ToString() : DBNull.Value);
                insert.Parameters.AddWithValue("body", buffered.ToArray());
                await insert.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            else await transaction.RollbackAsync(ct);
            buffered.Position = 0;
            await buffered.CopyToAsync(original, ct);
        }
        finally { context.Response.Body = original; }
    }
}
