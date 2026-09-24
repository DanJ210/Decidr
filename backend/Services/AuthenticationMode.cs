namespace backend.Services;

public enum AuthenticationMode
{
    Entra,
    SeededTesting,
}

public static class AuthenticationModeResolver
{
    public static AuthenticationMode Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredMode = configuration["Authentication:Mode"];
        if (!string.IsNullOrWhiteSpace(configuredMode))
        {
            if (Enum.TryParse<AuthenticationMode>(configuredMode, ignoreCase: true, out var mode))
            {
                return mode;
            }

            throw new InvalidOperationException(
                "Authentication:Mode must be either 'Entra' or 'SeededTesting'.");
        }

        var entraConfigured =
            !string.IsNullOrWhiteSpace(configuration["Entra:Authority"]) &&
            !string.IsNullOrWhiteSpace(configuration["Entra:Audience"]);
        if (entraConfigured)
        {
            return AuthenticationMode.Entra;
        }

        if (environment.IsDevelopment())
        {
            return AuthenticationMode.SeededTesting;
        }

        throw new InvalidOperationException(
            "Authentication:Mode must be configured outside Development.");
    }
}