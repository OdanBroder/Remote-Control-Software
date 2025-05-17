using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
//using Server.Hubs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Debug()
  .WriteTo.Console()
  .WriteTo.File("logs/server.txt", rollingInterval: RollingInterval.Day)
  .CreateLogger();
builder.Host.UseSerilog();

// Đăng ký SignalR và WebRTCServer
builder.Services.AddSignalR();
builder.Services.AddSingleton<WebRTCServer>();
builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

var app = builder.Build();
app.UseRouting();
app.UseSerilogRequestLogging();
app.MapHub<RemoteControlHub>("/signal");
app.Run("http://localhost:5000");
