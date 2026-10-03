using System.Collections.Concurrent;
using Backend_API.Models;

namespace Backend_API.Services;

public class CatastrophicAssessment
{
    public double ExplosionRiskPercent { get; set; }
    public bool IsThermalRunaway { get; set; }
    public int TimeToDetonationSeconds { get; set; }
    public bool Sil3EmergencyStopTriggered { get; set; }
    public string DisasterDiagnosis { get; set; } = string.Empty;
    public string SafetyAction { get; set; } = "Standart İşletim";
}

public class CatastrophicFailureAnalyzer
{
    // Cihaz bazında son 8 telemetri örneğini tutar (Türev hesaplaması için)
    private readonly ConcurrentDictionary<string, List<(double Temp, double Vib, DateTime Time)>> _history = new();
    private const double FlashpointBurstTemp = 150.0; // Yağ buharı infilak sıcaklığı

    public CatastrophicAssessment Analyze(SensorTelemetry telemetry)
    {
        var list = _history.GetOrAdd(telemetry.DeviceId, _ => new List<(double, double, DateTime)>());
        
        lock (list)
        {
            list.Add((telemetry.Temperature, telemetry.Vibration, telemetry.Timestamp));
            if (list.Count > 10)
            {
                list.RemoveAt(0);
            }

            if (list.Count < 3)
            {
                return new CatastrophicAssessment
                {
                    ExplosionRiskPercent = 2.0,
                    IsThermalRunaway = false,
                    TimeToDetonationSeconds = 9999,
                    Sil3EmergencyStopTriggered = false,
                    DisasterDiagnosis = "Sistem Kararlı (Nominal Termal Denge)",
                    SafetyAction = "Gözlem Devam Ediyor"
                };
            }

            // 1. Isı Değişim Hızları ve İkinci Türev (İvmelenme)
            int n = list.Count;
            var p0 = list[n - 3];
            var p1 = list[n - 2];
            var p2 = list[n - 1];

            double dt1 = Math.Max(0.5, (p1.Time - p0.Time).TotalSeconds);
            double dt2 = Math.Max(0.5, (p2.Time - p1.Time).TotalSeconds);

            double v1 = (p1.Temp - p0.Temp) / dt1; // 1. hız (°C/s)
            double v2 = (p2.Temp - p1.Temp) / dt2; // 2. hız (°C/s)

            double acceleration = (v2 - v1) / dt2; // 2. Türev (°C/s²)

            // 2. Termal Kaçak (Thermal Runaway) Kriteri:
            // Sıcaklık 85°C üzerindeyken ısı artış ivmesi pozitifse (soğutma çökmüş ve ısı katlanarak artıyorsa)
            bool isRunaway = telemetry.Temperature > 85.0 && acceleration > 0.15 && v2 > 0.5;
            bool isDestructiveResonance = telemetry.Vibration > 10.0;

            double explosionRisk = 5.0;

            if (isRunaway && isDestructiveResonance)
            {
                explosionRisk = 96.0;
            }
            else if (isRunaway)
            {
                explosionRisk = Math.Min(95.0, 60.0 + (telemetry.Temperature - 85.0) * 1.2);
            }
            else if (telemetry.Temperature > 105.0)
            {
                explosionRisk = Math.Min(88.0, 45.0 + (telemetry.Temperature - 100.0) * 2.0);
            }
            else if (isDestructiveResonance)
            {
                explosionRisk = Math.Min(85.0, 50.0 + (telemetry.Vibration - 10.0) * 3.5);
            }

            // 3. Patlamaya / İnfilaka Kalan Süre Tahmini (Time-to-Detonation)
            int timeToDetonation = 9999;
            if (explosionRisk > 50.0 && v2 > 0.1)
            {
                double tempDiff = Math.Max(1.0, FlashpointBurstTemp - telemetry.Temperature);
                timeToDetonation = (int)Math.Max(15.0, Math.Min(360.0, tempDiff / v2));
            }

            // 4. IEC 61508 SIL-3 Acil Kapatma (Safety Instrumented System)
            bool sil3Triggered = explosionRisk >= 80.0 || (telemetry.Temperature > 115.0 && telemetry.Vibration > 9.0);

            string diagnosis;
            string action;

            if (sil3Triggered)
            {
                diagnosis = "🚨 KATASTROFİK TEHLİKE: Termal Kaçak (Thermal Runaway) & Rezonans İnfilak Eşiğinde!";
                action = "⚡ SIL-3 ACİL KAPATMA (E-STOP) AKTİF: Soğutma Valfi %100 Açıldı & Güç Kesildi!";
            }
            else if (explosionRisk > 40.0)
            {
                diagnosis = "⚠️ YÜKSEK RİSK: Termodinamik Isı Birikimi (Pozitif Termal İvme)";
                action = "Motor yükünü %50 azaltın ve ikincil soğutma pompasını devreye alın.";
            }
            else
            {
                diagnosis = "DENGELİ: Isı transfer katsayısı nominal işletim sınırlarında.";
                action = "Rutin proses kontrolü.";
            }

            return new CatastrophicAssessment
            {
                ExplosionRiskPercent = Math.Round(explosionRisk, 1),
                IsThermalRunaway = isRunaway,
                TimeToDetonationSeconds = timeToDetonation,
                Sil3EmergencyStopTriggered = sil3Triggered,
                DisasterDiagnosis = diagnosis,
                SafetyAction = action
            };
        }
    }
}
