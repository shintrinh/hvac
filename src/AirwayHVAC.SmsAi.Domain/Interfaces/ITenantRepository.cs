using AirwayHVAC.SmsAi.Domain.Entities;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken);

    Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
}
