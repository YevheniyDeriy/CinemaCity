using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaCity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tmdb_id",
                table: "persons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "episodes_count",
                table: "movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "first_air_date",
                table: "movies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_air_date",
                table: "movies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seasons_count",
                table: "movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "movies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tagline",
                table: "movies",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tmdb_id",
                table: "movies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tmdb_id",
                table: "genres",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_persons_tmdb_id",
                table: "persons",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id > 0");

            migrationBuilder.CreateIndex(
                name: "IX_movies_content_type_tmdb_id",
                table: "movies",
                columns: new[] { "content_type", "tmdb_id" },
                unique: true,
                filter: "tmdb_id > 0");

            migrationBuilder.CreateIndex(
                name: "IX_genres_tmdb_id",
                table: "genres",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_persons_tmdb_id",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "IX_movies_content_type_tmdb_id",
                table: "movies");

            migrationBuilder.DropIndex(
                name: "IX_genres_tmdb_id",
                table: "genres");

            migrationBuilder.DropColumn(
                name: "tmdb_id",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "episodes_count",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "first_air_date",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "last_air_date",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "seasons_count",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "status",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "tagline",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "tmdb_id",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "tmdb_id",
                table: "genres");
        }
    }
}
