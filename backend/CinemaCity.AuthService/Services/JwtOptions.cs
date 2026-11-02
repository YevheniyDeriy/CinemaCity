namespace CinemaCity.AuthService.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CinemaCity";
    public string Audience { get; set; } = "CinemaCity.Client";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
