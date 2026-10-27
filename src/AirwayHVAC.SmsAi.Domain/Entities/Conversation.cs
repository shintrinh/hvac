using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class Conversation
{
    private const int MaxMessages = 20;
    private readonly List<Message> _messages;

    public Conversation(
        string id,
        string tenantId,
        string phone,
        string? leadId,
        string status,
        ConversationStage stage,
        string? issue,
        string? address,
        string? urgency,
        string? preferredTime,
        bool? isConfirmed,
        IEnumerable<Message>? messages,
        DateTimeOffset lastMessageAt,
        ConversationChannel channel = ConversationChannel.Sms)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Conversation id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Phone is required.", nameof(phone));

        Id = id;
        TenantId = tenantId;
        Phone = phone;
        LeadId = leadId;
        Status = string.IsNullOrWhiteSpace(status) ? "active" : status;
        Stage = stage;
        Issue = issue;
        Address = address;
        Urgency = urgency;
        PreferredTime = preferredTime;
        IsConfirmed = isConfirmed;
        _messages = messages?.TakeLast(MaxMessages).ToList() ?? [];
        LastMessageAt = lastMessageAt;
        Channel = channel;
    }

    public string Id { get; }

    public string TenantId { get; }

    public string Phone { get; }

    public string? LeadId { get; private set; }

    public string Status { get; private set; }

    public ConversationStage Stage { get; private set; }

    public string? Issue { get; private set; }

    public string? Address { get; private set; }

    public string? Urgency { get; private set; }

    public string? PreferredTime { get; private set; }

    public bool? IsConfirmed { get; private set; }

    public IReadOnlyList<Message> Messages => _messages;

    public DateTimeOffset LastMessageAt { get; private set; }

    public ConversationChannel Channel { get; }

    public void AppendCustomerMessage(string text, DateTimeOffset timestamp) => AppendMessage("customer", text, timestamp);

    public void AppendAiMessage(string text, DateTimeOffset timestamp) => AppendMessage("ai", text, timestamp);

    public void AttachLead(string leadId)
    {
        LeadId = leadId;
    }

    public void SetIssue(string issue)
    {
        if (!string.IsNullOrWhiteSpace(issue))
        {
            Issue = issue.Trim();
        }
    }

    public void SetAddress(string address)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            Address = address.Trim();
        }
    }

    public void SetUrgency(string urgency)
    {
        if (!string.IsNullOrWhiteSpace(urgency))
        {
            Urgency = urgency.Trim();
        }
    }

    public void SetPreferredTime(string preferredTime)
    {
        if (!string.IsNullOrWhiteSpace(preferredTime))
        {
            PreferredTime = preferredTime.Trim();
        }
    }

    public void ClearPreferredTime()
    {
        PreferredTime = null;
    }

    public void SetConfirmation(bool confirmed)
    {
        IsConfirmed = confirmed;
    }

    public void SetStage(ConversationStage stage)
    {
        Stage = stage;
        if (stage == ConversationStage.Completed)
        {
            Status = "closed";
        }
    }

    private void AppendMessage(string role, string text, DateTimeOffset timestamp)
    {
        _messages.Add(new Message(role, text, timestamp));

        if (_messages.Count > MaxMessages)
        {
            _messages.RemoveRange(0, _messages.Count - MaxMessages);
        }

        LastMessageAt = timestamp;
    }

    public static Conversation CreateNew(string tenantId, string phone, DateTimeOffset now, ConversationChannel channel = ConversationChannel.Sms)
        => new(
            $"conv_{channel.ToString().ToLowerInvariant()}_{NormalizePhone(phone)}",
            tenantId,
            phone,
            null,
            "active",
            ConversationStage.Issue,
            null,
            null,
            null,
            null,
            null,
            [],
            now,
            channel);

    private static string NormalizePhone(string phone)
        => new(phone.Where(char.IsDigit).ToArray());
}
