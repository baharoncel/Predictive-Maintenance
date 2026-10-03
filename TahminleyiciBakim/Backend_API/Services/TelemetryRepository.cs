using System.Collections.Concurrent;
using Backend_API.Models;

namespace Backend_API.Services;

public class TelemetryRepository
{
    private readonly ConcurrentQueue<TelemetryRecord> _records = new();
    private const int MaxCapacity = 500;
    private long _idCounter = 0;

    public void Add(SensorTelemetry telemetry, PredictionResult prediction)
    {
        var record = new TelemetryRecord
        {
            Id = Interlocked.Increment(ref _idCounter),
            DeviceId = telemetry.DeviceId,
            Temperature = telemetry.Temperature,
            Vibration = telemetry.Vibration,
            IsFailureRisk = prediction.ArizaRiski || prediction.FailureRisk,
            RiskProbability = prediction.RiskProbability,
            StatusMessage = prediction.StatusMessage,
            Timestamp = telemetry.Timestamp
        };

        _records.Enqueue(record);

        while (_records.Count > MaxCapacity && _records.TryDequeue(out _))
        {
            // Ring buffer mantığı: En eski kayıtları düşür
        }
    }

    public IEnumerable<TelemetryRecord> GetLatest(int limit = 50)
    {
        return _records.Reverse().Take(limit);
    }

    public object GetGrafanaTimeSeries()
    {
        var list = _records.ToList();
        return new
        {
            temperature = list.Select(r => new { time = r.Timestamp, value = r.Temperature }),
            vibration = list.Select(r => new { time = r.Timestamp, value = r.Vibration }),
            risk = list.Select(r => new { time = r.Timestamp, value = r.RiskProbability * 100 }),
            anomalies = list.Where(r => r.IsFailureRisk).Select(r => new { time = r.Timestamp, cause = r.StatusMessage })
        };
    }
}
