using System.Text.Json.Serialization;

namespace Backend_API.Models;

public class PredictionResult
{
    [JsonPropertyName("ariza_riski")]
    public bool ArizaRiski { get; set; }

    [JsonPropertyName("failure_risk")]
    public bool FailureRisk { get; set; }

    [JsonPropertyName("risk_probability")]
    public double RiskProbability { get; set; }

    [JsonPropertyName("status_message")]
    public string StatusMessage { get; set; } = string.Empty;

    [JsonPropertyName("estimated_rul_hours")]
    public double EstimatedRulHours { get; set; } = 2400.0;

    [JsonPropertyName("degradation_percent")]
    public double DegradationPercent { get; set; } = 10.0;

    [JsonPropertyName("maintenance_urgency")]
    public string MaintenanceUrgency { get; set; } = "NORMAL";

    // Katastrofik İnfilak & Termal Kaçak (SIL-3 Standartları)
    [JsonPropertyName("explosion_risk_percent")]
    public double ExplosionRiskPercent { get; set; } = 2.0;

    [JsonPropertyName("is_thermal_runaway")]
    public bool IsThermalRunaway { get; set; } = false;

    [JsonPropertyName("time_to_detonation_seconds")]
    public int TimeToDetonationSeconds { get; set; } = 9999;

    [JsonPropertyName("sil3_emergency_stop_triggered")]
    public bool Sil3EmergencyStopTriggered { get; set; } = false;

    [JsonPropertyName("safety_action")]
    public string SafetyAction { get; set; } = "Standart İşletim";

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }
}
