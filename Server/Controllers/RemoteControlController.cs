using Microsoft.AspNetCore.Mvc;
using Server.Models;
namespace Server.Controllers
{
    [ApiController]
    [Route("api/remote")]
    public class RemoteControlController : ControllerBase
    {
        [HttpPost("start")]
        public IActionResult StartSession([FromBody] RemoteSession session)
        {
            return Ok(new { Message = "Session started", SessionId = session.Id });
        }
    }
}