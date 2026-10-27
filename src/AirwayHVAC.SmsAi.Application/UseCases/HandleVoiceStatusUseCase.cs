using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed class HandleVoiceStatusUseCase
{
    private readonly IVoiceSessionRepository _voiceSessionRepository;
    private readonly EventService _eventService;
    private readonly ILogger<HandleVoiceStatusUseCase> _logger;

    public HandleVoiceStatusUseCase(
        IVoiceSessionRepository voiceSessionRepository,
        EventService eventService,
        ILogger<HandleVoiceStatusUseCase> logger)
    {
        _voiceSessionRepository = voiceSessionRepository;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task ExecuteAsync(HandleVoiceStatusCommand command, CancellationToken cancellationToken)
    {
        var voiceSession = await _voiceSessionRepository.GetByProviderCallIdAsync(command.CallSid, cancellationToken);
        if (voiceSession is null)
        {
            _logger.LogWarning("Voice status callback ignored because session was not found for call {CallSid}", command.CallSid);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var status = string.IsNullOrWhiteSpace(command.CallStatus) ? "completed" : command.CallStatus.Trim().ToLowerInvariant();
        voiceSession.Complete(status, now);
        await _voiceSessionRepository.UpsertAsync(voiceSession, cancellationToken);

        var eventType = status is "completed" or "busy" or "no-answer" or "canceled"
            ? "voice_call_completed"
            : "voice_call_failed";

        await _eventService.TrackAsync(voiceSession.TenantId, eventType, voiceSession.FromPhone, voiceSession.ConversationId, now, cancellationToken);
    }
}
