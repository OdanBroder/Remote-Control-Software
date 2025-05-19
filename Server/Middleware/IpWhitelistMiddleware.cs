using Microsoft.AspNetCore.Http;
using Server.Services;

namespace Server.Middleware
{
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IpWhitelistMiddleware> _logger;

        public IpWhitelistMiddleware(
            RequestDelegate next,
            IServiceProvider serviceProvider,
            ILogger<IpWhitelistMiddleware> logger)
        {
            _next = next;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.User.FindFirst("UserId")?.Value;
            if (userId != null)
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var securityService = scope.ServiceProvider.GetRequiredService<SecurityService>();
                    var isWhitelisted = await securityService.IsIpWhitelisted(int.Parse(userId), ipAddress);
                    if (!isWhitelisted)
                    {
                        _logger.LogWarning($"Access denied for IP: {ipAddress}, User: {userId}");
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Access denied: IP not whitelisted");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
} 