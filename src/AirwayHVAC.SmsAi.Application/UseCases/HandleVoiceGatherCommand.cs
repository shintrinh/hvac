namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleVoiceGatherCommand(
    string CallSid,
    string SpeechResult,
    decimal? Confidence);
