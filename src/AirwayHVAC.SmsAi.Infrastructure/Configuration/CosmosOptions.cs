namespace AirwayHVAC.SmsAi.Infrastructure.Configuration;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "hvac-saas";

    public string TenantsContainer { get; set; } = "tenants";

    public string ConversationsContainer { get; set; } = "conversations";

    public string LeadsContainer { get; set; } = "leads";

    public string EventsContainer { get; set; } = "events";

    public string VoiceSessionsContainer { get; set; } = "voiceSessions";
}
