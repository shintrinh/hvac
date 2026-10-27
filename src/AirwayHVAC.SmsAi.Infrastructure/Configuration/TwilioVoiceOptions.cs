namespace AirwayHVAC.SmsAi.Infrastructure.Configuration;

public sealed class TwilioVoiceOptions
{
    public const string SectionName = "TwilioVoice";

    public bool Enabled { get; set; } = true;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public string HumanHandoffPhoneNumber { get; set; } = string.Empty;

    public int GatherTimeoutSeconds { get; set; } = 5;

    public int MaxRetriesPerStage { get; set; } = 2;

    public string DisclosureMessage { get; set; } = "This call may be handled by an AI assistant for scheduling HVAC service.";

    public string VoiceName { get; set; } = "alice";

    public string Language { get; set; } = "en-US";
}
