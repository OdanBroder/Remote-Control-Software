using System;
using System.Threading.Tasks;
using Client.Services;

class Program
{
    static async Task Main()
    {
        var client = new TcpClientService("127.0.0.1", 5000);
        await client.ConnectAsync();
    }
}