using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.Infrastructure.Persistence;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByGoogleIdOrEmailAsync(string googleId, string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(
            user => user.GoogleId == googleId || user.Email == email,
            cancellationToken);
    }

    public void Add(User user)
    {
        dbContext.Users.Add(user);
    }
}
