namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleInboundVoiceCommand(
    string TenantId,
    string TenantPhoneNumber,
    string FromPhoneNumber,
    string ToPhoneNumber,
    string CallSid);
