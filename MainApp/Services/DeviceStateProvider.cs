using MainApp.Models;

namespace MainApp.Services;

public class DeviceStateProvider : IDeviceStateProvider
{
    private readonly MainWindow _mainWindow;

    public DeviceStateProvider(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    public object GetStatus()
    {
        return _mainWindow.GetDeviceStatus();
    }

    public void HandleCommand(CommandRequest command)
    {
        _mainWindow.HandleRestCommand(command);
    }
}