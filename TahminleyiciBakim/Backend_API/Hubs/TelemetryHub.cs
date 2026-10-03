using Backend_API.Models;
using Microsoft.AspNetCore.SignalR;

namespace Backend_API.Hubs;

public class TelemetryHub : Hub
{
    public async Task SendManualTelemetry(SensorTelemetry telemetry)
    {
        await Clients.All.SendAsync("NewTelemetryAvailable", telemetry);
    }
}
