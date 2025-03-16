using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class TcpServer
{
    private readonly TcpListener _listener;

    public TcpServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine("Server is listening...");

        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine("Client connected!");

        using var stream = client.GetStream();
        byte[] buffer = new byte[1024];

        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Console.WriteLine($"Received from Client: {receivedMessage}");

        string response = "Hello from Server!";
        byte[] responseData = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(responseData, 0, responseData.Length);
    }
}
