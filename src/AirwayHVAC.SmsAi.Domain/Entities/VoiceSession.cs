using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class VoiceSession
{
    public VoiceSession(
        string id,
        string tenantId,
        string conversationId,
        string provider,
        string providerCallId,
        string fromPhone,
        string toPhone,
        string status,
        ConversationStage currentStage,
        int turnCount,
        int silenceTimeoutCount,
        string? handoffReason,
        DateTimeOffset startedAt,
        DateTimeOffset lastActivityAt,
        DateTimeOffset? endedAt = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Voice session id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(providerCallId)) throw new ArgumentException("Provider call id is required.", nameof(providerCallId));
        if (string.IsNullOrWhiteSpace(fromPhone)) throw new ArgumentException("From phone is required.", nameof(fromPhone));
        if (string.IsNullOrWhiteSpace(toPhone)) throw new ArgumentException("To phone is required.", nameof(toPhone));

        Id = id;
        TenantId = tenantId;
        ConversationId = conversationId;
        Provider = provider;
        ProviderCallId = providerCallId;
        FromPhone = fromPhone;
        ToPhone = toPhone;
        Status = string.IsNullOrWhiteSpace(status) ? "in-progress" : status;
        CurrentStage = currentStage;
        TurnCount = turnCount;
        SilenceTimeoutCount = silenceTimeoutCount;
        HandoffReason = handoffReason;
        StartedAt = startedAt;
        LastActivityAt = lastActivityAt;
        EndedAt = endedAt;
    }

    public string Id { get; }

    public string TenantId { get; }

    public string ConversationId { get; }

    public string Provider { get; }

    public string ProviderCallId { get; }

    public string FromPhone { get; }

    public string ToPhone { get; }

    public string Status { get; private set; }

    public ConversationStage CurrentStage { get; private set; }

    public int TurnCount { get; private set; }

    public int SilenceTimeoutCount { get; private set; }

    public string? HandoffReason { get; private set; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public void RecordTurn(ConversationStage currentStage, DateTimeOffset timestamp)
    {
        TurnCount++;
        CurrentStage = currentStage;
        LastActivityAt = timestamp;
        Status = "in-progress";
    }

    public void RecordSilence(DateTimeOffset timestamp)
    {
        SilenceTimeoutCount++;
        LastActivityAt = timestamp;
    }

    public void RequestHandoff(string reason, DateTimeOffset timestamp)
    {
        HandoffReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        Status = "handoff";
        LastActivityAt = timestamp;
    }

    public void Complete(string status, DateTimeOffset timestamp)
    {
        Status = string.IsNullOrWhiteSpace(status) ? "completed" : status.Trim();
        LastActivityAt = timestamp;
        EndedAt = timestamp;
    }

    public static VoiceSession CreateNew(string tenantId, string conversationId, string providerCallId, string fromPhone, string toPhone, DateTimeOffset now)
        => new($"voice_{NormalizeProviderCallId(providerCallId)}", tenantId, conversationId, "twilio", providerCallId, fromPhone, toPhone, "in-progress", ConversationStage.Issue, 0, 0, null, now, now);

    private static string NormalizeProviderCallId(string providerCallId)
        => new(providerCallId.Where(char.IsLetterOrDigit).ToArray());
}
