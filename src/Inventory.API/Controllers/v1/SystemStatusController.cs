using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers.v1;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemStatusController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public SystemStatusController(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult<object> GetSystemInfo()
    {
        var isDemoEnabled = _configuration.GetValue<bool>("Seed:EnableDemoData");

        return Ok(new
        {
            service = "Enterprise Inventory & Order Management SaaS API",
            apiVersion = "v1",
            environment = _environment.EnvironmentName,
            isDemoEnvironment = isDemoEnabled,
            timestampUtc = DateTimeOffset.UtcNow
        });
    }
}
