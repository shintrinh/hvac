namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class VoiceSessionDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string Provider { get; set; } = "twilio";

    public string ProviderCallId { get; set; } = string.Empty;

    public string FromPhone { get; set; } = string.Empty;

    public string ToPhone { get; set; } = string.Empty;

    public string Status { get; set; } = "in-progress";

    public string CurrentStage { get; set; } = "Issue";

    public int TurnCount { get; set; }

    public int SilenceTimeoutCount { get; set; }

    public string? HandoffReason { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
}
