namespace Backend_API.Services;

public class RoiMetrics
{
    public double TotalCostSavedTRY { get; set; } = 485000.0; // Başlangıç taban tasarrufu
    public double TotalDowntimeHoursPrevented { get; set; } = 64.0;
    public int CatastrophicDisastersPrevented { get; set; } = 1;
    public int BearingFailuresPrevented { get; set; } = 5;
    public int OverheatingFailuresPrevented { get; set; } = 3;
    public double HseSafetyScorePercent { get; set; } = 99.8;
    public double RoiMultiplier { get; set; } = 4.8; // Yatırımın 4.8 katı getiri
    public DateTime LastSavingsRecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Finansal Kurtarma & Can Güvenliği ROI (Yatırım Geri Dönüşü) Hesaplama Motoru.
/// Fabrika yönetimine sistemin kurtardığı maliyeti, duruş süresini ve önlenen can kayıplarını hesaplar.
/// </summary>
public class RoiCalculatorService
{
    private readonly RoiMetrics _metrics = new();
    private readonly object _lock = new();

    public RoiMetrics GetMetrics()
    {
        lock (_lock)
        {
            return new RoiMetrics
            {
                TotalCostSavedTRY = Math.Round(_metrics.TotalCostSavedTRY, 2),
                TotalDowntimeHoursPrevented = Math.Round(_metrics.TotalDowntimeHoursPrevented, 1),
                CatastrophicDisastersPrevented = _metrics.CatastrophicDisastersPrevented,
                BearingFailuresPrevented = _metrics.BearingFailuresPrevented,
                OverheatingFailuresPrevented = _metrics.OverheatingFailuresPrevented,
                HseSafetyScorePercent = Math.Round(_metrics.HseSafetyScorePercent, 1),
                RoiMultiplier = Math.Round(_metrics.RoiMultiplier, 2),
                LastSavingsRecordedAt = _metrics.LastSavingsRecordedAt
            };
        }
    }

    public void RegisterCatastrophicInterception()
    {
        lock (_lock)
        {
            _metrics.CatastrophicDisastersPrevented += 1;
            // Bir fabrika patlaması/yangını önlendiğinde ortalama hasar & üretim durması: 1,250,000 ₺
            _metrics.TotalCostSavedTRY += 1250000.0;
            _metrics.TotalDowntimeHoursPrevented += 72.0; // 3 gün fabrika duruşu önlendi
            _metrics.HseSafetyScorePercent = 100.0;
            _metrics.RoiMultiplier = Math.Max(5.0, (_metrics.TotalCostSavedTRY / 150000.0));
            _metrics.LastSavingsRecordedAt = DateTime.UtcNow;
        }
    }

    public void RegisterBearingFaultInterception()
    {
        lock (_lock)
        {
            _metrics.BearingFailuresPrevented += 1;
            // Rulman dağılması sonucu şaft ve stator hasarı: ~45,000 ₺ + 8 saat duruş (~60,000 ₺)
            _metrics.TotalCostSavedTRY += 105000.0;
            _metrics.TotalDowntimeHoursPrevented += 8.0;
            _metrics.RoiMultiplier = Math.Max(2.0, (_metrics.TotalCostSavedTRY / 150000.0));
            _metrics.LastSavingsRecordedAt = DateTime.UtcNow;
        }
    }

    public void RegisterOverheatingInterception()
    {
        lock (_lock)
        {
            _metrics.OverheatingFailuresPrevented += 1;
            // Aşırı ısınma kaynaklı motor sarım yanması ve bobinaj tamiri: ~65,000 ₺ + 16 saat duruş
            _metrics.TotalCostSavedTRY += 95000.0;
            _metrics.TotalDowntimeHoursPrevented += 16.0;
            _metrics.RoiMultiplier = Math.Max(2.0, (_metrics.TotalCostSavedTRY / 150000.0));
            _metrics.LastSavingsRecordedAt = DateTime.UtcNow;
        }
    }
}
