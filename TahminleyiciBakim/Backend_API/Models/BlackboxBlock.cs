namespace Backend_API.Models;

public class BlackboxBlock
{
    public long SequenceNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string EventType { get; set; } = "TELEMETRY";
    public string PayloadSummary { get; set; } = string.Empty;
    public string PreviousHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public bool IsTampered { get; set; } = false;
}

public class BlackboxVerificationResult
{
    public bool IsValid { get; set; }
    public int TotalBlocks { get; set; }
    public long? BrokenBlockSequence { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string VerifiedAt { get; set; } = string.Empty;
}
