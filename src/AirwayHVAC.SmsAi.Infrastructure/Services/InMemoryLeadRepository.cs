using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AirwayHVAC.SmsAi.Infrastructure.Services;

public sealed class InMemoryLeadRepository : ILeadRepository
{
    private readonly ConcurrentDictionary<string, Lead> _store = new();

    public Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
    {
        var lead = _store.Values.FirstOrDefault(x => x.TenantId == tenantId && x.ConversationId == conversationId);
        return Task.FromResult(lead);
    }

    public Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(leadId, out var lead);
        return Task.FromResult(lead is not null && lead.TenantId == tenantId ? lead : null);
    }

    public Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _store.Values.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => string.Equals(x.Status.ToString(), status, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<Lead> Items, int TotalCount)>((items, totalCount));
    }

    public Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(
        string tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var items = _store.Values
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Lead>>(items);
    }

    public Task UpsertAsync(Lead lead, CancellationToken cancellationToken)
    {
        _store[lead.Id] = lead;
        return Task.CompletedTask;
    }
}
