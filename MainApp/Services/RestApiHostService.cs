using MainApp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace MainApp.Services;

public class RestApiHostService
{
    private WebApplication? _app;
    private Task? _runTask;
    private readonly string _url;
    private readonly IDeviceStateProvider _stateProvider;

    public RestApiHostService(string url, IDeviceStateProvider stateProvider)
    {
        _url = url;
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(_url);
        _app = builder.Build();

        _app.MapGet("/api/health", () =>
        {
            return Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "MainApp REST API"
            });
        });

        _app.MapGet("/api/status", () =>
        {
            var status = _stateProvider.GetStatus();
            return Results.Ok(status);
        });

        _app.MapPost("/api/command", async (HttpContext context) =>
        {
            try
            {
                var command = await context.Request.ReadFromJsonAsync<CommandRequest>();

                if (command == null)
                    return Results.BadRequest(new { error = "Invalid command format" });

                _stateProvider.HandleCommand(command);

                return Results.Ok(new
                {
                    message = "Command received",
                    action = command.Action,
                    deviceId = command.DeviceId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        _runTask = _app.RunAsync();
        await Task.Delay(500);
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_runTask != null)
        {
            await _runTask;
        }
    }
}