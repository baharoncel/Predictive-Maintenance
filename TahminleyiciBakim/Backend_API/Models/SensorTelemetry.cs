using System.Text.Json.Serialization;

namespace Backend_API.Models;

public class SensorTelemetry
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "MACHINE-UNIT-01";

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("vibration")]
    public double Vibration { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
