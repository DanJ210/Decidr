using backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class AuthenticationModeResolverTests
{
    [Fact]
    public void Explicit_seeded_testing_mode_overrides_entra_identifiers()
    {
        var configuration = CreateConfiguration(
            ("Authentication:Mode", "SeededTesting"),
            ("Entra:Authority", "https://tenant.example/"),
            ("Entra:Audience", "api://decidr"));

        var mode = AuthenticationModeResolver.Resolve(configuration, CreateEnvironment(Environments.Production));

        Assert.Equal(AuthenticationMode.SeededTesting, mode);
    }

    [Fact]
    public void Explicit_entra_mode_is_selected()
    {
        var configuration = CreateConfiguration(("Authentication:Mode", "Entra"));

        var mode = AuthenticationModeResolver.Resolve(configuration, CreateEnvironment(Environments.Production));

        Assert.Equal(AuthenticationMode.Entra, mode);
    }

    [Fact]
    public void Missing_mode_preserves_entra_inference()
    {
        var configuration = CreateConfiguration(
            ("Entra:Authority", "https://tenant.example/"),
            ("Entra:Audience", "api://decidr"));

        var mode = AuthenticationModeResolver.Resolve(configuration, CreateEnvironment(Environments.Development));

        Assert.Equal(AuthenticationMode.Entra, mode);
    }

    [Fact]
    public void Invalid_mode_is_rejected()
    {
        var configuration = CreateConfiguration(("Authentication:Mode", "Disabled"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AuthenticationModeResolver.Resolve(configuration, CreateEnvironment(Environments.Production)));

        Assert.Contains("Entra", exception.Message);
        Assert.Contains("SeededTesting", exception.Message);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value))
            .Build();
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}