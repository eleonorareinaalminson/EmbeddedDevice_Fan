

namespace MainApp.Services;

public class DeviceCommandMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
