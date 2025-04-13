using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

public class RemoteControlClient
{
    private HubConnection _connection;

    public RemoteControlClient()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5031/remote-control-access") // Server URL
            .Build();
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _connection.StartAsync();
            Console.WriteLine("Connected to server");

            _connection.On<string, byte[]>("ReceiveScreenData", (sessionId, imageData) =>
            {
                Console.WriteLine($"Received screen data for session {sessionId}");
            });

            _connection.On<string, string>("ReceiveInputAction", (sessionId, action) =>
            {
                Console.WriteLine($"Received input action for session {sessionId}: {action}");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
    }

    public async Task SendScreenData(string sessionId, byte[] imageData)
    {
        await _connection.InvokeAsync("SendScreenData", sessionId, imageData);
    }

    public async Task SendInputAction(string sessionId, string action)
    {
        await _connection.InvokeAsync("SendInputAction", sessionId, action);
    }
}
