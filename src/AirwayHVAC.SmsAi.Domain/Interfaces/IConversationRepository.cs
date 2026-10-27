using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, CancellationToken cancellationToken);

    Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, ConversationChannel channel, CancellationToken cancellationToken);

    Task<Conversation?> GetByIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Conversation>> GetByLastMessageAtRangeAsync(
        string tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken);

    Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken);
}
