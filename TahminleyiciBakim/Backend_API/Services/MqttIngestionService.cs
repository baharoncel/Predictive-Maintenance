using System.Text.Json;
using Backend_API.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend_API.Services;

public class MqttIngestionService : BackgroundService
{
    private readonly ILogger<MqttIngestionService> _logger;
    private readonly TelemetryChannel _channel;
    private readonly string? _mqttBrokerUrl;

    public MqttIngestionService(
        ILogger<MqttIngestionService> logger,
        TelemetryChannel channel)
    {
        _logger = logger;
        _channel = channel;
        _mqttBrokerUrl = Environment.GetEnvironmentVariable("MQTT_BROKER_URL");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[MqttIngestionService] Endüstriyel MQTT adaptörü başlatıldı.");

        if (string.IsNullOrWhiteSpace(_mqttBrokerUrl))
        {
            _logger.LogInformation("[MqttIngestionService] 'MQTT_BROKER_URL' tanımlanmadı. MQTT adaptörü bekleme modunda.");
            return;
        }

        _logger.LogInformation("[MqttIngestionService] MQTT Broker'a ({Broker}) bağlanılıyor ve 'factory/+/telemetry' konuları dinleniyor...", _mqttBrokerUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }

    public async Task IngestMqttPayloadAsync(string topic, string payloadJson)
    {
        try
        {
            var telemetry = JsonSerializer.Deserialize<SensorTelemetry>(payloadJson);
            if (telemetry != null)
            {
                await _channel.Writer.WriteAsync(telemetry);
                _logger.LogInformation("[MQTT Ingest] Konu: {Topic} üzerinden telemetri alındı: {Device}", topic, telemetry.DeviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MQTT Ingest] Geçersiz MQTT telemetri paketi.");
        }
    }
}
