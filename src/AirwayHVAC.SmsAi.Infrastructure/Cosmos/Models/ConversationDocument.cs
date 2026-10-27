namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class ConversationDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? LeadId { get; set; }

    public string Status { get; set; } = "active";

    public string Channel { get; set; } = "sms";

    public string Stage { get; set; } = "Issue";

    public string? Issue { get; set; }

    public string? Address { get; set; }

    public string? Urgency { get; set; }

    public string? PreferredTime { get; set; }

    public bool? IsConfirmed { get; set; }

    public List<MessageDocument> Messages { get; set; } = [];

    public DateTimeOffset LastMessageAt { get; set; }
}
