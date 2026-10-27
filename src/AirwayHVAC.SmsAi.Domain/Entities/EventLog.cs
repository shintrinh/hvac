namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class EventLog
{
    public EventLog(string id, string tenantId, string type, string phone, string conversationId, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Event id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Type is required.", nameof(type));

        Id = id;
        TenantId = tenantId;
        Type = type;
        Phone = phone;
        ConversationId = conversationId;
        Timestamp = timestamp;
    }

    public string Id { get; }

    public string TenantId { get; }

    public string Type { get; }

    public string Phone { get; }

    public string ConversationId { get; }

    public DateTimeOffset Timestamp { get; }

    public static EventLog Create(string tenantId, string type, string phone, string conversationId, DateTimeOffset timestamp)
        => new($"evt_{Guid.NewGuid():N}", tenantId, type, phone, conversationId, timestamp);
}
