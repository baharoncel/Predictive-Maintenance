using System.Text;
using System.Text.Json;
using Backend_API.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Backend_API.Services;

public class SensorDataProducer : BackgroundService
{
    private readonly ILogger<SensorDataProducer> _logger;
    private readonly TelemetryChannel _channel;
    private readonly string? _rabbitMqUrl;
    private const string QueueName = "sensor_data_queue";
    private readonly Random _random = new();

    // Simülasyon modları: NORMAL, BEARING_FAULT, OVERHEATING, FDIA_ATTACK, CATASTROPHIC_RUNAWAY
    public static volatile string SimulationMode = "NORMAL";
    private static double _runawayTemp = 88.0;

    public SensorDataProducer(ILogger<SensorDataProducer> logger, TelemetryChannel channel)
    {
        _logger = logger;
        _channel = channel;
        _rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SensorDataProducer] Arka plan üretici servisi başlatıldı.");

        if (string.IsNullOrWhiteSpace(_rabbitMqUrl))
        {
            _logger.LogInformation("[SensorDataProducer] 'RABBITMQ_URL' tanımlı değil. Yerel kanal (In-Memory Channel) modu devrede.");
            await RunLocalModeAsync(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_rabbitMqUrl),
                    AutomaticRecoveryEnabled = true
                };

                _logger.LogInformation("[SensorDataProducer] RabbitMQ sunucusuna bağlanılıyor...");
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var rabbitChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await rabbitChannel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var telemetry = GenerateTelemetry();
                    string json = JsonSerializer.Serialize(telemetry);
                    byte[] body = Encoding.UTF8.GetBytes(json);

                    await rabbitChannel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: QueueName,
                        body: body,
                        cancellationToken: stoppingToken);

                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SensorDataProducer] RabbitMQ bağlantı hatası. 5 saniye sonra yeniden denenecek...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("[SensorDataProducer] Arka plan servisi durduruldu.");
    }

    private async Task RunLocalModeAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var telemetry = GenerateTelemetry();
                await _channel.Writer.WriteAsync(telemetry, stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SensorDataProducer] Yerel kuyruk üretimi sırasında hata oluştu.");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private SensorTelemetry GenerateTelemetry()
    {
        double temperature;
        double vibration;

        if (SimulationMode == "CATASTROPHIC_RUNAWAY")
        {
            // Katastrofik Termal Kaçak: Isı her saniye katlanarak artar (Pozitif İvme d²T/dt² > 0)
            _runawayTemp += (_random.NextDouble() * 1.5 + 2.5); // Her saniye 2.5 - 4.0 °C fırlar
            if (_runawayTemp > 145.0) _runawayTemp = 95.0; // Döngüsel test için reset
            temperature = Math.Round(_runawayTemp, 2);
            vibration = Math.Round(_random.NextDouble() * 3.5 + 10.5, 2); // 10.5 - 14.0 mm/s yıkıcı rezonans
        }
        else if (SimulationMode == "FDIA_ATTACK")
        {
            _runawayTemp = 88.0;
            temperature = Math.Round(_random.NextDouble() * 10.0 + 135.0, 2);
            vibration = Math.Round(_random.NextDouble() * 5.0 + 18.0, 2);
        }
        else if (SimulationMode == "BEARING_FAULT")
        {
            _runawayTemp = 88.0;
            temperature = Math.Round(_random.NextDouble() * 15.0 + 65.0, 2);
            vibration = Math.Round(_random.NextDouble() * 4.0 + 8.5, 2);
        }
        else if (SimulationMode == "OVERHEATING")
        {
            _runawayTemp = 88.0;
            temperature = Math.Round(_random.NextDouble() * 25.0 + 92.0, 2);
            vibration = Math.Round(_random.NextDouble() * 3.0 + 3.5, 2);
        }
        else
        {
            _runawayTemp = 88.0;
            bool isAnomaly = _random.NextDouble() < 0.12;
            temperature = isAnomaly 
                ? Math.Round(_random.NextDouble() * 25.0 + 85.0, 2)
                : Math.Round(_random.NextDouble() * 40.0 + 35.0, 2);

            vibration = isAnomaly
                ? Math.Round(_random.NextDouble() * 6.0 + 6.0, 2)
                : Math.Round(_random.NextDouble() * 3.5 + 1.0, 2);
        }

        return new SensorTelemetry
        {
            DeviceId = "TURBINE-EDGE-01",
            Temperature = temperature,
            Vibration = vibration,
            Timestamp = DateTime.UtcNow
        };
    }
}
