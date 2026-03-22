using System.Security.Claims;

namespace HelpDeskApi.Features.Auth;

public static class AuthHelpers
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return int.TryParse(value, out var id) ? id : null;
    }

    public static string? GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role);

    public static bool IsAgentOrAdmin(this ClaimsPrincipal user)
    {
        var role = user.GetRole();
        return role == "Agent" || role == "Admin";
    }
}