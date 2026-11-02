using CinemaCity.Domain.Entities;

namespace CinemaCity.Application.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}
