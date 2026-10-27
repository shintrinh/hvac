namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface IOutboundSmsService
{
    Task SendAsync(string tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken);
}
