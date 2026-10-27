using AirwayHVAC.SmsAi.Domain.Entities;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface IVoiceSessionRepository
{
    Task<VoiceSession?> GetByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken);

    Task UpsertAsync(VoiceSession voiceSession, CancellationToken cancellationToken);
}
