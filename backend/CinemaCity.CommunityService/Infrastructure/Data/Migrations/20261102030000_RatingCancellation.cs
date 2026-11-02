using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CinemaCity.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102030000_RatingCancellation")]
public sealed class RatingCancellation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE public.ratings ADD COLUMN cancelled_at timestamptz;
        DROP INDEX public."IX_ratings_user_id_movie_id";
        DROP INDEX public."IX_ratings_user_id_episode_id";
        CREATE UNIQUE INDEX "IX_ratings_user_id_movie_id" ON public.ratings(user_id, movie_id)
            WHERE movie_id IS NOT NULL AND cancelled_at IS NULL;
        CREATE UNIQUE INDEX "IX_ratings_user_id_episode_id" ON public.ratings(user_id, episode_id)
            WHERE episode_id IS NOT NULL AND cancelled_at IS NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Cancellation history cannot be removed safely when a film has been rated again. Restore a database backup for rollback.");
}
