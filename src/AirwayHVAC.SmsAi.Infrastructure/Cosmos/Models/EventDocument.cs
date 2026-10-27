namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class EventDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
