namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleVoiceStatusCommand(
    string CallSid,
    string CallStatus);
