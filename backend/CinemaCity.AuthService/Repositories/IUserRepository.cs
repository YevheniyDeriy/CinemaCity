using CinemaCity.AuthService.Models;

namespace CinemaCity.AuthService.Repositories;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByGoogleIdOrEmailAsync(string googleId, string email, CancellationToken cancellationToken = default);

    void Add(User user);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<User> Users, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    void ReplaceRoles(User user, IReadOnlyCollection<string> roles);
}
