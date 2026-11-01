using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace RemoteLink.Host;

public partial class MainWindow : Window
{
    private readonly PairingServer _server = new();

    public MainWindow()
    {
        InitializeComponent();
        _server.StatusChanged += message => Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            PreviewButton.IsEnabled = _server.HasPairedClient;
            if (!_server.DesktopPreviewEnabled) PreviewButton.Content = "允許桌面預覽";
        });
        Closed += async (_, _) => await _server.DisposeAsync();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            await _server.StartAsync();
            PairCodeText.Text = _server.PairCode;
            AddressText.Text = $"主機位址：{GetLocalIPv4()}:{PairingServer.Port}";
            StopButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"啟動失敗：{ex.Message}";
            StartButton.IsEnabled = true;
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var enable = !_server.DesktopPreviewEnabled;
        _server.SetDesktopPreviewEnabled(enable);
        PreviewButton.Content = enable ? "停止桌面預覽" : "允許桌面預覽";
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        _server.SetDesktopPreviewEnabled(false);
        await _server.StopAsync();
        PairCodeText.Text = "------";
        AddressText.Text = "主機位址：尚未啟動";
        StartButton.IsEnabled = true;
        PreviewButton.IsEnabled = false;
        PreviewButton.Content = "允許桌面預覽";
    }

    private static string GetLocalIPv4()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address.ToString())
            .ToArray();
        return addresses.FirstOrDefault() ?? "請確認 Wi-Fi/乙太網路";
    }
}
