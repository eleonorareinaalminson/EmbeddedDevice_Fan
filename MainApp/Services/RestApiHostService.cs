// MainApp/Services/RestApiHostService.cs
using MainApp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace MainApp.Services;

public class RestApiHostService
{
    private WebApplication? _app;
    private Task? _runTask;
    private readonly string _url;

    public RestApiHostService(string url = "http://localhost:5001")
    {
        _url = url;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Konfigurera URL
        builder.WebHost.UseUrls(_url);

        // Bygg app
        _app = builder.Build();

        // ========================================
        // API ENDPOINTS
        // ========================================

        // GET /api/health - Health check
        _app.MapGet("/api/health", () =>
        {
            return Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "MainApp REST API"
            });
        });

        // GET /api/status - Hämta enhetsstatus
        _app.MapGet("/api/status", () =>
        {
            if (DeviceState.GetStatusFunc == null)
                return Results.Problem("Status function not configured");

            var status = DeviceState.GetStatusFunc();
            return Results.Ok(status);
        });

        // POST /api/command - Ta emot kommando
        _app.MapPost("/api/command", async (HttpContext context) =>
        {
            try
            {
                var command = await context.Request.ReadFromJsonAsync<CommandRequest>();

                if (command == null)
                    return Results.BadRequest(new { error = "Invalid command format" });

                if (DeviceState.HandleCommandAction == null)
                    return Results.Problem("Command handler not configured");

                DeviceState.HandleCommandAction(command);

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

        // Starta servern i bakgrunden
        _runTask = _app.RunAsync();

        await Task.Delay(500); // Ge servern tid att starta
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
