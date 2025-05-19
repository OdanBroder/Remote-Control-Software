using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Username))
            {
                return BadRequest("Username are required.");
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                return BadRequest("Password are required.");
            }
            user.InternalId = Guid.NewGuid().GetHashCode();
            user.CreatedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
            if (existingUser != null)
            {
                return Conflict("Username already exists.");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { Token = token });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User login)
        {
            if (string.IsNullOrEmpty(login.Username))
            {
                return BadRequest("Username are required.");
            }

            if (string.IsNullOrEmpty(login.Password))
            {
                return BadRequest("Password are required.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == login.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(login.Password, user.Password))
            {
                return Unauthorized("Invalid username or password.");
            }

            var token = GenerateJwtToken(user);
            return Ok(new { Token = token });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("No token provided");
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var expiresAt = jwtToken.ValidTo;

            var blacklistedToken = new BlacklistedToken
            {
                Token = token,
                ExpiresAt = expiresAt
            };

            _context.BlacklistedTokens.Add(blacklistedToken);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Successfully logged out" });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _config["JWT_SECRET"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT_SECRET is not configured.");
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.InternalId.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: "yourapp",
                audience: "yourapp",
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
