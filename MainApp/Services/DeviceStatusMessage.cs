
namespace MainApp.Services;

public class DeviceStatusMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public int DeviceType { get; set; } = 1; // 1 = Fan
    public int State { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}
