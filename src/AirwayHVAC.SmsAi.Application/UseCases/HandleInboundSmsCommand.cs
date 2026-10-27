namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleInboundSmsCommand(
    string TenantId,
    string TenantPhoneNumber,
    string FromPhoneNumber,
    string MessageBody);
