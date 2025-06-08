using System;
using System.ComponentModel;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Client.Helpers;
using Client.Services;
using CommunityToolkit.Mvvm.Input;

public class FileTransferViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler TransferCompleted;

    public ICommand ChooseFileCommand { get; }
    public IAsyncRelayCommand SendFileCommand { get; }

    private string _selectedFileName;
    public string SelectedFileName
    {
        get => _selectedFileName;
        set { _selectedFileName = value; OnPropertyChanged(nameof(SelectedFileName)); }
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(nameof(Progress)); }
    }

    public string SessionId { get; set; }
    public string Host { get; set; } = AppSettings.ServerIP;

    private string _filePath;
    private CancellationTokenSource _cts;
    private readonly FileTransferService _service;

    private string _status;
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }
    public FileTransferViewModel()
    {
        _service = new FileTransferService();
        SessionId = SessionStorage.LoadSession();

        ChooseFileCommand = new RelayCommand(OnChooseFile);
        SendFileCommand = new AsyncRelayCommand<object>(
            _ => OnSendFileAsync(),
            _ => CanSendFile()
        );
    }

    private void OnChooseFile()
    {
        var file = _service.PickFile();
        if (!string.IsNullOrEmpty(file))
        {
            _filePath = file;
            SelectedFileName = Path.GetFileName(_filePath);
            SendFileCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSendFile() => !string.IsNullOrEmpty(_filePath);

    private async Task OnSendFileAsync()
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        _cts = new CancellationTokenSource();

        var fileInfo = new FileInfo(_filePath);

        Status = "Requesting server to prepare TCP session...";
        var (success, port, sessionId, msg) = await _service.InitiateTcpTransferAsync(SessionId, SelectedFileName, fileInfo.Length);

        Console.WriteLine($"[FileTransfer] Initiated TCP transfer: Success={success}, Port={port}, SessionId={sessionId}, Message={msg}");
        if (!success)
        {
            Status = "Failed to initiate transfer: " + msg;
            MessageBox.Show(Status, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            Status = "Sending file...";
            await _service.SendFileOverTcpAsync(Host, port, _filePath, fileInfo.Length, p => Progress = p, _cts.Token);
            Status = "File sent successfully!";
            TransferCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = "Transfer failed: " + ex.Message;
            MessageBox.Show(Status, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    public void StartTcpFileTransfer(int transferId)
    {
        Status = "File transfer accepted. Sending file...";
    }

    protected void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
