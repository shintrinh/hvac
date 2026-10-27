using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace AirwayHVAC.SmsAi.Infrastructure.Services;

public sealed class OpenAiResponder : IAiResponder
{
    private readonly AzureOpenAiOptions _options;

    public OpenAiResponder(Microsoft.Extensions.Options.IOptions<AzureOpenAiOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> GenerateHvacResponseAsync(Tenant tenant, string customerPhone, IReadOnlyCollection<Message> conversationHistory, string inboundMessage, CancellationToken cancellationToken)
    {
        var endpoint = _options.Endpoint;
        var deployment = _options.DeploymentName;
        var apiKey = _options.ApiKey;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiKey))
        {
            return "Thanks for contacting us. Please share your address and preferred appointment time.";
        }

        ChatClient client = new(
            credential: new ApiKeyCredential(apiKey),
            model: deployment,
            options: new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_options.SystemPrompt),
            new SystemChatMessage($"Tenant {tenant.CompanyName} in {tenant.ServiceArea}, business hours {tenant.BusinessHours.Open}-{tenant.BusinessHours.Close} {tenant.Timezone}.")
        };

        foreach (var message in conversationHistory)
        {
            messages.Add(message.Role == "customer" ? new UserChatMessage(message.Text) : new AssistantChatMessage(message.Text));
        }

        messages.Add(new UserChatMessage(inboundMessage));

        var completion = await client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return string.Join(string.Empty, completion.Value.Content.Select(x => x.Text)).Trim();
    }
}
