using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ConfigurationController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet("authentication")]
    public ActionResult<object> GetAuthenticationConfiguration()
    {
        var mode = AuthenticationModeResolver.Resolve(_configuration, _environment);
        return Ok(new { mode = mode.ToString() });
    }
}