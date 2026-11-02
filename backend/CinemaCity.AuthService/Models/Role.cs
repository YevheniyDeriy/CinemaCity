namespace CinemaCity.AuthService.Models;

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRoleAssignment> Users { get; set; } = new List<UserRoleAssignment>();
}

public sealed class UserRoleAssignment
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
