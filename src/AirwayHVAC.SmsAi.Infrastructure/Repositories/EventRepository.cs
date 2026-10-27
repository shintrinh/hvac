using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly Container _container;

    public EventRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.EventsContainer);
    }

    public Task AddAsync(EventLog eventLog, CancellationToken cancellationToken)
    {
        var doc = new EventDocument
        {
            Id = eventLog.Id,
            TenantId = eventLog.TenantId,
            Type = eventLog.Type,
            Phone = eventLog.Phone,
            ConversationId = eventLog.ConversationId,
            Timestamp = eventLog.Timestamp,
        };

        return _container.CreateItemAsync(doc, new PartitionKey(eventLog.TenantId), cancellationToken: cancellationToken);
    }
}
