using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Models;
using CinemaCity.AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.AuthService.Repositories;

public sealed class RefreshTokenRepository(AuthDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.RefreshTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);
    }

    public Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .Include(token => token.User).ThenInclude(user => user.Roles).ThenInclude(role => role.Role)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        dbContext.RefreshTokens.Add(refreshToken);
    }
}
