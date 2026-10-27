namespace AirwayHVAC.SmsAi.Infrastructure.Configuration;

public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; init; } = string.Empty;

    public string AuthToken { get; init; } = string.Empty;

    public string FromNumber { get; init; } = string.Empty;
    public string MessagingServiceSid { get; set; } = string.Empty;
}
