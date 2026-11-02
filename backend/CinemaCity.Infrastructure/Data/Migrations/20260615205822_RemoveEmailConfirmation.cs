using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaCity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_email_confirmed",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_email_confirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
