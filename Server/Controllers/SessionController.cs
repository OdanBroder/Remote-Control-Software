using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/session")]
    public class SessionController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult GetSession(string id)
        {
            return Ok(new { SessionId = id, Status = "Active" });
        }
    }
}
