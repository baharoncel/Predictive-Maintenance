using System.Collections.Concurrent;
using Backend_API.Models;

namespace Backend_API.Services;

public class SecurityIncident
{
    public string DeviceId { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double InjectedTemperature { get; set; }
    public double InjectedVibration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public bool IsSpoofingDetected { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
}

public class PhysicalValidatorService
{
    private readonly ConcurrentDictionary<string, (double Temp, double Vib, DateTime Time)> _deviceStates = new();
    private int _blockedAttacksCount = 0;
    private SecurityIncident? _lastIncident;

    // Endüstriyel Fiziksel Değişim Sınırları (Termal ve Dinamik Ataleti)
    private const double MaxTempDeltaPerSec = 15.0; // 1 saniyede en fazla 15°C değişim (Fiziksel sınır)
    private const double MaxVibDeltaPerSec = 7.5;   // 1 saniyede en fazla 7.5 mm/s sıçrama
    private const double AbsoluteMaxTemp = 160.0;
    private const double AbsoluteMinTemp = -10.0;

    public int BlockedAttacksCount => _blockedAttacksCount;
    public SecurityIncident? LastIncident => _lastIncident;

    public ValidationResult Validate(SensorTelemetry telemetry)
    {
        // 1. Mutlak Değer Sınırı Kontrolü (Sensör Out-of-Bounds Kontrolü)
        if (telemetry.Temperature > AbsoluteMaxTemp || telemetry.Temperature < AbsoluteMinTemp)
        {
            RecordIncident(telemetry, "MUTLAK_SINIR_IHLALI", 
                $"Sıcaklık değeri fiziksel olarak imkansız ({telemetry.Temperature}°C). Sensör manipülasyonu şüphesi.");
            return new ValidationResult
            {
                IsValid = false,
                IsSpoofingDetected = true,
                Reason = "Mutlak sıcaklık sınırı ihlali."
            };
        }

        if (telemetry.Vibration < 0 || telemetry.Vibration > 25.0)
        {
            RecordIncident(telemetry, "TITRESIM_SPEKTRUM_IHLALI", 
                $"Titreşim genliği kabul edilebilir aralık dışında ({telemetry.Vibration} mm/s).");
            return new ValidationResult
            {
                IsValid = false,
                IsSpoofingDetected = true,
                Reason = "Titreşim genlik ihlali."
            };
        }

        // 2. Termal Ataleti ve Saniyelik Değişim Hızı Kontrolü (Rate of Change / Thermal Inertia)
        if (_deviceStates.TryGetValue(telemetry.DeviceId, out var prevState))
        {
            double elapsedSeconds = Math.Max(0.5, (telemetry.Timestamp - prevState.Time).TotalSeconds);
            double deltaTemp = Math.Abs(telemetry.Temperature - prevState.Temp);
            double deltaVib = Math.Abs(telemetry.Vibration - prevState.Vib);

            double tempRate = deltaTemp / elapsedSeconds;
            double vibRate = deltaVib / elapsedSeconds;

            if (tempRate > MaxTempDeltaPerSec)
            {
                RecordIncident(telemetry, "SAHTE_TERMAL_ENJEKSIYON (FDIA)", 
                    $"1 saniyede {deltaTemp:F1}°C sıcaklık sıçraması tespit edildi (Maksimum fiziksel izin: {MaxTempDeltaPerSec}°C/s). Saldırı engellendi.");
                return new ValidationResult
                {
                    IsValid = false,
                    IsSpoofingDetected = true,
                    Reason = $"İmkansız termal sıçrama ({deltaTemp:F1}°C / sn)."
                };
            }

            if (vibRate > MaxVibDeltaPerSec)
            {
                RecordIncident(telemetry, "SAHTE_REZONANS_SPOOFING", 
                    $"1 saniyede {deltaVib:F1} mm/s titreşim darbesi tespit edildi (Maksimum fiziksel izin: {MaxVibDeltaPerSec} mm/s).");
                return new ValidationResult
                {
                    IsValid = false,
                    IsSpoofingDetected = true,
                    Reason = $"İmkansız mekanik sıçrama ({deltaVib:F1} mm/s)."
                };
            }
        }

        // Başarılı doğrulama: Son geçerli durumu güncelle
        _deviceStates[telemetry.DeviceId] = (telemetry.Temperature, telemetry.Vibration, telemetry.Timestamp);
        return new ValidationResult { IsValid = true };
    }

    private void RecordIncident(SensorTelemetry telemetry, string threatType, string description)
    {
        Interlocked.Increment(ref _blockedAttacksCount);
        _lastIncident = new SecurityIncident
        {
            DeviceId = telemetry.DeviceId,
            ThreatType = threatType,
            Description = description,
            InjectedTemperature = telemetry.Temperature,
            InjectedVibration = telemetry.Vibration,
            Timestamp = DateTime.UtcNow
        };
    }
}
