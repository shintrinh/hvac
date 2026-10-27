using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class Lead
{
    public Lead(
        string id,
        string tenantId,
        string phone,
        string? name,
        string? issue,
        string? address,
        string? preferredTime,
        LeadStatus status,
        string source,
        string conversationId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Lead id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Phone is required.", nameof(phone));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException("Conversation id is required.", nameof(conversationId));

        Id = id;
        TenantId = tenantId;
        Phone = phone;
        Name = name;
        Issue = issue;
        Address = address;
        PreferredTime = preferredTime;
        Status = status;
        Source = source;
        ConversationId = conversationId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt ?? createdAt;
    }

    public string Id { get; }

    public string TenantId { get; }

    public string Phone { get; }

    public string? Name { get; private set; }

    public string? Issue { get; private set; }

    public string? Address { get; private set; }

    public string? PreferredTime { get; private set; }

    public LeadStatus Status { get; private set; }

    public string Source { get; }

    public string ConversationId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Enrich(string? issue, string? address, string? preferredTime)
    {
        Issue = issue ?? Issue;
        Address = address ?? Address;
        PreferredTime = preferredTime ?? PreferredTime;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(LeadStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetName(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public static Lead CreateNew(string tenantId, string phone, string conversationId, DateTimeOffset now, string source = "sms")
        => new($"lead_{Guid.NewGuid():N}", tenantId, phone, null, null, null, null, LeadStatus.New, source, conversationId, now, now);
}
