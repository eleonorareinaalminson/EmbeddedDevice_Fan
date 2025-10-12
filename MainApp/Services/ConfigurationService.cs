using Microsoft.Extensions.Configuration;

namespace MainApp.Services;
public class ConfigurationService
{
    public IConfiguration Configuration { get; }

    public ConfigurationService()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Configuration = builder.Build();
    }

    public string GetDeviceId() => Configuration["Device:DeviceId"] ?? "unknown-device";
    public string GetDeviceName() => Configuration["Device:Name"] ?? "Unknown Device";
    public string GetServiceBusConnectionString() => Configuration["ServiceBus:ConnectionString"] ?? throw new InvalidOperationException("Connection string missing");

    public string GetStatusQueue() => Configuration["ServiceBus:StatusQueue"] ?? "device-status";
    public string GetCommandQueue() => Configuration["ServiceBus:CommandQueue"] ?? "device-commands";
    public string GetAlarmQueue() => Configuration["ServiceBus:AlarmQueue"] ?? "device-alarms";

    public double GetAlarmSpeedThreshold() => double.Parse(Configuration["Thresholds:AlarmSpeedThreshold"] ?? "2.8");
}
