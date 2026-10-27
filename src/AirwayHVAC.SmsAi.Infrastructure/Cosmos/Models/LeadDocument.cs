namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class LeadDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Issue { get; set; }

    public string? Address { get; set; }

    public string? PreferredTime { get; set; }

    public string Status { get; set; } = "new";

    public string Source { get; set; } = "sms";

    public string ConversationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
