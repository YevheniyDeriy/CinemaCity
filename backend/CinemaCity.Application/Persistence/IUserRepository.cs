using CinemaCity.Domain.Entities;

namespace CinemaCity.Application.Persistence;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByGoogleIdOrEmailAsync(string googleId, string email, CancellationToken cancellationToken = default);

    void Add(User user);
}
