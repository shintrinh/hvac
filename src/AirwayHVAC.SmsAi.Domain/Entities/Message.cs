namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class Message
{
    public Message(string role, string text, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text is required.", nameof(text));

        Role = role;
        Text = text;
        Timestamp = timestamp;
    }

    public string Role { get; }

    public string Text { get; }

    public DateTimeOffset Timestamp { get; }
}
