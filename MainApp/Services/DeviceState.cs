using MainApp.Models;

namespace MainApp.Services;

public static class DeviceState
{
    public static Func<object>? GetStatusFunc { get; set; }
    public static Action<CommandRequest>? HandleCommandAction { get; set; }
}