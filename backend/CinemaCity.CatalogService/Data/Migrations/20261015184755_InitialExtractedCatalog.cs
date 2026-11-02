using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CinemaCity.CatalogService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialExtractedCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_statistics_versions",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_statistics_versions", x => new { x.Kind, x.TargetId });
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    release_year = table.Column<int>(type: "integer", nullable: false),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tagline = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    first_air_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_air_date = table.Column<DateOnly>(type: "date", nullable: true),
                    seasons_count = table.Column<int>(type: "integer", nullable: true),
                    episodes_count = table.Column<int>(type: "integer", nullable: true),
                    poster_url = table.Column<string>(type: "text", nullable: true),
                    backdrop_url = table.Column<string>(type: "text", nullable: true),
                    trailer_youtube_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    ratings_count = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    birth_place = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movie_countries",
                columns: table => new
                {
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_countries", x => new { x.movie_id, x.country_id });
                    table.ForeignKey(
                        name: "FK_movie_countries_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movie_countries_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movie_genres",
                columns: table => new
                {
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    genre_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_genres", x => new { x.movie_id, x.genre_id });
                    table.ForeignKey(
                        name: "FK_movie_genres_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movie_genres_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movie_languages",
                columns: table => new
                {
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_languages", x => new { x.movie_id, x.language_id });
                    table.ForeignKey(
                        name: "FK_movie_languages_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movie_languages_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    release_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seasons", x => x.id);
                    table.ForeignKey(
                        name: "FK_seasons_movies_series_id",
                        column: x => x.series_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movie_cast",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    role_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_cast", x => x.id);
                    table.ForeignKey(
                        name: "FK_movie_cast_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movie_cast_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    air_date = table.Column<DateOnly>(type: "date", nullable: true),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    ratings_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_episodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_episodes_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_countries_code",
                table: "countries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_countries_name",
                table: "countries",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_episodes_season_id_episode_number",
                table: "episodes",
                columns: new[] { "season_id", "episode_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genres_name",
                table: "genres",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genres_slug",
                table: "genres",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genres_tmdb_id",
                table: "genres",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id > 0");

            migrationBuilder.CreateIndex(
                name: "IX_languages_code",
                table: "languages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_languages_name",
                table: "languages",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movie_cast_movie_id",
                table: "movie_cast",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "IX_movie_cast_person_id",
                table: "movie_cast",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_movie_countries_country_id",
                table: "movie_countries",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_movie_genres_genre_id",
                table: "movie_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "IX_movie_languages_language_id",
                table: "movie_languages",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "IX_movies_average_rating_ratings_count",
                table: "movies",
                columns: new[] { "average_rating", "ratings_count" });

            migrationBuilder.CreateIndex(
                name: "IX_movies_content_type",
                table: "movies",
                column: "content_type");

            migrationBuilder.CreateIndex(
                name: "IX_movies_content_type_tmdb_id",
                table: "movies",
                columns: new[] { "content_type", "tmdb_id" },
                unique: true,
                filter: "tmdb_id > 0");

            migrationBuilder.CreateIndex(
                name: "IX_movies_is_deleted",
                table: "movies",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_movies_release_year",
                table: "movies",
                column: "release_year");

            migrationBuilder.CreateIndex(
                name: "IX_movies_title",
                table: "movies",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_persons_full_name",
                table: "persons",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "IX_persons_tmdb_id",
                table: "persons",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id > 0");

            migrationBuilder.CreateIndex(
                name: "IX_seasons_series_id_season_number",
                table: "seasons",
                columns: new[] { "series_id", "season_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_statistics_versions");

            migrationBuilder.DropTable(
                name: "episodes");

            migrationBuilder.DropTable(
                name: "movie_cast");

            migrationBuilder.DropTable(
                name: "movie_countries");

            migrationBuilder.DropTable(
                name: "movie_genres");

            migrationBuilder.DropTable(
                name: "movie_languages");

            migrationBuilder.DropTable(
                name: "seasons");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "movies");
        }
    }
}
