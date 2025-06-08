using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Client.Helpers;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

public class FileTransferService
{
    public string BackendBaseUrl => AppSettings.BaseApiUri;
    private readonly HttpClient _httpClient;

    public FileTransferService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var token = TokenStorage.LoadToken();
            if (!string.IsNullOrEmpty(token))
                SetAuthToken(token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileTransferService] Token load error: {ex.Message}");
        }
    }

    public void SetAuthToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public string PickFile()
    {
        var dlg = new OpenFileDialog();
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public async Task<(bool success, int port, string sessionId, string message)> InitiateTcpTransferAsync(string sessionId, string fileName, long fileSize)
    {
        var url = $"{BackendBaseUrl}/api/FileTransfer/tcp/initiate?sessionId={sessionId}&fileName={Uri.EscapeDataString(fileName)}&fileSize={fileSize}";
        try
        {
            var response = await _httpClient.PostAsync(url, null);
            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            if (obj["success"]?.Value<bool>() == true)
            {
                int port = obj["data"]["port"].Value<int>();
                string id = obj["data"]["id"].Value<string>();
                return (true, port, id, "");
            }
            else
            {
                return (false, 0, null, obj["message"]?.ToString() ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileTransferService] Error initiating TCP transfer: {ex.Message}");
            return (false, 0, null, ex.Message);
        }
    }

    public async Task SendFileOverTcpAsync(string host, int port, string filePath, long fileSize, Action<int> onProgress, CancellationToken token)
    {
        const int BufferSize = 64 * 1024;
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port);

        using var netStream = tcpClient.GetStream();
        using var fs = File.OpenRead(filePath);
        var buffer = new byte[BufferSize];
        long totalSent = 0;
        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buffer, 0, BufferSize, token)) > 0)
        {
            await netStream.WriteAsync(buffer, 0, bytesRead, token);
            totalSent += bytesRead;
            int percent = (int)((double)totalSent / fileSize * 100);
            onProgress?.Invoke(percent);
        }
    }

    public async Task<(bool success, int port, string message)> ConnectToReceiverTcpAsync(string sessionId)
    {
        try
        {
            var url = $"{BackendBaseUrl}/api/FileTransfer/tcp/connect?sessionId={sessionId}";
            var response = await _httpClient.PostAsync(url, null);
            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            if (obj["success"]?.Value<bool>() == true)
            {
                int port = obj["data"]["port"].Value<int>();
                return (true, port, "");
            }
            else
            {
                return (false, 0, obj["message"]?.ToString() ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    public async Task ReceiveFileOverTcpAsync(string host, int port, string savePath, long fileSize, Action<int> onProgress, CancellationToken token)
    {
        const int BufferSize = 64 * 1024;
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port);

        using var netStream = tcpClient.GetStream();
        using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[BufferSize];
        long totalRead = 0;
        int bytesRead;

        while (totalRead < fileSize && (bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
        {
            await fs.WriteAsync(buffer, 0, bytesRead, token);
            totalRead += bytesRead;
            int percent = (int)((double)totalRead / fileSize * 100);
            onProgress?.Invoke(percent);
        }
    }
}
