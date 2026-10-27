using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed class HandleVoiceGatherUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IVoiceSessionRepository _voiceSessionRepository;
    private readonly ConversationOrchestrator _orchestrator;
    private readonly EventService _eventService;
    private readonly ILogger<HandleVoiceGatherUseCase> _logger;

    public HandleVoiceGatherUseCase(
        ITenantRepository tenantRepository,
        IVoiceSessionRepository voiceSessionRepository,
        ConversationOrchestrator orchestrator,
        EventService eventService,
        ILogger<HandleVoiceGatherUseCase> logger)
    {
        _tenantRepository = tenantRepository;
        _voiceSessionRepository = voiceSessionRepository;
        _orchestrator = orchestrator;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<HandleVoiceResult> ExecuteAsync(HandleVoiceGatherCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var voiceSession = await _voiceSessionRepository.GetByProviderCallIdAsync(command.CallSid, cancellationToken)
            ?? throw new InvalidOperationException($"Voice session not found for call {command.CallSid}");

        if (string.IsNullOrWhiteSpace(command.SpeechResult))
        {
            voiceSession.RecordSilence(now);
            voiceSession.RequestHandoff("no_speech", now);
            await _voiceSessionRepository.UpsertAsync(voiceSession, cancellationToken);
            await _eventService.TrackAsync(voiceSession.TenantId, "voice_speech_empty", voiceSession.FromPhone, voiceSession.ConversationId, now, cancellationToken);

            return new HandleVoiceResult(
                voiceSession.ConversationId,
                voiceSession.Id,
                "I did not hear a response. Please hold while I connect you to our team.",
                voiceSession.CurrentStage,
                false,
                true,
                voiceSession.HandoffReason);
        }

        if (ShouldHandoff(command.SpeechResult))
        {
            voiceSession.RequestHandoff("customer_requested_human", now);
            await _voiceSessionRepository.UpsertAsync(voiceSession, cancellationToken);
            await _eventService.TrackAsync(voiceSession.TenantId, "voice_handoff_requested", voiceSession.FromPhone, voiceSession.ConversationId, now, cancellationToken);

            return new HandleVoiceResult(
                voiceSession.ConversationId,
                voiceSession.Id,
                "Please hold while I connect you to our team.",
                voiceSession.CurrentStage,
                false,
                true,
                voiceSession.HandoffReason);
        }

        var tenant = await _tenantRepository.GetByTenantIdAsync(voiceSession.TenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant not found for tenant id {voiceSession.TenantId}");

        await _eventService.TrackAsync(voiceSession.TenantId, "voice_speech_received", voiceSession.FromPhone, voiceSession.ConversationId, now, cancellationToken);
        var result = await _orchestrator.ProcessCustomerMessageAsync(tenant, voiceSession.FromPhone, command.SpeechResult, ConversationChannel.Voice, now, cancellationToken);

        voiceSession.RecordTurn(result.Stage, DateTimeOffset.UtcNow);
        if (result.Stage == ConversationStage.Completed)
        {
            voiceSession.Complete("completed", DateTimeOffset.UtcNow);
        }

        await _voiceSessionRepository.UpsertAsync(voiceSession, cancellationToken);

        _logger.LogInformation("Voice gather processed for call {CallSid}, stage {Stage}", command.CallSid, result.Stage);

        return new HandleVoiceResult(
            result.ConversationId,
            voiceSession.Id,
            result.ReplyMessage,
            result.Stage,
            result.Stage == ConversationStage.Completed,
            false,
            null);
    }

    private static bool ShouldHandoff(string text)
        => text.Contains("human", StringComparison.OrdinalIgnoreCase)
           || text.Contains("agent", StringComparison.OrdinalIgnoreCase)
           || text.Contains("representative", StringComparison.OrdinalIgnoreCase)
           || text.Contains("operator", StringComparison.OrdinalIgnoreCase)
           || text.Contains("person", StringComparison.OrdinalIgnoreCase);
}
