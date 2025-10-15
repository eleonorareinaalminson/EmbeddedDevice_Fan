using MainApp.Models;

namespace MainApp.Services;

public interface IDeviceStateProvider
{
    object GetStatus();
    void HandleCommand(CommandRequest command);
}