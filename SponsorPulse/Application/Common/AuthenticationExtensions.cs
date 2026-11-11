using Microsoft.AspNetCore.Components.Authorization;

namespace SponsorPulse.Application.Common.Extensions;

public static class AuthenticationExtensions
{
    public static bool TryGetUserId(this AuthenticationState authState, out Guid? userId)
    {
        userId = null;
        var user = authState.User;

        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return false;
        }

        var idClaim =
            user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(idClaim, out var resultUserId))
        {
            return false;
        }

        userId = resultUserId;

        return true;
    }
}
