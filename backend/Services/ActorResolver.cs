using System.Security.Claims;
using backend.Data.Entities;

namespace backend.Services;

public sealed class ActorResolver : IActorResolver
{
    private const string DevelopmentUserHeader = "X-Dev-User-Id";
    private readonly IAuthenticatedUserService _authenticatedUserService;
    private readonly ICommunityCourtService _courtService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ActorResolver(
        IAuthenticatedUserService authenticatedUserService,
        ICommunityCourtService courtService,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _authenticatedUserService = authenticatedUserService;
        _courtService = courtService;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<UserEntity?> ResolveAsync(
        ClaimsPrincipal principal,
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated == true)
        {
            if (!AuthorizationPolicies.HasAccessAsUserScope(principal))
            {
                return null;
            }

            return await _authenticatedUserService.GetOrCreateAsync(principal, cancellationToken);
        }

        if (!_environment.IsDevelopment() ||
            !string.IsNullOrWhiteSpace(_configuration["Entra:Authority"]) ||
            !string.IsNullOrWhiteSpace(_configuration["Entra:Audience"]) ||
            !request.Headers.TryGetValue(DevelopmentUserHeader, out var values) ||
            !Guid.TryParse(values.FirstOrDefault(), out var userId))
        {
            return null;
        }

        return _courtService.GetUser(userId) is { } user
            ? new UserEntity
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Role = user.Role,
            }
            : null;
    }
}