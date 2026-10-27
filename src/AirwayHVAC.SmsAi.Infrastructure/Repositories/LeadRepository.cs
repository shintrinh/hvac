using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly Container _container;

    public LeadRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.LeadsContainer);
    }

    public async Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.conversationId = @conversationId OFFSET 0 LIMIT 1")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@conversationId", conversationId);

        using var iterator = _container.GetItemQueryIterator<LeadDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
            MaxItemCount = 1,
        });

        var response = await iterator.ReadNextAsync(cancellationToken);
        var doc = response.Resource.FirstOrDefault();
        return doc is null ? null : Map(doc);
    }

    public async Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.id = @leadId OFFSET 0 LIMIT 1")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@leadId", leadId);

        using var iterator = _container.GetItemQueryIterator<LeadDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
            MaxItemCount = 1,
        });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var doc = response.Resource.FirstOrDefault();
        return doc is null ? null : Map(doc);
    }

    public async Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();

        var where = "c.tenantId = @tenantId";
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            where += normalizedStatus == "contacted"
                ? " AND (c.status = @status OR c.status = @legacyStatus)"
                : " AND c.status = @status";
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {where} ORDER BY c.createdAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var countQuery = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {where}")
            .WithParameter("@tenantId", tenantId);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query.WithParameter("@status", normalizedStatus);
            countQuery.WithParameter("@status", normalizedStatus);
            if (normalizedStatus == "contacted")
            {
                query.WithParameter("@legacyStatus", "qualified");
                countQuery.WithParameter("@legacyStatus", "qualified");
            }
        }

        var items = new List<Lead>();
        using var iterator = _container.GetItemQueryIterator<LeadDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
            MaxItemCount = pageSize,
        });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            items.AddRange(response.Resource.Select(Map));
        }

        var totalCount = 0;
        using var countIterator = _container.GetItemQueryIterator<int>(countQuery, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
            MaxItemCount = 1,
        });

        if (countIterator.HasMoreResults)
        {
            var countResponse = await countIterator.ReadNextAsync(cancellationToken);
            totalCount = countResponse.Resource.FirstOrDefault();
        }

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(
        string tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.createdAt >= @fromInclusive AND c.createdAt < @toExclusive ORDER BY c.createdAt ASC")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@fromInclusive", fromInclusive)
            .WithParameter("@toExclusive", toExclusive);

        var items = new List<Lead>();
        using var iterator = _container.GetItemQueryIterator<LeadDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(tenantId),
        });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            items.AddRange(response.Resource.Select(Map));
        }

        return items;
    }

    public Task UpsertAsync(Lead lead, CancellationToken cancellationToken)
        => _container.UpsertItemAsync(Map(lead), new PartitionKey(lead.TenantId), cancellationToken: cancellationToken);

    private static LeadDocument Map(Lead lead) => new()
    {
        Id = lead.Id,
        TenantId = lead.TenantId,
        Phone = lead.Phone,
        Name = lead.Name,
        Issue = lead.Issue,
        Address = lead.Address,
        PreferredTime = lead.PreferredTime,
        Status = lead.Status.ToString().ToLowerInvariant(),
        Source = lead.Source,
        ConversationId = lead.ConversationId,
        CreatedAt = lead.CreatedAt,
        UpdatedAt = lead.UpdatedAt,
    };

    private static Lead Map(LeadDocument doc)
        => new(
            doc.Id,
            doc.TenantId,
            doc.Phone,
            doc.Name,
            doc.Issue,
            doc.Address,
            doc.PreferredTime,
            Enum.TryParse<LeadStatus>(doc.Status, true, out var status) ? status : LeadStatus.New,
            doc.Source,
            doc.ConversationId,
            doc.CreatedAt,
            doc.UpdatedAt == default ? doc.CreatedAt : doc.UpdatedAt);
}
