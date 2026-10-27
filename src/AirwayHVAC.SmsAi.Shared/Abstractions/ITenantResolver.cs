namespace AirwayHVAC.SmsAi.Shared.Abstractions;

public interface ITenantResolver
{
    TenantRoutingDecision Resolve(
        string? hostName,
        string fromPhoneNumber,
        string toPhoneNumber,
        string? tenantIdHeader,
        string? verificationKeyHeader);
}

public sealed record TenantRoutingDecision(
    string? TenantId,
    string? TenantPhoneNumber,
    string Source,
    string Reason,
    bool Rejected)
{
    public static TenantRoutingDecision UseTenantId(string tenantId, string source, string reason)
        => new(tenantId, null, source, reason, false);

    public static TenantRoutingDecision UsePhoneMapping(string tenantPhoneNumber, string reason)
        => new(null, tenantPhoneNumber, "to_phone", reason, false);

    public static TenantRoutingDecision Reject(string reason)
        => new(null, null, "rejected", reason, true);
}
