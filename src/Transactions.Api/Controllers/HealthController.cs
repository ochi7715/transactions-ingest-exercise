using Microsoft.AspNetCore.Mvc;

namespace Transactions.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            message = "The Transactions API is running."
        });
    }
}
