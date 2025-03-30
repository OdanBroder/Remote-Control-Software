using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] User user)
        {
            return Ok(new { Token = "dummy-jwt-token" });
        }
    }
}
