namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class TenantDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string ServiceArea { get; set; } = string.Empty;

    public string Timezone { get; set; } = string.Empty;

    public BusinessHoursDocument BusinessHours { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
}
