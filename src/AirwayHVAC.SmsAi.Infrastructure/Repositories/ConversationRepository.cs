using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Repositories;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly Container _container;

    public ConversationRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.ConversationsContainer);
    }

    public async Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, CancellationToken cancellationToken)
        => await GetByPhoneAsync(tenantId, phone, ConversationChannel.Sms, cancellationToken);

    public async Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, ConversationChannel channel, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.phone = @phone AND (c.channel = @channel OR (NOT IS_DEFINED(c.channel) AND @channel = 'sms')) OFFSET 0 LIMIT 1")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@phone", phone)
            .WithParameter("@channel", channel.ToString().ToLowerInvariant());

        using var iterator = _container.GetItemQueryIterator<ConversationDocument>(query, requestOptions: new QueryRequestOptions
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

    public async Task<Conversation?> GetByIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.id = @conversationId OFFSET 0 LIMIT 1")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@conversationId", conversationId);

        using var iterator = _container.GetItemQueryIterator<ConversationDocument>(query, requestOptions: new QueryRequestOptions
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

    public async Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetPagedAsync(
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
            where += " AND c.status = @status";
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {where} ORDER BY c.lastMessageAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var countQuery = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {where}")
            .WithParameter("@tenantId", tenantId);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query.WithParameter("@status", normalizedStatus);
            countQuery.WithParameter("@status", normalizedStatus);
        }

        var items = new List<Conversation>();
        using var iterator = _container.GetItemQueryIterator<ConversationDocument>(query, requestOptions: new QueryRequestOptions
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

    public async Task<IReadOnlyList<Conversation>> GetByLastMessageAtRangeAsync(
        string tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId AND c.lastMessageAt >= @fromInclusive AND c.lastMessageAt < @toExclusive ORDER BY c.lastMessageAt DESC")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@fromInclusive", fromInclusive)
            .WithParameter("@toExclusive", toExclusive);

        var items = new List<Conversation>();
        using var iterator = _container.GetItemQueryIterator<ConversationDocument>(query, requestOptions: new QueryRequestOptions
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

    public Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken)
        => _container.UpsertItemAsync(Map(conversation), new PartitionKey(conversation.TenantId), cancellationToken: cancellationToken);

    private static ConversationDocument Map(Conversation conversation) => new()
    {
        Id = conversation.Id,
        TenantId = conversation.TenantId,
        Phone = conversation.Phone,
        LeadId = conversation.LeadId,
        Status = conversation.Status,
        Channel = conversation.Channel.ToString().ToLowerInvariant(),
        Stage = conversation.Stage.ToString(),
        Issue = conversation.Issue,
        Address = conversation.Address,
        Urgency = conversation.Urgency,
        PreferredTime = conversation.PreferredTime,
        IsConfirmed = conversation.IsConfirmed,
        LastMessageAt = conversation.LastMessageAt,
        Messages = conversation.Messages.Select(x => new MessageDocument
        {
            Role = x.Role,
            Text = x.Text,
            Timestamp = x.Timestamp,
        }).ToList(),
    };

    private static Conversation Map(ConversationDocument document)
        => new(
            document.Id,
            document.TenantId,
            document.Phone,
            document.LeadId,
            document.Status,
            Enum.TryParse<ConversationStage>(document.Stage, true, out var stage) ? stage : ConversationStage.Issue,
            document.Issue,
            document.Address,
            document.Urgency,
            document.PreferredTime,
            document.IsConfirmed,
            document.Messages.Select(x => new Message(x.Role, x.Text, x.Timestamp)),
            document.LastMessageAt,
            Enum.TryParse<ConversationChannel>(document.Channel, true, out var channel) ? channel : ConversationChannel.Sms);
}
