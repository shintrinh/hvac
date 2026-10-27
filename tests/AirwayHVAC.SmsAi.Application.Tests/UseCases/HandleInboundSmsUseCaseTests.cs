using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.UseCases;

public sealed class HandleInboundSmsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPersistConversationLeadEvents_AndSendSms()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var sms = new FakeOutboundSmsService();

        var sut = new HandleInboundSmsUseCase(
            tenantRepo,
            sms,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new LeadService(leadRepo),
            new EventService(eventRepo),
            new ConversationFlowEngine(),
            NullLogger<HandleInboundSmsUseCase>.Instance);

        var result = await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "My AC is blowing warm air"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ConversationId));
        Assert.False(string.IsNullOrWhiteSpace(result.LeadId));
        Assert.Contains("service address", result.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("+15555550123", sms.LastToPhoneNumber);
        Assert.Equal(2, conversationRepo.LastSaved!.Messages.Count);
        Assert.Equal(ConversationStage.Address, conversationRepo.LastSaved.Stage);
        Assert.Contains(eventRepo.Events, x => x.Type == "sms_received");
        Assert.Contains(eventRepo.Events, x => x.Type == "stage_changed");
        Assert.Contains(eventRepo.Events, x => x.Type == "sms_sent");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowIssueToConfirmToCompletedFlow()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var sms = new FakeOutboundSmsService();

        var sut = new HandleInboundSmsUseCase(
            tenantRepo,
            sms,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new LeadService(leadRepo),
            new EventService(eventRepo),
            new ConversationFlowEngine(),
            NullLogger<HandleInboundSmsUseCase>.Instance);

        await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "AC not cooling"), CancellationToken.None);
        await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "123 Main St"), CancellationToken.None);
        await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "emergency"), CancellationToken.None);
        var confirm = await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "tomorrow 9am"), CancellationToken.None);
        var completed = await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+18777804236", "+15555550123", "yes"), CancellationToken.None);

        Assert.Contains("Please confirm", confirm.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confirmed", completed.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationStage.Completed, conversationRepo.LastSaved!.Stage);
        Assert.Contains(eventRepo.Events, x => x.Type == "booking_confirmed");
    }


    [Fact]
    public async Task ExecuteAsync_ShouldResolveTenantByTenantId_WhenProvided()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var sms = new FakeOutboundSmsService();

        var sut = new HandleInboundSmsUseCase(
            tenantRepo,
            sms,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new LeadService(leadRepo),
            new EventService(eventRepo),
            new ConversationFlowEngine(),
            NullLogger<HandleInboundSmsUseCase>.Instance);

        await sut.ExecuteAsync(new HandleInboundSmsCommand("tenant_hdr", string.Empty, "+15555550123", "AC not cooling"), CancellationToken.None);

        Assert.Equal("tenant_hdr", conversationRepo.LastSaved!.TenantId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveTenantByPhoneNumber_AfterRemovingPlusPrefix()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var sms = new FakeOutboundSmsService();

        var sut = new HandleInboundSmsUseCase(
            tenantRepo,
            sms,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new LeadService(leadRepo),
            new EventService(eventRepo),
            new ConversationFlowEngine(),
            NullLogger<HandleInboundSmsUseCase>.Instance);

        await sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, "+17854078412", "+15555550123", "AC not cooling"), CancellationToken.None);

        Assert.Contains("+17854078412", tenantRepo.PhoneLookups);
        Assert.Contains("17854078412", tenantRepo.PhoneLookups);
        Assert.Equal("tenant_phone_without_plus", conversationRepo.LastSaved!.TenantId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailSafe_WhenRoutingDecisionHasNoTenantData()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var sms = new FakeOutboundSmsService();

        var sut = new HandleInboundSmsUseCase(
            tenantRepo,
            sms,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new LeadService(leadRepo),
            new EventService(eventRepo),
            new ConversationFlowEngine(),
            NullLogger<HandleInboundSmsUseCase>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
            => sut.ExecuteAsync(new HandleInboundSmsCommand(string.Empty, string.Empty, "+15555550123", "AC not cooling"), CancellationToken.None));

        Assert.Contains("missing both tenant id and phone mapping", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<string> PhoneLookups { get; } = [];

        public Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
            => Task.FromResult<Tenant?>(new Tenant(tenantId, tenantId, "CoolAir HVAC", "+18777804236", "Houston TX", "America/Chicago", new BusinessHours("08:00", "18:00"), DateTimeOffset.UtcNow));

        public Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
        {
            PhoneLookups.Add(phoneNumber);

            if (string.Equals(phoneNumber, "17854078412", StringComparison.Ordinal))
            {
                return Task.FromResult<Tenant?>(new Tenant("tenant_phone_without_plus", "tenant_phone_without_plus", "CoolAir HVAC", phoneNumber, "Houston TX", "America/Chicago", new BusinessHours("08:00", "18:00"), DateTimeOffset.UtcNow));
            }

            if (string.Equals(phoneNumber, "+18777804236", StringComparison.Ordinal))
            {
                return Task.FromResult<Tenant?>(new Tenant("tenant_123", "tenant_123", "CoolAir HVAC", phoneNumber, "Houston TX", "America/Chicago", new BusinessHours("08:00", "18:00"), DateTimeOffset.UtcNow));
            }

            return Task.FromResult<Tenant?>(null);
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly Dictionary<string, Conversation> _store = new();

        public Conversation? LastSaved { get; private set; }

        public Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, CancellationToken cancellationToken)
            => GetByPhoneAsync(tenantId, phone, ConversationChannel.Sms, cancellationToken);

        public Task<Conversation?> GetByPhoneAsync(string tenantId, string phone, ConversationChannel channel, CancellationToken cancellationToken)
        {
            _store.TryGetValue($"{tenantId}:{phone}", out var conversation);
            return Task.FromResult(conversation);
        }

        public Task<Conversation?> GetByIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
        {
            var conversation = _store.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == conversationId);
            return Task.FromResult(conversation);
        }

        public Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetPagedAsync(
            string tenantId,
            int page,
            int pageSize,
            string? status,
            CancellationToken cancellationToken)
        {
            var query = _store.Values.Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(x => x.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<(IReadOnlyList<Conversation> Items, int TotalCount)>((items, totalCount));
        }

        public Task<IReadOnlyList<Conversation>> GetByLastMessageAtRangeAsync(
            string tenantId,
            DateTimeOffset fromInclusive,
            DateTimeOffset toExclusive,
            CancellationToken cancellationToken)
        {
            var items = _store.Values
                .Where(x => x.TenantId == tenantId && x.LastMessageAt >= fromInclusive && x.LastMessageAt < toExclusive)
                .ToList();
            return Task.FromResult<IReadOnlyList<Conversation>>(items);
        }

        public Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            LastSaved = conversation;
            _store[$"{conversation.TenantId}:{conversation.Phone}"] = conversation;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private Lead? _lead;

        public Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
            => Task.FromResult(_lead);

        public Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken)
            => Task.FromResult(_lead is not null && _lead.TenantId == tenantId && _lead.Id == leadId ? _lead : null);

        public Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
            string tenantId,
            int page,
            int pageSize,
            string? status,
            CancellationToken cancellationToken)
        {
            var items = _lead is null
                ? []
                : new List<Lead> { _lead }.Where(x => x.TenantId == tenantId).ToList();

            return Task.FromResult<(IReadOnlyList<Lead> Items, int TotalCount)>((items, items.Count));
        }

        public Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(
            string tenantId,
            DateTimeOffset fromInclusive,
            DateTimeOffset toExclusive,
            CancellationToken cancellationToken)
        {
            var items = _lead is null
                ? []
                : new List<Lead> { _lead }
                    .Where(x => x.TenantId == tenantId && x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive)
                    .ToList();

            return Task.FromResult<IReadOnlyList<Lead>>(items);
        }

        public Task UpsertAsync(Lead lead, CancellationToken cancellationToken)
        {
            _lead = lead;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        public List<EventLog> Events { get; } = [];

        public Task AddAsync(EventLog eventLog, CancellationToken cancellationToken)
        {
            Events.Add(eventLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboundSmsService : IOutboundSmsService
    {
        public string LastToPhoneNumber { get; private set; } = string.Empty;

        public Task SendAsync(string tenantId, string toPhoneNumber, string message, CancellationToken cancellationToken)
        {
            LastToPhoneNumber = toPhoneNumber;
            return Task.CompletedTask;
        }
    }
}
