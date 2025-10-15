# EmbeddedDevice_Fan

WPF-based IoT application for controlling and monitoring an embedded device (fan).

## Features

- **Local UI Control** – Start/Stop and speed adjustment via slider
- **Azure Service Bus** – Receives commands and sends status/alarm messages
- **REST API** – Local API on port 5001 for remote control
- **Status Logging** – Real-time logging in UI and file storage
- **Alarm Thresholds** – Automatic alarm monitoring for high speeds

## System Requirements

- .NET 9.0 (Windows)
- Azure Service Bus connection (configured in `appsettings.json`)

## Installation & Running

```bash
dotnet build
dotnet run
```

## Configuration

Edit `MainApp/appsettings.json`:

```json
{
  "Device": {
    "DeviceId": "fan-001",
    "Name": "Office Fan"
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://...",
    "StatusQueue": "device-status",
    "CommandQueue": "device-commands",
    "AlarmQueue": "device-alarms"
  },
  "Thresholds": {
    "AlarmSpeedThreshold": 2.8
  }
}
```

## REST API

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check |
| `/api/status` | GET | Current device status |
| `/api/command` | POST | Send command |

### Example: Set Speed via REST

```bash
curl -X POST http://localhost:5001/api/command \
  -H "Content-Type: application/json" \
  -d '{"Action":"setspeed","Parameters":{"Value":2.5}}'
```

## Architecture

- **MainWindow.xaml.cs** – UI logic and state management
- **DeviceServiceBusClient** – Azure Service Bus integration
- **RestApiHostService** – Local REST server
- **ConfigurationService** – Configuration management

## License

Internal use
