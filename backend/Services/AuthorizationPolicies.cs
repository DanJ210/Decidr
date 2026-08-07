namespace backend.Services;

public static class AuthorizationPolicies
{
    public const string AccessAsUser = "AccessAsUser";
    public const string AccessAsUserScope = "access_as_user";

    public static bool HasAccessAsUserScope(System.Security.Claims.ClaimsPrincipal principal)
    {
        return principal.FindAll("scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(AccessAsUserScope, StringComparer.Ordinal);
    }
}