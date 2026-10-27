using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace AirwayHVAC.SmsAi.Infrastructure.Services;

public sealed class TwilioSmsService : IOutboundSmsService
{
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IOptions<TwilioOptions> options, ILogger<TwilioSmsService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        //TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        //_logger.LogInformation("Sending SMS reply for tenant {TenantId} to {PhoneNumber}", tenantId, toPhoneNumber);

        //await MessageResource.CreateAsync(
        //    to: new PhoneNumber(toPhoneNumber),
        //    from: new PhoneNumber(_options.FromNumber),
        //    body: message);

        // Khởi tạo Twilio client
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);

        _logger.LogInformation(
            "Sending SMS for tenant {TenantId} to {PhoneNumber}",
            tenantId,
            toPhoneNumber);

        try
        {
            var result = await MessageResource.CreateAsync(
                to: new PhoneNumber(toPhoneNumber),
                messagingServiceSid: _options.MessagingServiceSid,
                body: message
                );

            _logger.LogInformation($"SMS sent successfully. SID: {result.Sid} with message: {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SMS for tenant {TenantId} to {PhoneNumber}",
                tenantId,
                toPhoneNumber);

            throw;
        }

    }


}
