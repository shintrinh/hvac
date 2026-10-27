namespace AirwayHVAC.SmsAi.Infrastructure.Configuration;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAi";

    public string Endpoint { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string DeploymentName { get; init; } = "gpt-4o-mini";

    public string ApiVersion { get; init; } = "2024-06-01";

    public string SystemPrompt { get; init; } = "You are an expert HVAC customer support assistant. Be concise, helpful, and ask clarifying questions when needed.";
}
