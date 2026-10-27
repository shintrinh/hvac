using AirwayHVAC.SmsAi.Domain.Entities;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface IAiResponder
{
    Task<string> GenerateHvacResponseAsync(Tenant tenant, string customerPhone, IReadOnlyCollection<Message> conversationHistory, string inboundMessage, CancellationToken cancellationToken);
}
