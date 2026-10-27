using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Shared.Abstractions;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Services;

public sealed class HeaderTenantResolver : ITenantResolver
{
    private readonly TenantRoutingOptions _options;

    public HeaderTenantResolver(IOptions<TenantRoutingOptions> options)
    {
        _options = options.Value;
    }

    public TenantRoutingDecision Resolve(
        string? hostName,
        string fromPhoneNumber,
        string toPhoneNumber,
        string? tenantIdHeader,
        string? verificationKeyHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantIdHeader))
        {
            if (IsVerifiedHeader(verificationKeyHeader))
            {
                return TenantRoutingDecision.UseTenantId(tenantIdHeader.Trim(), "verified_header", "Verified tenant header accepted.");
            }

            return TenantRoutingDecision.Reject("Tenant header was provided but verification failed.");
        }

        if (!string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            return TenantRoutingDecision.UsePhoneMapping(toPhoneNumber, "No verified tenant header. Falling back to To phone mapping.");
        }

        if (_options.AllowDefaultTenantFallback && !string.IsNullOrWhiteSpace(_options.DefaultTenantId))
        {
            return TenantRoutingDecision.UseTenantId(_options.DefaultTenantId, "default_fallback", "Configured development fallback applied.");
        }

        return TenantRoutingDecision.Reject("Unable to resolve tenant (no verified header and no To phone number).");
    }

    private bool IsVerifiedHeader(string? verificationKeyHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.VerificationKey))
        {
            return false;
        }

        return string.Equals(verificationKeyHeader, _options.VerificationKey, StringComparison.Ordinal);
    }
}
