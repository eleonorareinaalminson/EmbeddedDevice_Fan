using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace MainApp.Services;

public class DeviceServiceBusClient : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly string _connectionString;
    private readonly string _statusQueue;
    private readonly string _commandQueue;
    private readonly string _alarmQueue;

    private ServiceBusClient? _client;
    private ServiceBusSender? _statusSender;
    private ServiceBusSender? _alarmSender;
    private ServiceBusProcessor? _commandProcessor;

    public event EventHandler<DeviceCommandMessage>? CommandReceived;

    public DeviceServiceBusClient(
        string deviceId,
        string connectionString,
        string statusQueue,
        string commandQueue,
        string alarmQueue)
    {
        _deviceId = deviceId;
        _connectionString = connectionString;
        _statusQueue = statusQueue;
        _commandQueue = commandQueue;
        _alarmQueue = alarmQueue;
    }

    public async Task InitializeAsync()
    {
        _client = new ServiceBusClient(_connectionString);

        _statusSender = _client.CreateSender(_statusQueue);
        _alarmSender = _client.CreateSender(_alarmQueue);

        _commandProcessor = _client.CreateProcessor(
            _commandQueue,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

        _commandProcessor.ProcessMessageAsync += OnCommandMessageAsync;
        _commandProcessor.ProcessErrorAsync += OnErrorAsync;

        await _commandProcessor.StartProcessingAsync();
    }

    private async Task OnCommandMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            // Filtrera på Subject (DeviceId)
            if (args.Message.Subject != _deviceId)
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            var body = args.Message.Body.ToString();
            var command = JsonSerializer.Deserialize<DeviceCommandMessage>(body);

            if (command != null && command.DeviceId == _deviceId)
            {
                CommandReceived?.Invoke(this, command);
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing command: {ex.Message}");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"ServiceBus error: {args.Exception.Message}");
        return Task.CompletedTask;
    }

    public async Task SendStatusAsync(DeviceStatusMessage status)
    {
        if (_statusSender == null)
            throw new InvalidOperationException("Client not initialized");

        status.DeviceId = _deviceId;
        status.Timestamp = DateTime.UtcNow;

        var messageBody = JsonSerializer.Serialize(status);
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json",
            Subject = _deviceId
        };

        await _statusSender.SendMessageAsync(message);
    }

    public async Task SendAlarmAsync(AlarmMessage alarm)
    {
        if (_alarmSender == null)
            throw new InvalidOperationException("Client not initialized");

        alarm.DeviceId = _deviceId;
        alarm.Timestamp = DateTime.UtcNow;

        var messageBody = JsonSerializer.Serialize(alarm);
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json",
            Subject = _deviceId
        };

        await _alarmSender.SendMessageAsync(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_commandProcessor != null)
        {
            await _commandProcessor.StopProcessingAsync();
            await _commandProcessor.DisposeAsync();
        }

        if (_statusSender != null)
        {
            await _statusSender.DisposeAsync();
        }

        if (_alarmSender != null)
        {
            await _alarmSender.DisposeAsync();
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }
    }
}
