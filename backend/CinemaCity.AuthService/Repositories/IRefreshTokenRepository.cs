using CinemaCity.AuthService.Models;

namespace CinemaCity.AuthService.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}
