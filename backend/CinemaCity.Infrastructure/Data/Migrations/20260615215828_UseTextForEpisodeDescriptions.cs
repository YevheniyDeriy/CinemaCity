using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaCity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseTextForEpisodeDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "episodes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(800)",
                oldMaxLength: 800,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "episodes",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
