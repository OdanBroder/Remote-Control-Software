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
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

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
var development = Environment.GetEnvironmentVariable("DEPLOYMENT") ?? "development";
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

// Get Redis configuration from environment variables
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
var redisUser = Environment.GetEnvironmentVariable("REDIS_USER") ?? "default";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "yourpassword";

// Test Redis connection
try
{
    var options = new ConfigurationOptions
    {
        EndPoints = { { redisHost, int.Parse(redisPort) } },
        User = redisUser,
        Password = redisPassword,
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000
    };

    var redis = ConnectionMultiplexer.Connect(options);
    var db = redis.GetDatabase();
    await db.StringSetAsync("test", "Hello Redis!");
    var value = await db.StringGetAsync("test");
    Console.WriteLine($"✅ Redis connection successful. Test value: {value}");
}
catch (Exception ex)
{
    Console.WriteLine("❌ Redis connection failed:");
    Console.WriteLine(ex.Message);
}

// Add distributed session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure SignalR with Redis backplane
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
.AddStackExchangeRedis($"{redisHost}:{redisPort},user={redisUser},password={redisPassword},abortConnect=false,connectTimeout=5000,syncTimeout=5000", options =>
{
    options.Configuration.ChannelPrefix = "RemoteControl";
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

// Clear any default URLs
builder.WebHost.UseUrls();

var serverUrls = new string[0];
if (development == "development")
{
    var port = int.Parse(Environment.GetEnvironmentVariable("SERVER_PORT") ?? "5031");
    var serverUrl = $"http://localhost:{port}";
    serverUrls = new[] { serverUrl };
    builder.WebHost.UseUrls(serverUrl);
}
else
{
    // In production, use ASPNETCORE_URLS environment variable
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrEmpty(urls))
    {
        // Fallback to default ports if ASPNETCORE_URLS is not set
        var serverPorts = new[] { 5030, 5031, 5032 };
        serverUrls = serverPorts.Select(port => $"http://+:{port}").ToArray();
        builder.WebHost.UseUrls(serverUrls);
    }
    // If ASPNETCORE_URLS is set, it will be used automatically by ASP.NET Core
}

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Existing Kestrel configuration
    serverOptions.Limits.MaxConcurrentConnections = 100;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
    serverOptions.Limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 100, gracePeriod: TimeSpan.FromSeconds(10));
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.MaxRequestBufferSize = 1024 * 1024; // 1 MB
    serverOptions.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB
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

// Add session middleware
app.UseSession();

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

// Print server information
Console.WriteLine("Server instances running on:");
if (development == "development"){
    var port = int.Parse(Environment.GetEnvironmentVariable("SERVER_PORT") ?? "5031");
    Console.WriteLine($"✅ http://localhost:{port}");
}
else{
    foreach (var url in serverUrls)
    {
        Console.WriteLine($"✅ {url}");
    }
}

app.Run();