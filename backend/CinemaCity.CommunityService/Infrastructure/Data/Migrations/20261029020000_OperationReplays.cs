using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CinemaCity.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261029020000_OperationReplays")]
public sealed class OperationReplays : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS public.operation_replays (
            scope text PRIMARY KEY, request_hash text NOT NULL, status integer NOT NULL,
            content_type text, location text, body bytea NOT NULL,
            created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE IF EXISTS public.operation_replays");
}
