using System;
using System.Threading.Tasks;
using Server.Services;

class Program
{
    static async Task Main()
    {
        var server = new TcpServer(5000);
        await server.StartAsync();
    }
}