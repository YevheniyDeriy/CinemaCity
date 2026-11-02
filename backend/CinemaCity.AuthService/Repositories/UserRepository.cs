using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Models;
using CinemaCity.AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.AuthService.Repositories;

public sealed class UserRepository(AuthDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(user => user.Roles).ThenInclude(role => role.Role).SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<(IReadOnlyCollection<User> Users, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var users = await query.Include(user => user.Roles).ThenInclude(role => role.Role).OrderBy(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (users, total);
    }

    public void ReplaceRoles(User user, IReadOnlyCollection<string> roles)
    {
        dbContext.UserRoles.RemoveRange(user.Roles);
        user.Roles = roles.Select(name => new UserRoleAssignment { UserId = user.Id, RoleId = name == "Admin" ? 2 : 1 }).ToList();
    }

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
        return dbContext.Users.Include(user => user.Roles).ThenInclude(role => role.Role).FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByGoogleIdOrEmailAsync(string googleId, string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.Include(user => user.Roles).ThenInclude(role => role.Role).FirstOrDefaultAsync(
            user => user.GoogleId == googleId || user.Email == email,
            cancellationToken);
    }

    public void Add(User user)
    {
        if (user.Roles.Count == 0) user.Roles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = user.Role == UserRole.Admin ? 2 : 1 });
        if (string.IsNullOrWhiteSpace(user.DisplayName)) user.DisplayName = user.Username;
        dbContext.Users.Add(user);
    }
}
