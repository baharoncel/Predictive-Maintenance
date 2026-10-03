using System.Text;
using Backend_API.Models;

namespace Backend_API.Services;

public class ReportGeneratorService
{
    private readonly TelemetryRepository _repository;
    private readonly PhysicalValidatorService _validator;

    public ReportGeneratorService(
        TelemetryRepository repository,
        PhysicalValidatorService validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public string GenerateHtmlReport(string plantName = "ENDÜSTRİYEL ÜRETİM HATTI #1 (TÜRBİN-01)")
    {
        var records = _repository.GetLatest(100).ToList();
        int totalRecords = records.Count;
        int anomalyCount = records.Count(r => r.IsFailureRisk);
        double avgTemp = totalRecords > 0 ? records.Average(r => r.Temperature) : 0;
        double maxTemp = totalRecords > 0 ? records.Max(r => r.Temperature) : 0;
        double maxVib = totalRecords > 0 ? records.Max(r => r.Vibration) : 0;

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <title>Kestirimci Bakım & Varlık Sağlık Raporu</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; color: #1e293b; }}
        .header {{ border-bottom: 3px solid #0284c7; padding-bottom: 20px; display: flex; justify-content: space-between; align-items: center; }}
        .title {{ font-size: 24px; font-weight: 800; color: #0f172a; text-transform: uppercase; }}
        .meta {{ font-size: 13px; color: #64748b; margin-top: 5px; }}
        .iso-badge {{ background: #f0fdf4; border: 1px solid #86efac; color: #166534; padding: 6px 14px; border-radius: 6px; font-weight: bold; font-size: 12px; }}
        .kpi-row {{ display: flex; gap: 20px; margin: 30px 0; }}
        .kpi-box {{ flex: 1; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; }}
        .kpi-title {{ font-size: 11px; text-transform: uppercase; color: #64748b; font-weight: 700; }}
        .kpi-num {{ font-size: 28px; font-weight: 800; color: #0284c7; margin-top: 6px; }}
        .kpi-danger {{ color: #dc2626; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 25px; }}
        th, td {{ padding: 10px 14px; text-align: left; border-bottom: 1px solid #e2e8f0; font-size: 13px; }}
        th {{ background: #f1f5f9; color: #334155; text-transform: uppercase; font-size: 11px; font-weight: 700; }}
        .badge-risk {{ background: #fee2e2; color: #991b1b; padding: 4px 8px; border-radius: 4px; font-weight: bold; font-size: 11px; }}
        .badge-ok {{ background: #dcfce7; color: #166534; padding: 4px 8px; border-radius: 4px; font-weight: bold; font-size: 11px; }}
        .sign-row {{ display: flex; justify-content: space-between; margin-top: 60px; padding-top: 30px; border-top: 1px dashed #cbd5e1; }}
        .sign-box {{ width: 220px; text-align: center; }}
        .sign-line {{ border-bottom: 1px solid #94a3b8; height: 40px; margin-bottom: 8px; }}
        @media print {{
            body {{ margin: 15px; }}
            .no-print {{ display: none; }}
        }}
    </style>
</head>
<body>
    <div class=""no-print"" style=""margin-bottom: 20px; background: #e0f2fe; padding: 12px 20px; border-radius: 8px; display: flex; justify-content: space-between; align-items: center;"">
        <span>📄 <strong>Resmi Denetim Raporu:</strong> Bu raporu PDF olarak kaydetmek için aşağıdaki butona basın.</span>
        <button onclick=""window.print()"" style=""background: #0284c7; color: white; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-weight: bold;"">🖨️ PDF Olarak Yazdır / Kaydet</button>
    </div>

    <div class=""header"">
        <div>
            <div class=""title"">Endüstriyel Kestirimci Bakım & Varlık Sağlık Raporu</div>
            <div class=""meta"">Tesis: {plantName} | Rapor No: PRD-{DateTime.UtcNow:yyyyMMdd-HHmm} | Standart: ISO 55000 / EN 13306</div>
        </div>
        <div class=""iso-badge"">SİSTEM SAĞLIK ONAYLI</div>
    </div>

    <div class=""kpi-row"">
        <div class=""kpi-box"">
            <div class=""kpi-title"">Toplam Analiz Edilen Örnek</div>
            <div class=""kpi-num"">{totalRecords}</div>
        </div>
        <div class=""kpi-box"">
            <div class=""kpi-title"">Ortalama Çalışma Sıcaklığı</div>
            <div class=""kpi-num"">{avgTemp:F1} °C</div>
        </div>
        <div class=""kpi-box"">
            <div class=""kpi-title"">Maksimum Titreşim Genliği</div>
            <div class=""kpi-num {(maxVib > 6.0 ? "kpi-danger" : "")}"">{maxVib:F2} mm/s</div>
        </div>
        <div class=""kpi-box"">
            <div class=""kpi-title"">Tespit Edilen Arıza Riski</div>
            <div class=""kpi-num {(anomalyCount > 0 ? "kpi-danger" : "")}"">{anomalyCount}</div>
        </div>
        <div class=""kpi-box"">
            <div class=""kpi-title"">Engellenen Siber Tehdit (FDIA)</div>
            <div class=""kpi-num"">{_validator.BlockedAttacksCount}</div>
        </div>
    </div>

    <h3 style=""margin-top: 30px; font-size: 16px; color: #0f172a;"">Son Telemetri Ölçümleri & Yapay Zeka Risk Analizleri</h3>
    <table>
        <thead>
            <tr>
                <th>Zaman Damgası (UTC)</th>
                <th>Cihaz</th>
                <th>Sıcaklık (°C)</th>
                <th>Titreşim (mm/s)</th>
                <th>AI Durum / Karar</th>
                <th>Risk İhtimali</th>
                <th>Teşhis & Aksiyon</th>
            </tr>
        </thead>
        <tbody>");

        foreach (var r in records.Take(15))
        {
            string badgeClass = r.IsFailureRisk ? "badge-risk" : "badge-ok";
            string badgeText = r.IsFailureRisk ? "KRİTİK RİSK" : "NORMAL";

            sb.Append($@"
            <tr>
                <td>{r.Timestamp:HH:mm:ss.fff}</td>
                <td><strong>{r.DeviceId}</strong></td>
                <td>{r.Temperature:F1}</td>
                <td>{r.Vibration:F2}</td>
                <td><span class=""{badgeClass}"">{badgeText}</span></td>
                <td>%{r.RiskProbability * 100:F1}</td>
                <td>{r.StatusMessage}</td>
            </tr>");
        }

        sb.Append($@"
        </tbody>
    </table>

    <div class=""sign-row"">
        <div class=""sign-box"">
            <div class=""sign-line""></div>
            <strong>Vardiya Bakım Teknisyeni</strong><br>
            <span style=""font-size: 12px; color: #64748b;"">İmza / Kaşe</span>
        </div>
        <div class=""sign-box"">
            <div class=""sign-line""></div>
            <strong>Başmühendis / Tesis Müdürü</strong><br>
            <span style=""font-size: 12px; color: #64748b;"">Onay / Tarih</span>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
