using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
	public class TcpClientService
	{
		private readonly TcpClient _client;
		private readonly string _serverIp;
		private readonly int _port;

		public TcpClientService(string serverIp, int port)
		{
			_client = new TcpClient();
			_serverIp = serverIp;
			_port = port;
		}

		public async Task ConnectAsync()
		{
			await _client.ConnectAsync(_serverIp, _port);
			using var stream = _client.GetStream();
			byte[] data = Encoding.UTF8.GetBytes("Hello Server!");
			await stream.WriteAsync(data, 0, data.Length);

			byte[] buffer = new byte[1024];
			int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
			string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
			Console.WriteLine($"Server Response: {response}");
		}
	}
}