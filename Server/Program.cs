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

// Load .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "RemoteControl_DB";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "h2a";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "yourpassword";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "yoursecret";
Console.WriteLine($"DB_HOST: {dbHost}");
Console.WriteLine($"DB_PORT: {dbPort}");
Console.WriteLine($"DB_NAME: {dbName}");
Console.WriteLine($"DB_USER: {dbUser}");
Console.WriteLine($"DB_PASSWORD: {dbPassword}");
Console.WriteLine($"JWT_SECRET: {jwtSecret}");

var connectionString = $"server={dbHost};port={dbPort};database={dbName};user={dbUser};password={dbPassword};";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RemoteSessionService>();
builder.Services.AddSingleton<ScreenCaptureService>();
builder.Services.AddSingleton<InputHandlerService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.UseUrls("https://0.0.0.0:5031");

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

app.UseCors("AllowAll");

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RemoteControlHub>("/remote-control-access");

app.Run();
