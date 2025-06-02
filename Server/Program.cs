using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;
using Server.Hubs;
using dotenv.net;
using Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Mvc;
using MessagePack;

// Load .env file
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "RemoteControl_DB";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "h2a";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "yourpassword";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "yoursecret";
var certpasswd = Environment.GetEnvironmentVariable("CERT_PASSWORD") ?? "yourpassword";
Console.WriteLine($"DB_HOST: {dbHost}");
Console.WriteLine($"DB_PORT: {dbPort}");
Console.WriteLine($"DB_NAME: {dbName}");
Console.WriteLine($"DB_USER: {dbUser}");
Console.WriteLine($"DB_PASSWORD: {dbPassword}");
Console.WriteLine($"JWT_SECRET: {jwtSecret}");

var connectionString = $"server={dbHost};port={dbPort};database={dbName};user={dbUser};password={dbPassword};";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Configure request body size limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue; // or a specific limit like 30000000 for 30MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // or a specific limit like 30000000 for 30MB
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "remote-control-server",
            ValidAudience = "remote-control-client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Add event handler for SignalR authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/remotecontrolhub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .WithOrigins("https://localhost:5031")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition")
            .SetIsOriginAllowed(origin => true)); // For development only
});

// Configure controllers
builder.Services.AddControllers();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.StreamBufferCapacity = 10;
    options.MaximumParallelInvocationsPerClient = 1;
    
    // Add performance optimizations
    options.EnableDetailedErrors = false; // Disable in production
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
    options.StreamBufferCapacity = 100;
    options.MaximumParallelInvocationsPerClient = 10;
})
.AddMessagePackProtocol();

builder.Services.AddScoped<RemoteSessionService>();
builder.Services.AddScoped<FileTransferService>();
builder.Services.AddScoped<SessionQualityService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddScoped<CryptoService>();
// builder.Services.AddSingleton<ScreenCaptureService>();
builder.Services.AddSingleton<InputHandlerService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTPS configuration
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Configure thread pool
    serverOptions.Limits.MaxConcurrentConnections = 100;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
    serverOptions.Limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.MaxRequestBufferSize = 1024 * 1024; // 1 MB
    serverOptions.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB

    serverOptions.ConfigureHttpsDefaults(listenOptions =>
    {
        listenOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
        listenOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("certs/server.pfx", certpasswd);
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.EnsureCreated(); // Or dbContext.Database.Migrate(); if using migrations
        Console.WriteLine("✅ Database connection successful.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Database connection failed:");
        Console.WriteLine(ex.Message);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Add token blacklist middleware
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseMiddleware<IpWhitelistMiddleware>();

app.MapControllers();
app.MapHub<RemoteControlHub>("/remotecontrolhub", options =>
{
    options.CloseOnAuthenticationExpiration = true;
    options.ApplicationMaxBufferSize = 102400; // 100 KB
    options.TransportMaxBufferSize = 102400; // 100 KB
    options.TransportSendTimeout = TimeSpan.FromSeconds(30);
    options.WebSockets.CloseTimeout = TimeSpan.FromSeconds(30);
});

// Configure static files
app.UseStaticFiles();
app.MapFallbackToFile("remote_control_test.html");

app.Run();