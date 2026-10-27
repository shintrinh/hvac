using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.UseCases;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task UpdateLeadStatusAsync_ShouldFollowAllowedTransitions()
    {
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository(new Lead(
            "lead_1",
            "tenant_1",
            "+15551230000",
            null,
            null,
            null,
            null,
            LeadStatus.New,
            "sms",
            "conv_1",
            DateTimeOffset.UtcNow));

        var sut = new DashboardService(conversationRepo, leadRepo);

        var updated = await sut.UpdateLeadStatusAsync("tenant_1", "lead_1", LeadStatus.Contacted, CancellationToken.None);

        Assert.Equal(LeadStatus.Contacted, updated.Status);
    }

    [Fact]
    public async Task UpdateLeadStatusAsync_ShouldRejectInvalidTransition()
    {
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository(new Lead(
            "lead_1",
            "tenant_1",
            "+15551230000",
            null,
            null,
            null,
            null,
            LeadStatus.New,
            "sms",
            "conv_1",
            DateTimeOffset.UtcNow));

        var sut = new DashboardService(conversationRepo, leadRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateLeadStatusAsync("tenant_1", "lead_1", LeadStatus.Booked, CancellationToken.None));
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, CancellationToken cancellationToken)
            => GetByPhoneAsync(tenantId, phone, ConversationChannel.Sms, cancellationToken);

        public Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, ConversationChannel channel, CancellationToken cancellationToken)
            => Task.FromResult<Conversation?>(null);

        public Task<Conversation?> GetByIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
            => Task.FromResult<Conversation?>(null);

        public Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetPagedAsync(string tenantId, int page, int pageSize, string? status, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<Conversation> Items, int TotalCount)>(([], 0));

        public Task<IReadOnlyList<Conversation>> GetByLastMessageAtRangeAsync(string tenantId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Conversation>>([]);

        public Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private Lead _lead;

        public FakeLeadRepository(Lead lead)
        {
            _lead = lead;
        }

        public Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
            => Task.FromResult<Lead?>(_lead);

        public Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken)
            => Task.FromResult<Lead?>(_lead.Id == leadId && _lead.TenantId == tenantId ? _lead : null);

        public Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(string tenantId, int page, int pageSize, string? status, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<Lead> Items, int TotalCount)>((new List<Lead> { _lead }, 1));

        public Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(string tenantId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Lead>>([_lead]);

        public Task UpsertAsync(Lead lead, CancellationToken cancellationToken)
        {
            _lead = lead;
            return Task.CompletedTask;
        }
    }
}
