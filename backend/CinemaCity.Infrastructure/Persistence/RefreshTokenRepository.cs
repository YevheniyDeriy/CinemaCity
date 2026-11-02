using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
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
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        dbContext.RefreshTokens.Add(refreshToken);
    }
}
