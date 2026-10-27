using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Infrastructure.Repositories;

public sealed class VoiceSessionRepository : IVoiceSessionRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosOptions _options;
    private Container? _container;

    public VoiceSessionRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
    }

    public async Task<VoiceSession?> GetByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.providerCallId = @providerCallId OFFSET 0 LIMIT 1")
            .WithParameter("@providerCallId", providerCallId);

        using var iterator = container.GetItemQueryIterator<VoiceSessionDocument>(query, requestOptions: new QueryRequestOptions
        {
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

    public async Task UpsertAsync(VoiceSession voiceSession, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.UpsertItemAsync(Map(voiceSession), new PartitionKey(voiceSession.TenantId), cancellationToken: cancellationToken);
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
        {
            return _container;
        }

        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken);
        var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_options.VoiceSessionsContainer, "/tenantId"),
            cancellationToken: cancellationToken);

        _container = containerResponse.Container;
        return _container;
    }

    private static VoiceSessionDocument Map(VoiceSession voiceSession) => new()
    {
        Id = voiceSession.Id,
        TenantId = voiceSession.TenantId,
        ConversationId = voiceSession.ConversationId,
        Provider = voiceSession.Provider,
        ProviderCallId = voiceSession.ProviderCallId,
        FromPhone = voiceSession.FromPhone,
        ToPhone = voiceSession.ToPhone,
        Status = voiceSession.Status,
        CurrentStage = voiceSession.CurrentStage.ToString(),
        TurnCount = voiceSession.TurnCount,
        SilenceTimeoutCount = voiceSession.SilenceTimeoutCount,
        HandoffReason = voiceSession.HandoffReason,
        StartedAt = voiceSession.StartedAt,
        LastActivityAt = voiceSession.LastActivityAt,
        EndedAt = voiceSession.EndedAt,
    };

    private static VoiceSession Map(VoiceSessionDocument doc)
        => new(
            doc.Id,
            doc.TenantId,
            doc.ConversationId,
            doc.Provider,
            doc.ProviderCallId,
            doc.FromPhone,
            doc.ToPhone,
            doc.Status,
            Enum.TryParse<ConversationStage>(doc.CurrentStage, true, out var stage) ? stage : ConversationStage.Issue,
            doc.TurnCount,
            doc.SilenceTimeoutCount,
            doc.HandoffReason,
            doc.StartedAt,
            doc.LastActivityAt,
            doc.EndedAt);
}
