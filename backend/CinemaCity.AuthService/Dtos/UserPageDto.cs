using System.ComponentModel;
using CinemaCity.AuthService.Services;

namespace CinemaCity.AuthService.Dtos;

/// <summary>A bounded page of CinemaCity accounts without credentials.</summary>
public sealed record UserPageDto(
    [property: Description("Accounts on the requested page.")] IReadOnlyCollection<UserDetailsDto> Items,
    [property: Description("One-based page number.")] int Page,
    [property: Description("Requested page size, at most 100.")] int PageSize,
    [property: Description("Total accounts before pagination.")] int TotalCount);
