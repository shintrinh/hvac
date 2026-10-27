using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleVoiceResult(
    string ConversationId,
    string VoiceSessionId,
    string ReplyMessage,
    ConversationStage Stage,
    bool Completed,
    bool HandoffRequested,
    string? HandoffReason);
