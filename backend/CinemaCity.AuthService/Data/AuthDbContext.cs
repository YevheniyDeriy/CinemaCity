using CinemaCity.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.AuthService.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles"); entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Name).HasColumnName("name").HasMaxLength(32).IsRequired();
            entity.HasIndex(r => r.Name).IsUnique();
            entity.HasData(new Role { Id = 1, Name = "User" }, new Role { Id = 2, Name = "Admin" });
        });
        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("user_roles"); entity.HasKey(r => new { r.UserId, r.RoleId });
            entity.Property(r => r.UserId).HasColumnName("user_id");
            entity.Property(r => r.RoleId).HasColumnName("role_id");
            entity.HasOne(r => r.User).WithMany(u => u.Roles).HasForeignKey(r => r.UserId);
            entity.HasOne(r => r.Role).WithMany(r => r.Users).HasForeignKey(r => r.RoleId);
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.Username).HasColumnName("username").HasMaxLength(30).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash");
            entity.Property(user => user.GoogleId).HasColumnName("google_id").HasMaxLength(255);
            entity.Property(user => user.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(user => user.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            entity.Property(user => user.LockoutEndAt).HasColumnName("lockout_end_at");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(user => user.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.GoogleId).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.Id).HasColumnName("id");
            entity.Property(token => token.UserId).HasColumnName("user_id");
            entity.Property(token => token.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(token => token.ExpiresAt).HasColumnName("expires_at");
            entity.Property(token => token.ReplacedByToken).HasColumnName("replaced_by_token").HasMaxLength(128);
            entity.Property(token => token.RevokedAt).HasColumnName("revoked_at");
            entity.Property(token => token.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
















}
