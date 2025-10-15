using MainApp.Services;
using MainApp.Models;
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
    private RestApiHostService? _restApiHost;
    private bool _isConnected = false;

    private const int MAX_UI_LOG_LINES = 50;

    public MainWindow()
    {
        InitializeComponent();
        InitializeFeatures();
        _ = InitializeServicesAsync();
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

    private async Task InitializeServicesAsync()
    {
        _configService = new ConfigurationService();

        var stateProvider = new DeviceStateProvider(this);

        try
        {
            _restApiHost = new RestApiHostService("http://localhost:5001", stateProvider);
            await _restApiHost.StartAsync();
            LogMessage("REST API started on http://localhost:5001");
            LogMessage("Endpoints:");
            LogMessage("- GET  http://localhost:5001/api/health");
            LogMessage("- GET  http://localhost:5001/api/status");
            LogMessage("- POST http://localhost:5001/api/command");
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to start REST API: {ex.Message}");
            MessageBox.Show($"REST API failed to start: {ex.Message}\n\nDevice will work in standalone mode.",
                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await InitializeServiceBusAsync();
    }

    private async Task InitializeServiceBusAsync()
    {
        try
        {
            _serviceBusClient = new DeviceServiceBusClient(
                _configService!.GetDeviceId(),
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
            LogMessage($"Service Bus unavailable: {ex.Message}");
            LogMessage("Device will work in REST-only mode");
        }
    }

    public object GetDeviceStatus()
    {
        return new
        {
            deviceId = _configService?.GetDeviceId() ?? "fan-001",
            deviceType = 1,
            state = _isRunning ? 2 : (_isConnected ? 1 : 0),
            isRunning = _isRunning,
            speed = _currentSpeed,
            timestamp = DateTime.UtcNow,
            properties = new Dictionary<string, object>
            {
                { "Speed", _currentSpeed },
                { "IsRunning", _isRunning }
            }
        };
    }

    public void HandleRestCommand(CommandRequest command)
    {
        Dispatcher.Invoke(async () =>
        {
            LogMessage($"REST Command received: {command.Action}");

            switch (command.Action.ToLower())
            {
                case "start":
                    if (!_isRunning)
                    {
                        await ToggleRunningStateAsync();
                        LogMessage("Fan started via REST");
                    }
                    else
                    {
                        LogMessage("Fan already running");
                    }
                    break;

                case "stop":
                    if (_isRunning)
                    {
                        await ToggleRunningStateAsync();
                        LogMessage("Fan stopped via REST");
                    }
                    else
                    {
                        LogMessage("Fan already stopped");
                    }
                    break;

                case "setspeed":
                    if (command.Parameters.TryGetValue("Value", out var speedObj))
                    {
                        var speed = Convert.ToDouble(speedObj, System.Globalization.CultureInfo.InvariantCulture);

                        if (speed < 0.1 || speed > 3.0)
                        {
                            LogMessage($"Invalid speed {speed:0.00} (must be 0.1-3.0)");
                            break;
                        }

                        Slider_Speed.Value = speed;

                        if (_isRunning && _rotatingFan != null)
                        {
                            _rotatingFan.SetSpeedRatio(speed);
                            _currentSpeed = speed;
                            LogMessage($"Speed set to {speed:0.00} via REST");
                            await SendStatusUpdateAsync();
                        }
                        else
                        {
                            _currentSpeed = speed;
                            LogMessage($"Speed preset to {speed:0.00} (fan not running)");
                        }
                    }
                    else
                    {
                        LogMessage("Missing 'Value' parameter");
                    }
                    break;

                default:
                    LogMessage($"Unknown command: {command.Action}");
                    break;
            }
        });
    }

    private async void OnCommandReceived(object? sender, DeviceCommandMessage command)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            LogMessage($"Service Bus Command: {command.Action}");

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
                        var speed = Convert.ToDouble(speedObj, System.Globalization.CultureInfo.InvariantCulture);
                        Slider_Speed.Value = speed;

                        if (_isRunning && _rotatingFan != null)
                        {
                            _rotatingFan.SetSpeedRatio(speed);
                            _currentSpeed = speed;
                            LogMessage($"Speed set to {speed:0.00} via Service Bus");
                            await SendStatusUpdateAsync();
                        }
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

        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(Txt_StatusLog.Text))
            {
                Txt_StatusLog.Text = line + Environment.NewLine + Txt_StatusLog.Text;
            }
            else
            {
                Txt_StatusLog.Text = line;
            }

            var lines = Txt_StatusLog.Text.Split(Environment.NewLine);
            if (lines.Length > MAX_UI_LOG_LINES)
            {
                Txt_StatusLog.Text = string.Join(Environment.NewLine, lines.Take(MAX_UI_LOG_LINES));
            }
        });

        try
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
        catch { }
    }

    protected override async void OnClosed(EventArgs e)
    {
        _statusTimer?.Stop();
        _speedTimer?.Stop();

        if (_restApiHost != null)
        {
            try
            {
                await _restApiHost.StopAsync();
                LogMessage("REST API stopped");
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping REST API: {ex.Message}");
            }
        }

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

        base.OnClosed(e);
    }
}