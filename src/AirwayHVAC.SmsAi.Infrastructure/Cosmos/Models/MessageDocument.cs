namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class MessageDocument
{
    public string Role { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
