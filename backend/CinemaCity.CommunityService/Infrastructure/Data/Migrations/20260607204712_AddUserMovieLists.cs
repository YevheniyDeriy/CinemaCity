using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaCity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMovieLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_movie_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_movie_lists", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_movie_lists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_movie_list_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_movie_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_movie_list_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_movie_list_items_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_movie_list_items_user_movie_lists_user_movie_list_id",
                        column: x => x.user_movie_list_id,
                        principalTable: "user_movie_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_movie_list_items_movie_id",
                table: "user_movie_list_items",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_movie_list_items_user_movie_list_id_movie_id",
                table: "user_movie_list_items",
                columns: new[] { "user_movie_list_id", "movie_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_movie_lists_user_id_name",
                table: "user_movie_lists",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_movie_list_items");

            migrationBuilder.DropTable(
                name: "user_movie_lists");
        }
    }
}
