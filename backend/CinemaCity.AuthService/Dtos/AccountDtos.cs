using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CinemaCity.AuthService.Services;

namespace CinemaCity.AuthService.Dtos;

/// <summary>Registers an account in the original CinemaCity user model.</summary>
public sealed class RegisterDto
{
    [Required, StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters."), Description("Original CinemaCity username, 3–30 characters.")]
    public string Username { get; set; } = string.Empty;
    [Required, EmailAddress(ErrorMessage = "Email format is invalid."), MaxLength(255), Description("Unique account email address.")]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(256, MinimumLength = 8, ErrorMessage = "Password must contain between 8 and 256 characters."), Description("Password of at least 8 characters; stored only as a salted hash.")]
    public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password), ErrorMessage = "Password confirmation does not match."), Description("Must match Password.")]
    public string ConfirmPassword { get; set; } = string.Empty;
    public RegisterRequest ToRequest() => new(Username, Email, Password, ConfirmPassword);
}

/// <summary>Signs in to an existing CinemaCity account.</summary>
public sealed class LoginDto
{
    [Required, EmailAddress, MaxLength(255), Description("Registered email.")]
    public string Email { get; set; } = string.Empty;
    [Required, MaxLength(256), Description("Existing account password.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Refresh credential; the browser may instead supply its HttpOnly cookie.</summary>
public sealed class RefreshDto
{
    [MaxLength(512), Description("Refresh token, or null to use the session cookie.")]
    public string? RefreshToken { get; set; }
}

/// <summary>The replacement set of User and/or Admin roles.</summary>
public sealed class UpdateRolesDto
{
    [Required, MinLength(1), Description("Role names: User and/or Admin.")]
    public string[] Roles { get; set; } = [];
}
