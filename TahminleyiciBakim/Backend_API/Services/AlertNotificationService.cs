using System.Net.Http.Json;
using Backend_API.Models;
using Microsoft.Extensions.Logging;

namespace Backend_API.Services;

public class NotificationConfigModel
{
    public string? WebhookUrl { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }
}

public class AlertNotificationService
{
    private readonly ILogger<AlertNotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private string? _webhookUrl;
    private string? _telegramBotToken;
    private string? _telegramChatId;
    private DateTime _lastNotificationSent = DateTime.MinValue;

    public AlertNotificationService(
        ILogger<AlertNotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _webhookUrl = Environment.GetEnvironmentVariable("ALERT_WEBHOOK_URL");
        _telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        _telegramChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
    }

    public void UpdateConfig(NotificationConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(config.WebhookUrl))
            _webhookUrl = config.WebhookUrl;
        if (!string.IsNullOrWhiteSpace(config.TelegramBotToken))
            _telegramBotToken = config.TelegramBotToken;
        if (!string.IsNullOrWhiteSpace(config.TelegramChatId))
            _telegramChatId = config.TelegramChatId;

        _logger.LogInformation("[Bildirim] Kanal ayarları dinamik olarak güncellendi. Webhook: {HasWebhook}, Telegram: {HasTg}",
            !string.IsNullOrWhiteSpace(_webhookUrl), !string.IsNullOrWhiteSpace(_telegramBotToken));
    }

    public NotificationConfigModel GetConfig()
    {
        return new NotificationConfigModel
        {
            WebhookUrl = _webhookUrl,
            TelegramBotToken = string.IsNullOrWhiteSpace(_telegramBotToken) ? null : "********",
            TelegramChatId = _telegramChatId
        };
    }

    public async Task<bool> SendCriticalAlertAsync(SensorTelemetry telemetry, PredictionResult prediction, bool isTest = false)
    {
        if (!isTest && (DateTime.UtcNow - _lastNotificationSent).TotalSeconds < 10)
        {
            return false;
        }

        _lastNotificationSent = DateTime.UtcNow;

        string alertMessage = 
            $"🚨 *KRİTİK KESTİRİMCİ BAKIM UYARISI* 🚨\n" +
            $"🏭 *Tesis:* Endüstriyel Türbin Üretim Hattı #1\n" +
            $"📍 *Cihaz ID:* `{telemetry.DeviceId}`\n" +
            $"🌡️ *Sıcaklık:* `{telemetry.Temperature:F1} °C`\n" +
            $"〰️ *Titreşim:* `{telemetry.Vibration:F2} mm/s`\n" +
            $"🧠 *Arıza Riski:* `%{prediction.RiskProbability * 100:F1}`\n" +
            $"⏳ *Kalan Faydalı Ömür (RUL):* `{prediction.EstimatedRulHours:F1} Saat`\n" +
            $"📋 *Teşhis:* {prediction.StatusMessage}\n" +
            $"⏱️ *Zaman Damgası:* `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC`";

        bool dispatchedToExternal = false;

        // 1. Webhook (Slack / Discord / Teams)
        if (!string.IsNullOrWhiteSpace(_webhookUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Discord ve Slack uyumlu JSON gövdesi
                var payload = new { content = alertMessage, text = alertMessage };
                var response = await client.PostAsJsonAsync(_webhookUrl, payload);
                if (response.IsSuccessStatusCode)
                {
                    dispatchedToExternal = true;
                    _logger.LogInformation("[Bildirim] Webhook kanalına uyarı başarıyla teslim edildi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Bildirim] Webhook gönderim hatası.");
            }
        }

        // 2. Telegram Bot
        if (!string.IsNullOrWhiteSpace(_telegramBotToken) && !string.IsNullOrWhiteSpace(_telegramChatId))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                string tgUrl = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage";
                var response = await client.PostAsJsonAsync(tgUrl, new
                {
                    chat_id = _telegramChatId,
                    text = alertMessage,
                    parse_mode = "Markdown"
                });
                if (response.IsSuccessStatusCode)
                {
                    dispatchedToExternal = true;
                    _logger.LogInformation("[Bildirim] Telegram botu ile bakım ekibine mesaj teslim edildi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Bildirim] Telegram mesaj gönderim hatası.");
            }
        }

        // Konsol Günlüğü
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("[📲 BAKIM EKİBİNE OTOMATİK BİLDİRİM FIRLATILDI]");
        Console.WriteLine($"Kanal: {(dispatchedToExternal ? "Dış Servis (Webhook / Telegram)" : "Sistem Konsolu & Web Push")}");
        Console.WriteLine($"Hedef Cihaz: {telemetry.DeviceId} | RUL: {prediction.EstimatedRulHours:F1} Saat");
        Console.WriteLine($"Aciliyet: {prediction.MaintenanceUrgency} | Teşhis: {prediction.StatusMessage}");
        Console.WriteLine("=======================================================\n");
        Console.ResetColor();

        return true;
    }
}
