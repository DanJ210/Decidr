using System.Reflection;
using backend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class ConfigurationControllerTests
{
    [Fact]
    public void Authentication_configuration_is_anonymous()
    {
        var method = typeof(ConfigurationController)
            .GetMethod(nameof(ConfigurationController.GetAuthenticationConfiguration));

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData("Entra")]
    [InlineData("SeededTesting")]
    public void Authentication_configuration_returns_resolved_mode(string configuredMode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = configuredMode,
            })
            .Build();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Production);
        var controller = new ConfigurationController(configuration, environment.Object);

        var result = controller.GetAuthenticationConfiguration();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var mode = ok.Value?.GetType().GetProperty("mode")?.GetValue(ok.Value);
        Assert.Equal(configuredMode, mode);
    }
}