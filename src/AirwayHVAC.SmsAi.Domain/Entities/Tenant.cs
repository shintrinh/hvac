namespace AirwayHVAC.SmsAi.Domain.Entities;

public sealed class Tenant
{
    public Tenant(
        string id,
        string tenantId,
        string companyName,
        string phoneNumber,
        string serviceArea,
        string timezone,
        BusinessHours businessHours,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        CompanyName = companyName;
        PhoneNumber = phoneNumber;
        ServiceArea = serviceArea;
        Timezone = timezone;
        BusinessHours = businessHours;
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public string TenantId { get; }

    public string CompanyName { get; }

    public string PhoneNumber { get; }

    public string ServiceArea { get; }

    public string Timezone { get; }

    public BusinessHours BusinessHours { get; }

    public DateTimeOffset CreatedAt { get; }
}

public sealed class BusinessHours
{
    public BusinessHours(string open, string close)
    {
        Open = open;
        Close = close;
    }

    public string Open { get; }

    public string Close { get; }
}
