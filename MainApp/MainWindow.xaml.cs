using MainApp.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MainApp;
public partial class MainWindow : Window
{
    private string _logFilePath = "";
    private double _pendingSpeed;
    private double _currentSpeed = 1.0;
    private bool _isRunning = false;
    private Storyboard? _rotatingFan;
    private DispatcherTimer? _speedTimer;
    private DispatcherTimer? _statusTimer;
    private List<string>? _eventLog;

    private DeviceServiceBusClient? _serviceBusClient;
    private ConfigurationService? _configService;
    private bool _isConnected = false;

    private const int MAX_UI_LOG_LINES = 50; // Begränsa antal rader i UI

    public MainWindow()
    {
        InitializeComponent();
        InitializeFeatures();
        _ = InitializeServiceBusAsync();
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
            _ = SendStatusUpdateAsync();
        };

        _statusTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _statusTimer.Tick += async (_, _) => await SendStatusUpdateAsync();

        _eventLog = [];

        var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmbeddedDevice");
        Directory.CreateDirectory(appDir);
        _logFilePath = Path.Combine(appDir, "eventlog.log");
    }

    private async Task InitializeServiceBusAsync()
    {
        try
        {
            _configService = new ConfigurationService();

            _serviceBusClient = new DeviceServiceBusClient(
                _configService.GetDeviceId(),
                _configService.GetServiceBusConnectionString(),
                _configService.GetStatusQueue(),
                _configService.GetCommandQueue(),
                _configService.GetAlarmQueue()
            );

            _serviceBusClient.CommandReceived += OnCommandReceived;

            await _serviceBusClient.InitializeAsync();
            _isConnected = true;

            LogMessage("Connected to Azure Service Bus");

            await SendStatusUpdateAsync();

            _statusTimer?.Start();
        }
        catch (Exception ex)
        {
            _isConnected = false;
            LogMessage($"Failed to connect to Service Bus: {ex.Message}");
            MessageBox.Show($"Service Bus connection failed: {ex.Message}\n\nDevice will work in standalone mode.",
                "Connection Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnCommandReceived(object? sender, DeviceCommandMessage command)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            LogMessage($"Command received: {command.Action}");

            switch (command.Action)
            {
                case "Start":
                    if (!_isRunning)
                        await ToggleRunningStateAsync();
                    break;

                case "Stop":
                    if (_isRunning)
                        await ToggleRunningStateAsync();
                    break;

                case "SetSpeed":
                    if (command.Parameters.TryGetValue("Value", out var speedObj))
                    {
                        var speed = Convert.ToDouble(speedObj);
                        Slider_Speed.Value = speed;
                    }
                    break;
            }
        });
    }

    private async Task SendStatusUpdateAsync()
    {
        if (!_isConnected || _serviceBusClient == null)
            return;

        try
        {
            var status = new DeviceStatusMessage
            {
                DeviceType = 1,
                State = _isRunning ? 2 : (_isConnected ? 1 : 0),
                Properties = new Dictionary<string, object>
                {
                    { "Speed", _currentSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    { "IsRunning", _isRunning }
                }
            };

            await _serviceBusClient.SendStatusAsync(status);

            await CheckAlarmThresholdAsync();
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to send status: {ex.Message}");
        }
    }

    private async Task CheckAlarmThresholdAsync()
    {
        if (_serviceBusClient == null || _configService == null)
            return;

        var threshold = _configService.GetAlarmSpeedThreshold();

        if (_isRunning && _currentSpeed > threshold)
        {
            var alarm = new AlarmMessage
            {
                Message = $"Fan speed ({_currentSpeed:F2}) exceeds threshold ({threshold:F2})",
                Severity = _currentSpeed > threshold + 0.5 ? "Critical" : "Warning"
            };

            await _serviceBusClient.SendAlarmAsync(alarm);
            LogMessage($"ALARM: {alarm.Message}");
        }
    }

    private void Btn_OnOff_Click(object sender, RoutedEventArgs e)
    {
        _ = ToggleRunningStateAsync();
    }

    private async Task ToggleRunningStateAsync()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            Btn_OnOff.Content = "STOP";
            _rotatingFan?.Begin();
            _rotatingFan?.SetSpeedRatio(Slider_Speed.Value);
            _currentSpeed = Slider_Speed.Value;
            LogMessage("Fan started");
        }
        else
        {
            _isRunning = false;
            Btn_OnOff.Content = "START";
            _rotatingFan?.Pause();
            LogMessage("Fan stopped");
        }

        await SendStatusUpdateAsync();
    }

    private void Slider_Speed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isRunning && _rotatingFan is not null)
        {
            _rotatingFan?.SetSpeedRatio(e.NewValue);
            _currentSpeed = e.NewValue;
            _pendingSpeed = e.NewValue;
            _speedTimer?.Stop();
            _speedTimer?.Start();
        }
    }

    private void LogMessage(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";
        _eventLog?.Add(line);

        // Uppdatera UI
        Dispatcher.Invoke(() =>
        {
            // Lägg till ny rad överst
            if (!string.IsNullOrEmpty(Txt_StatusLog.Text))
            {
                Txt_StatusLog.Text = line + Environment.NewLine + Txt_StatusLog.Text;
            }
            else
            {
                Txt_StatusLog.Text = line;
            }

            // Begränsa antal rader i UI för prestanda
            var lines = Txt_StatusLog.Text.Split(Environment.NewLine);
            if (lines.Length > MAX_UI_LOG_LINES)
            {
                Txt_StatusLog.Text = string.Join(Environment.NewLine, lines.Take(MAX_UI_LOG_LINES));
            }
        });

        // Spara till fil
        try
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch { }
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _statusTimer?.Stop();
        _speedTimer?.Stop();

        if (_isConnected && _serviceBusClient != null)
        {
            try
            {
                var status = new DeviceStatusMessage
                {
                    DeviceType = 1,
                    State = 0,
                    Properties = new Dictionary<string, object>()
                };
                await _serviceBusClient.SendStatusAsync(status);
            }
            catch { }

            await _serviceBusClient.DisposeAsync();
        }
    }
}