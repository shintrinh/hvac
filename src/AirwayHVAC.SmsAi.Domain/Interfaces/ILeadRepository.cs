using AirwayHVAC.SmsAi.Domain.Entities;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface ILeadRepository
{
    Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken);

    Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(
        string tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken);

    Task UpsertAsync(Lead lead, CancellationToken cancellationToken);
}
