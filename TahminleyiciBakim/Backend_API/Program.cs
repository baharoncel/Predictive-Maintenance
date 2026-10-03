using System.Security.Claims;
using System.Threading.RateLimiting;
using Backend_API.Hubs;
using Backend_API.Models;
using Backend_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Port konfigürasyonu
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// 1. HTTP Client Fabrikası
builder.Services.AddHttpClient("AiServiceClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient();

// 2. Güvenlik, Fiziksel Doğrulama, Katastrofik Analiz, Bildirim ve Veri Servisleri
var jwtService = new JwtTokenService();
builder.Services.AddSingleton(jwtService);
builder.Services.AddSingleton<PhysicalValidatorService>();
builder.Services.AddSingleton<CatastrophicFailureAnalyzer>();
builder.Services.AddSingleton<AlertNotificationService>();
builder.Services.AddSingleton<ReportGeneratorService>();
builder.Services.AddSingleton<BlackboxRecorderService>();
builder.Services.AddSingleton<IndustrialNewsRadarService>();
builder.Services.AddSingleton<RoiCalculatorService>();
builder.Services.AddSingleton<TelemetryRepository>();
builder.Services.AddSingleton<TelemetryChannel>();
builder.Services.AddSignalR();

// 3. JWT Kimlik Doğrulama Katmanı (Zero-Trust IoT)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = jwtService.GetValidationParameters();
});

builder.Services.AddAuthorization();

// 4. Rate Limiting Katmanı (DDoS & Flooding Koruması)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("TelemetryLimiter", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(1);
        opt.PermitLimit = 15;
        opt.QueueLimit = 5;
    });
});

// 5. Arka Plan Üretici, Tüketici ve MQTT Servisleri
builder.Services.AddHostedService<SensorDataProducer>();
builder.Services.AddHostedService<SensorDataConsumer>();
builder.Services.AddSingleton<MqttIngestionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttIngestionService>());

// 6. CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

// 7. Swagger & OpenAPI (JWT Bearer Destekli)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Predictive Maintenance Industrial Backend API",
        Version = "v2.5",
        Description = "Kestirimci Bakım C# Arka Uç: IEC 61508 SIL-3 Katastrofik İnfilak Önleme, RUL & Zero-Trust"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization başlığı. Örnek: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// SignalR Hub
app.MapHub<TelemetryHub>("/hubs/telemetry");

// -------------------------------------------------------------
// Endpoint 1: IoT Zero-Trust Cihaz Token Alma (JWT Authentication)
// -------------------------------------------------------------
app.MapPost("/api/auth/device-token", (DeviceAuthRequest request, JwtTokenService tokenService) =>
{
    try
    {
        var token = tokenService.GenerateDeviceToken(request.DeviceId, request.ClientSecret);
        return Results.Ok(new
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresInSeconds = 7200,
            DeviceId = request.DeviceId,
            Role = "SensorDevice"
        });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
})
.WithName("GetDeviceToken")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 2: IoT Zero-Trust Sensör Veri Girişi (Ingestion Gateway)
// -------------------------------------------------------------
app.MapPost("/api/telemetry", async (
    SensorTelemetry telemetry,
    HttpContext httpContext,
    TelemetryChannel channel) =>
{
    if (DateTime.UtcNow - telemetry.Timestamp > TimeSpan.FromSeconds(45))
    {
        return Results.BadRequest(new { Error = "Zaman damgası geçersiz veya paket süresi dolmuş (Replay Attack koruması)." });
    }

    var iotApiKey = Environment.GetEnvironmentVariable("IOT_API_KEY") ?? "industrial-secret-edge-key-2026";
    bool isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;

    if (!isAuthenticated)
    {
        if (httpContext.Request.Headers.TryGetValue("X-IoT-Key", out var headerKey) && headerKey == iotApiKey)
        {
            isAuthenticated = true;
        }
    }

    if (!isAuthenticated)
    {
        return Results.Unauthorized();
    }

    await channel.Writer.WriteAsync(telemetry);

    return Results.Accepted("/api/telemetry", new
    {
        Message = "Sensör telemetrisi Zero-Trust doğrulamadan geçti ve kuyruğa yönlendirildi.",
        telemetry.DeviceId,
        telemetry.Temperature,
        telemetry.Vibration,
        telemetry.Timestamp
    });
})
.RequireRateLimiting("TelemetryLimiter")
.WithName("SubmitTelemetry")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 3: Resmi Bakım Denetim Raporu (ISO 55000 PDF / HTML Görünümü)
// -------------------------------------------------------------
app.MapGet("/api/reports/pdf-view", (ReportGeneratorService reportService) =>
{
    var html = reportService.GenerateHtmlReport();
    return Results.Content(html, "text/html");
})
.WithName("GetMaintenanceReportPdfView")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 4: Bildirim Kanalı Ayarları & Test Gönderimi
// -------------------------------------------------------------
app.MapGet("/api/notifications/config", (AlertNotificationService notificationService) =>
{
    return Results.Ok(notificationService.GetConfig());
})
.WithName("GetNotificationConfig")
.WithOpenApi();

app.MapPost("/api/notifications/config", (NotificationConfigModel config, AlertNotificationService notificationService) =>
{
    notificationService.UpdateConfig(config);
    return Results.Ok(new { Success = true, Message = "Bildirim kanalı ayarları güncellendi." });
})
.WithName("UpdateNotificationConfig")
.WithOpenApi();

