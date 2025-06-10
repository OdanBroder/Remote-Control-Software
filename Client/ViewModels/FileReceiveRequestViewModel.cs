using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Client.Helpers;
using Client.Services;
using System.Windows;
using System.Reflection;

public class FileReceiveRequestViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public int TransferId { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public string SessionId { get; set; } 
    public IAsyncRelayCommand AcceptCommand { get; }
    public ICommand RejectCommand { get; }

    public event Action<bool> RequestClosed;

    private int _progress;
    private readonly FileTransferService _fileTransferService = new FileTransferService();
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(nameof(Progress)); }
    }

    private string _status;
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }
    public FileReceiveRequestViewModel()
    {
        SessionId = SessionStorage.LoadSession();
        AcceptCommand = new global::CommunityToolkit.Mvvm.Input.AsyncRelayCommand(OnAcceptAsync);
        RejectCommand = new RelayCommand(OnReject);
    }

    private async Task OnAcceptAsync()
    {
        try
        {
            Status = "Preparing to receive...";
            if (string.IsNullOrWhiteSpace(SessionId))
            {
                Status = "SessionId is required!";
                return;
            }
            var (success, port, message) = await _fileTransferService.ConnectToReceiverTcpAsync(SessionId);
            if (!success)
            {
                Status = $"Error: {message}";
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                FileName = FileName,
                Filter = "All files|*.*"
            };
            if (saveDialog.ShowDialog() != true)
            {
                Status = "Cancelled";
                return;
            }
            string savePath = saveDialog.FileName;

            Status = "Receiving file...";
            await _fileTransferService.ReceiveFileOverTcpAsync(
                host: AppSettings.ServerIP,
                port: port,
                savePath: savePath,
                fileSize: FileSize,
                onProgress: p => Progress = (int)(p),
                token: CancellationToken.None
            );
            Status = "Received!";
            RequestClosed?.Invoke(true);

            //Dong ngay tai dai.
        }
        catch (Exception ex)
        {
            Status = "Failed: " + ex.Message;
        }
    }

    private void OnReject()
    {
        Status = "Rejected";
        RequestClosed?.Invoke(false);
    }

    protected void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
