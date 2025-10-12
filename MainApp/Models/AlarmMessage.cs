
namespace MainApp.Models;


public class AlarmMessage
{
    public string AlarmId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public DateTime Timestamp { get; set; }
    public bool IsAcknowledged { get; set; }
}