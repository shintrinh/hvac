using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed class HandleInboundVoiceUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ConversationService _conversationService;
    private readonly EventService _eventService;
    private readonly IVoiceSessionRepository _voiceSessionRepository;
    private readonly ILogger<HandleInboundVoiceUseCase> _logger;

    public HandleInboundVoiceUseCase(
        ITenantRepository tenantRepository,
        ConversationService conversationService,
        EventService eventService,
        IVoiceSessionRepository voiceSessionRepository,
        ILogger<HandleInboundVoiceUseCase> logger)
    {
        _tenantRepository = tenantRepository;
        _conversationService = conversationService;
        _eventService = eventService;
        _voiceSessionRepository = voiceSessionRepository;
        _logger = logger;
    }

    public async Task<HandleVoiceResult> ExecuteAsync(HandleInboundVoiceCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = await ResolveTenantAsync(command.TenantId, command.TenantPhoneNumber, cancellationToken);
        var conversation = await _conversationService.LoadOrCreateAsync(tenant.TenantId, command.FromPhoneNumber, ConversationChannel.Voice, now, cancellationToken);

        var voiceSession = await _voiceSessionRepository.GetByProviderCallIdAsync(command.CallSid, cancellationToken)
            ?? VoiceSession.CreateNew(tenant.TenantId, conversation.Id, command.CallSid, command.FromPhoneNumber, command.ToPhoneNumber, now);

        await _conversationService.SaveAsync(conversation, cancellationToken);
        await _voiceSessionRepository.UpsertAsync(voiceSession, cancellationToken);
        await _eventService.TrackAsync(tenant.TenantId, "voice_call_started", command.FromPhoneNumber, conversation.Id, now, cancellationToken);

        _logger.LogInformation("Inbound voice call started for tenant {TenantId}, call {CallSid}, conversation {ConversationId}", tenant.TenantId, command.CallSid, conversation.Id);

        return new HandleVoiceResult(
            conversation.Id,
            voiceSession.Id,
            "Please tell me what issue you are having with your heating or cooling system.",
            conversation.Stage,
            false,
            false,
            null);
    }

    private async Task<Tenant> ResolveTenantAsync(string tenantId, string tenantPhoneNumber, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return await _tenantRepository.GetByTenantIdAsync(tenantId, cancellationToken)
                ?? throw new InvalidOperationException($"Tenant not found for tenant id {tenantId}");
        }

        if (!string.IsNullOrWhiteSpace(tenantPhoneNumber))
        {
            var tenant = await _tenantRepository.GetByPhoneNumberAsync(tenantPhoneNumber, cancellationToken);
            if (tenant is not null)
            {
                return tenant;
            }

            var normalizedPhone = tenantPhoneNumber.Replace("+", string.Empty, StringComparison.Ordinal).Trim();
            if (!string.Equals(normalizedPhone, tenantPhoneNumber, StringComparison.Ordinal))
            {
                tenant = await _tenantRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
                if (tenant is not null)
                {
                    return tenant;
                }
            }

            throw new InvalidOperationException($"Tenant not found for Twilio number {tenantPhoneNumber}");
        }

        throw new InvalidOperationException("Tenant routing decision is missing both tenant id and phone mapping.");
    }
}
