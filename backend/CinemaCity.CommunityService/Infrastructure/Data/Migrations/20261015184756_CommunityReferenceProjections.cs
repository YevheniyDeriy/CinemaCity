using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CinemaCity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityReferenceProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Freeze the pre-extraction owned data before moving active local read models.
            // Runtime services cannot access this database; the offline importer may read the archive.
            migrationBuilder.EnsureSchema(name: "legacy");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.users AS TABLE public.users");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.refresh_tokens AS TABLE public.refresh_tokens");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.genres AS TABLE public.genres");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.countries AS TABLE public.countries");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.languages AS TABLE public.languages");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.persons AS TABLE public.persons");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.movies AS TABLE public.movies");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.seasons AS TABLE public.seasons");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.episodes AS TABLE public.episodes");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.movie_genres AS TABLE public.movie_genres");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.movie_countries AS TABLE public.movie_countries");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.movie_languages AS TABLE public.movie_languages");
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS legacy.movie_cast AS TABLE public.movie_cast");

            migrationBuilder.EnsureSchema(
                name: "projections");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "seasons",
                newName: "seasons",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "refresh_tokens",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "persons",
                newName: "persons",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "movies",
                newName: "movies",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "movie_languages",
                newName: "movie_languages",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "movie_genres",
                newName: "movie_genres",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "movie_countries",
                newName: "movie_countries",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "movie_cast",
                newName: "movie_cast",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "languages",
                newName: "languages",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "genres",
                newName: "genres",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "episodes",
                newName: "episodes",
                newSchema: "projections");

            migrationBuilder.RenameTable(
                name: "countries",
                newName: "countries",
                newSchema: "projections");

            migrationBuilder.CreateTable(
                name: "catalog_statistics_outbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AverageRating = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    RatingsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_statistics_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_statistics_outbox_SentAt_NextAttemptAt",
                table: "catalog_statistics_outbox",
                columns: new[] { "SentAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_statistics_outbox");

            migrationBuilder.RenameTable(
                name: "users",
                schema: "projections",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "seasons",
                schema: "projections",
                newName: "seasons");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "projections",
                newName: "refresh_tokens");

            migrationBuilder.RenameTable(
                name: "persons",
                schema: "projections",
                newName: "persons");

            migrationBuilder.RenameTable(
                name: "movies",
                schema: "projections",
                newName: "movies");

            migrationBuilder.RenameTable(
                name: "movie_languages",
                schema: "projections",
                newName: "movie_languages");

            migrationBuilder.RenameTable(
                name: "movie_genres",
                schema: "projections",
                newName: "movie_genres");

            migrationBuilder.RenameTable(
                name: "movie_countries",
                schema: "projections",
                newName: "movie_countries");

            migrationBuilder.RenameTable(
                name: "movie_cast",
                schema: "projections",
                newName: "movie_cast");

            migrationBuilder.RenameTable(
                name: "languages",
                schema: "projections",
                newName: "languages");

            migrationBuilder.RenameTable(
                name: "genres",
                schema: "projections",
                newName: "genres");

            migrationBuilder.RenameTable(
                name: "episodes",
                schema: "projections",
                newName: "episodes");

            migrationBuilder.RenameTable(
                name: "countries",
                schema: "projections",
                newName: "countries");
        }
    }
}
