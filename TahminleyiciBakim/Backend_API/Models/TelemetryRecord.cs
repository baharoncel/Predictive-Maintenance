namespace Backend_API.Models;

public class DeviceAuthRequest
{
    public string DeviceId { get; set; } = "TURBINE-EDGE-01";
    public string ClientSecret { get; set; } = "industrial-secret-edge-key-2026";
}

public class TelemetryRecord
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Vibration { get; set; }
    public bool IsFailureRisk { get; set; }
    public double RiskProbability { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
