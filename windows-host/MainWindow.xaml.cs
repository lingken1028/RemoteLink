using System.Windows;

namespace RemoteLink.Host;

public partial class MainWindow : Window
{
    private bool _running;

    public MainWindow()
    {
        InitializeComponent();
        SetState(false);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        SetState(true);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        SetState(false);
    }

    private void SetState(bool running)
    {
        _running = running;
        StatusText.Text = _running ? "主機介面已啟動（尚無遠端串流）" : "尚未啟動";
        PairCodeText.Text = "------";
        StartButton.IsEnabled = !_running;
        StopButton.IsEnabled = _running;
    }
}
