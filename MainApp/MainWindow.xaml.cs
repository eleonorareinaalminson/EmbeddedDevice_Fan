using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MainApp;

public partial class MainWindow : Window
{
    private string _logFilePath = "";
    private double _pendingSpeed;
    private bool _isRunning = false;
    private Storyboard? _rotatingFan;
    private DispatcherTimer? _speedTimer;
    private List<string>? _eventLog;
    public MainWindow()
    {
        InitializeComponent();
        InitializeFeatures();
    }
    private void InitializeFeatures()
    {
        _rotatingFan = ((BeginStoryboard)FindResource("sb-rotate-fan")).Storyboard;
        _speedTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _speedTimer.Tick += (_, _) =>
        {
            _speedTimer.Stop();
            LogMessage($"Fan speed set to {_pendingSpeed:0.00}");
        };
        _eventLog = [];

        var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmbeddedDevice");
        Directory.CreateDirectory(appDir);
        _logFilePath = Path.Combine(appDir, "eventlog.log");
    }

    private void Btn_OnOff_Click(object sender, RoutedEventArgs e)
    {
        ToggleRunningState();

    }

    private void ToggleRunningState()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            Btn_OnOff.Content = "STOP";
            _rotatingFan?.Begin();
            _rotatingFan?.SetSpeedRatio(Slider_Speed.Value);
            LogMessage("Fan started");

        }
        else
        {
            _isRunning = false;
            Btn_OnOff.Content = "START";
            _rotatingFan?.Pause();
            LogMessage("Fan stopped");

        }
    }

    private void Slider_Speed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {

        if (_isRunning && _rotatingFan is not null)
        {
            _rotatingFan?.SetSpeedRatio(e.NewValue);
            _pendingSpeed = e.NewValue;
            _speedTimer?.Stop();
            _speedTimer?.Start();
        }
    }

    private void LogMessage(string message)
    {
        string line = @$"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";
        _eventLog?.Add(line);

        try 
        { 
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch { }


    }
}