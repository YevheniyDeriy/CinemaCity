using CinemaCity.AuthService.Models;

namespace CinemaCity.AuthService.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public string DisplayName { get; set; } = string.Empty;
    public ICollection<UserRoleAssignment> Roles { get; set; } = new List<UserRoleAssignment>();
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEndAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
