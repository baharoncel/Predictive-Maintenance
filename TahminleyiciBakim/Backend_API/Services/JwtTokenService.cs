using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Backend_API.Services;

public class JwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer = "PredictiveMaintenanceCloud";
    private readonly string _audience = "IndustrialSensors";

    public JwtTokenService()
    {
        // Ortam değişkeninden oku veya güvenli varsayılan anahtar kullan
        _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                     ?? "ZeroTrust_IndustrialPredictiveMaintenance_SecureKey_2026_xYz987!";
    }

    public string GenerateDeviceToken(string deviceId, string clientSecret)
    {
        // Basit doğrulaması: Cihaza atanmış gizli anahtarı doğrula
        var validKey = Environment.GetEnvironmentVariable("IOT_API_KEY") ?? "industrial-secret-edge-key-2026";
        if (clientSecret != validKey)
        {
            throw new UnauthorizedAccessException("Geçersiz cihaz gizli anahtarı (ClientSecret).");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, deviceId),
                new Claim("DeviceId", deviceId),
                new Claim(ClaimTypes.Role, "SensorDevice"),
                new Claim("Scope", "telemetry:write")
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public TokenValidationParameters GetValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_secretKey);
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    }
}
