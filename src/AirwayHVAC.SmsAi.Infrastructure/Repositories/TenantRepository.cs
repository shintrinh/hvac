using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly Container _container;

    public TenantRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.TenantsContainer);
    }

    public async Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId OFFSET 0 LIMIT 1")
            .WithParameter("@tenantId", tenantId);

        using var iterator = _container.GetItemQueryIterator<TenantDocument>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var doc = response.Resource.FirstOrDefault();

        return Map(doc);
    }

    public async Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.phoneNumber = @phoneNumber OFFSET 0 LIMIT 1")
            .WithParameter("@phoneNumber", phoneNumber);

        using var iterator = _container.GetItemQueryIterator<TenantDocument>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var doc = response.Resource.FirstOrDefault();

        return Map(doc);
    }

    private static Tenant? Map(TenantDocument? doc)
        => doc is null
            ? null
            : new Tenant(
                doc.Id,
                doc.TenantId,
                doc.CompanyName,
                doc.PhoneNumber,
                doc.ServiceArea,
                doc.Timezone,
                new BusinessHours(doc.BusinessHours.Open, doc.BusinessHours.Close),
                doc.CreatedAt);
}

