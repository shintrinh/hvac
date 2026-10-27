namespace AirwayHVAC.SmsAi.Infrastructure.Configuration;

public sealed class TenantRoutingOptions
{
    public const string SectionName = "TenantRouting";

    public bool AllowDefaultTenantFallback { get; set; }

    public string? DefaultTenantId { get; set; }

    public string TenantIdHeaderName { get; set; } = "X-Tenant-Id";

    public string VerificationKeyHeaderName { get; set; } = "X-Tenant-Key";

    public string? VerificationKey { get; set; }
}