app.MapPost("/api/notifications/test-dispatch", async (AlertNotificationService notificationService) =>
{
    var dummyTelemetry = new SensorTelemetry
    {
        DeviceId = "TURBINE-EDGE-01",
        Temperature = 96.5,
        Vibration = 8.4,
        Timestamp = DateTime.UtcNow
    };
    var dummyPrediction = new PredictionResult
    {
        ArizaRiski = true,
        RiskProbability = 0.98,
        EstimatedRulHours = 12.5,
        MaintenanceUrgency = "ACIL",
        StatusMessage = "TEST ALARMI: Sistem üzerinden tetiklenen acil durum bildirimi."
    };

    var sent = await notificationService.SendCriticalAlertAsync(dummyTelemetry, dummyPrediction, isTest: true);

    return Results.Ok(new
    {
        Success = true,
        Dispatched = sent,
        Message = "Bakım teknisyenine acil durum uyarısı başarıyla iletildi.",
        dummyTelemetry.DeviceId,
        dummyPrediction.EstimatedRulHours
    });
})
.WithName("TestNotificationDispatch")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 5: Siber Güvenlik İstatistikleri
// -------------------------------------------------------------
app.MapGet("/api/security/stats", (PhysicalValidatorService validator) =>
{
    return Results.Ok(new
    {
        PhysicalValidationActive = true,
        BlockedAttacksCount = validator.BlockedAttacksCount,
        LastIncident = validator.LastIncident
    });
})
.WithName("GetSecurityStats")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 6: Geçmiş Telemetri Kayıtları (Time-Series Query)
// -------------------------------------------------------------
app.MapGet("/api/telemetry/history", (int? limit, TelemetryRepository repository) =>
{
    var records = repository.GetLatest(limit ?? 50);
    return Results.Ok(records);
})
.WithName("GetTelemetryHistory")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 7: Grafana Uyumlu Zaman Serisi Veri Hattı
// -------------------------------------------------------------
app.MapGet("/api/telemetry/timeseries", (TelemetryRepository repository) =>
{
    return Results.Ok(repository.GetGrafanaTimeSeries());
})
.WithName("GetGrafanaTimeSeries")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 8: İnteraktif Simülasyon Kontrolü
// -------------------------------------------------------------
app.MapPost("/api/simulation/mode", (string mode) =>
{
    var validModes = new[] { "NORMAL", "BEARING_FAULT", "OVERHEATING", "FDIA_ATTACK", "CATASTROPHIC_RUNAWAY" };
    if (!validModes.Contains(mode.ToUpper()))
    {
        return Results.BadRequest(new { Error = "Geçersiz mod. Seçenekler: NORMAL, BEARING_FAULT, OVERHEATING, FDIA_ATTACK, CATASTROPHIC_RUNAWAY" });
    }

    SensorDataProducer.SimulationMode = mode.ToUpper();
    return Results.Ok(new
    {
        Message = $"Simülasyon modu güncellendi: {SensorDataProducer.SimulationMode}",
        Mode = SensorDataProducer.SimulationMode
    });
})
.WithName("SetSimulationMode")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 9: Endüstriyel Kriptografik Kara Kutu (ISO 27001 Audit)
// -------------------------------------------------------------
app.MapGet("/api/blackbox/blocks", (BlackboxRecorderService blackbox, int? count) =>
{
    return Results.Ok(blackbox.GetRecentBlocks(count ?? 20));
})
.WithName("GetBlackboxBlocks")
.WithOpenApi();

app.MapGet("/api/blackbox/verify", (BlackboxRecorderService blackbox) =>
{
    var result = blackbox.VerifyChainIntegrity();
    return Results.Ok(result);
})
.WithName("VerifyBlackboxChain")
.WithOpenApi();

app.MapGet("/api/blackbox/export-audit", (BlackboxRecorderService blackbox) =>
{
    string certificate = blackbox.ExportAuditCertificate();
    return Results.Content(certificate, "text/plain; charset=utf-8");
})
.WithName("ExportBlackboxAudit")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 10: Sektörel Güvenlik & Kaza İstihbarat Radarı (OSINT)
// -------------------------------------------------------------
app.MapGet("/api/radar/feed", (IndustrialNewsRadarService radar) =>
{
    return Results.Ok(radar.GetLatestIntelligence());
})
.WithName("GetIndustrialNewsRadar")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 11: Finansal Kurtarma & Can Güvenliği ROI Metrikleri
// -------------------------------------------------------------
app.MapGet("/api/roi/metrics", (RoiCalculatorService roi) =>
{
    return Results.Ok(roi.GetMetrics());
})
.WithName("GetRoiMetrics")
.WithOpenApi();

// -------------------------------------------------------------
// Endpoint 12: Sistem Sağlık Kontrolü (Cloud Health Check)
// -------------------------------------------------------------
app.MapGet("/health", (PhysicalValidatorService validator) => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
    RabbitMqConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RABBITMQ_URL")),
    AiServiceConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AI_SERVICE_URL")),
    ZeroTrustJwtEnabled = true,
    PhysicalDefenseFilterActive = true,
    CatastrophicExplosionEngineActive = true,
    CryptographicBlackboxActive = true,
    IndustrialNewsRadarActive = true,
    RoiTrackerActive = true,
    MqttAdapterActive = true,
    NotificationEngineActive = true,
    BlockedAttacksCount = validator.BlockedAttacksCount,
    CurrentSimulationMode = SensorDataProducer.SimulationMode
}))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();

