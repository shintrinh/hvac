using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class ConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IConversationRepository conversationRepository, ILogger<ConversationService> logger)
    {
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    public async Task<Conversation> LoadOrCreateAsync(string tenantId, string phone, DateTimeOffset now, CancellationToken cancellationToken)
        => await LoadOrCreateAsync(tenantId, phone, ConversationChannel.Sms, now, cancellationToken);

    public async Task<Conversation> LoadOrCreateAsync(string tenantId, string phone, ConversationChannel channel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await _conversationRepository.GetByPhoneAsync(tenantId, phone, channel, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        _logger.LogInformation("Creating new {Channel} conversation for tenant {TenantId}, phone {Phone}", channel, tenantId, phone);
        return Conversation.CreateNew(tenantId, phone, now, channel);
    }

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken)
        => _conversationRepository.UpsertAsync(conversation, cancellationToken);
}
