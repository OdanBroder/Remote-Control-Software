using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Server.Middleware
{
    public class TokenBlacklistMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenBlacklistMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (!string.IsNullOrEmpty(token))
            {
                var isBlacklisted = await dbContext.BlacklistedTokens
                    .AnyAsync(b => b.Token == token && b.ExpiresAt > DateTime.UtcNow);

                if (isBlacklisted)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { Message = "Token has been revoked" });
                    return;
                }
            }

            await _next(context);
        }
    }
} 