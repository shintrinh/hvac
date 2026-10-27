using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.UseCases;

public sealed class HandleVoiceUseCaseTests
{
    [Fact]
    public async Task InboundVoice_ShouldCreateVoiceSession_AndTrackStartedEvent()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var eventRepo = new FakeEventRepository();
        var voiceSessionRepo = new FakeVoiceSessionRepository();

        var sut = new HandleInboundVoiceUseCase(
            tenantRepo,
            new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance),
            new EventService(eventRepo),
            voiceSessionRepo,
            NullLogger<HandleInboundVoiceUseCase>.Instance);

        var result = await sut.ExecuteAsync(new HandleInboundVoiceCommand(string.Empty, "+18777804236", "+15555550123", "+18777804236", "CA_TEST_001"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ConversationId));
        Assert.False(string.IsNullOrWhiteSpace(result.VoiceSessionId));
        Assert.Contains("heating or cooling", result.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.HandoffRequested);
        Assert.Equal(ConversationChannel.Voice, conversationRepo.LastSaved!.Channel);
        Assert.NotNull(voiceSessionRepo.LastSaved);
        Assert.Equal("CA_TEST_001", voiceSessionRepo.LastSaved!.ProviderCallId);
        Assert.Contains(eventRepo.Events, x => x.Type == "voice_call_started");
    }

    [Fact]
    public async Task VoiceGather_ShouldUseSharedFlow_CreateVoiceLead_AndCompleteConversation()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var voiceSessionRepo = new FakeVoiceSessionRepository();

        var conversationService = new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance);
        var eventService = new EventService(eventRepo);
        var orchestrator = new ConversationOrchestrator(
            conversationService,
            new LeadService(leadRepo),
            eventService,
            new ConversationFlowEngine(),
            NullLogger<ConversationOrchestrator>.Instance);

        var inbound = new HandleInboundVoiceUseCase(
            tenantRepo,
            conversationService,
            eventService,
            voiceSessionRepo,
            NullLogger<HandleInboundVoiceUseCase>.Instance);

        var gather = new HandleVoiceGatherUseCase(
            tenantRepo,
            voiceSessionRepo,
            orchestrator,
            eventService,
            NullLogger<HandleVoiceGatherUseCase>.Instance);

        await inbound.ExecuteAsync(new HandleInboundVoiceCommand(string.Empty, "+18777804236", "+15555550123", "+18777804236", "CA_TEST_002"), CancellationToken.None);

        var address = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_002", "My AC is blowing warm air", 0.92m), CancellationToken.None);
        var urgency = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_002", "123 Main Street", 0.95m), CancellationToken.None);
        var appointment = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_002", "emergency", 0.91m), CancellationToken.None);
        var confirm = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_002", "tomorrow 9 AM", 0.91m), CancellationToken.None);
        var completed = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_002", "yes", 0.91m), CancellationToken.None);

        Assert.Contains("service address", address.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("emergency", urgency.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("day and time", appointment.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Is that correct", confirm.ReplyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(completed.Completed);
        Assert.Equal(ConversationStage.Completed, completed.Stage);
        Assert.Equal(ConversationChannel.Voice, conversationRepo.LastSaved!.Channel);
        Assert.Equal("voice", leadRepo.LastSaved!.Source);
        Assert.Equal("completed", voiceSessionRepo.LastSaved!.Status);
        Assert.Contains(eventRepo.Events, x => x.Type == "voice_speech_received");
        Assert.Contains(eventRepo.Events, x => x.Type == "booking_confirmed");
    }

    [Fact]
    public async Task VoiceGather_ShouldRequestHandoff_WhenSpeechIsEmpty()
    {
        var tenantRepo = new FakeTenantRepository();
        var conversationRepo = new FakeConversationRepository();
        var leadRepo = new FakeLeadRepository();
        var eventRepo = new FakeEventRepository();
        var voiceSessionRepo = new FakeVoiceSessionRepository();

        var conversationService = new ConversationService(conversationRepo, NullLogger<ConversationService>.Instance);
        var eventService = new EventService(eventRepo);
        var orchestrator = new ConversationOrchestrator(
            conversationService,
            new LeadService(leadRepo),
            eventService,
            new ConversationFlowEngine(),
            NullLogger<ConversationOrchestrator>.Instance);

        var inbound = new HandleInboundVoiceUseCase(
            tenantRepo,
            conversationService,
            eventService,
            voiceSessionRepo,
            NullLogger<HandleInboundVoiceUseCase>.Instance);

        var gather = new HandleVoiceGatherUseCase(
            tenantRepo,
            voiceSessionRepo,
            orchestrator,
            eventService,
            NullLogger<HandleVoiceGatherUseCase>.Instance);

        await inbound.ExecuteAsync(new HandleInboundVoiceCommand(string.Empty, "+18777804236", "+15555550123", "+18777804236", "CA_TEST_003"), CancellationToken.None);

        var result = await gather.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_003", string.Empty, null), CancellationToken.None);

        Assert.True(result.HandoffRequested);
        Assert.Equal("no_speech", result.HandoffReason);
        Assert.Equal("handoff", voiceSessionRepo.LastSaved!.Status);
        Assert.Contains(eventRepo.Events, x => x.Type == "voice_speech_empty");
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
            => Task.FromResult<Tenant?>(new Tenant(tenantId, tenantId, "CoolAir HVAC", "+18777804236", "Houston TX", "America/Chicago", new BusinessHours("08:00", "18:00"), DateTimeOffset.UtcNow));

        public Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
        {
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
            _store.TryGetValue($"{tenantId}:{channel}:{phone}", out var conversation);
            return Task.FromResult(conversation);
        }

        public Task<Conversation?> GetByIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
        {
            var conversation = _store.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == conversationId);
            return Task.FromResult(conversation);
        }

        public Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetPagedAsync(string tenantId, int page, int pageSize, string? status, CancellationToken cancellationToken)
        {
            var items = _store.Values.Where(x => x.TenantId == tenantId).ToList();
            return Task.FromResult<(IReadOnlyList<Conversation> Items, int TotalCount)>((items, items.Count));
        }

        public Task<IReadOnlyList<Conversation>> GetByLastMessageAtRangeAsync(string tenantId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken)
        {
            var items = _store.Values.Where(x => x.TenantId == tenantId && x.LastMessageAt >= fromInclusive && x.LastMessageAt < toExclusive).ToList();
            return Task.FromResult<IReadOnlyList<Conversation>>(items);
        }

        public Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            LastSaved = conversation;
            _store[$"{conversation.TenantId}:{conversation.Channel}:{conversation.Phone}"] = conversation;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private Lead? _lead;

        public Lead? LastSaved { get; private set; }

        public Task<Lead?> GetByConversationIdAsync(string tenantId, string conversationId, CancellationToken cancellationToken)
            => Task.FromResult(_lead);

        public Task<Lead?> GetByIdAsync(string tenantId, string leadId, CancellationToken cancellationToken)
            => Task.FromResult(_lead is not null && _lead.TenantId == tenantId && _lead.Id == leadId ? _lead : null);

        public Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(string tenantId, int page, int pageSize, string? status, CancellationToken cancellationToken)
        {
            var items = _lead is null ? [] : new List<Lead> { _lead }.Where(x => x.TenantId == tenantId).ToList();
            return Task.FromResult<(IReadOnlyList<Lead> Items, int TotalCount)>((items, items.Count));
        }

        public Task<IReadOnlyList<Lead>> GetByCreatedAtRangeAsync(string tenantId, DateTimeOffset fromInclusive, DateTimeOffset toExclusive, CancellationToken cancellationToken)
        {
            var items = _lead is null
                ? []
                : new List<Lead> { _lead }.Where(x => x.TenantId == tenantId && x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive).ToList();
            return Task.FromResult<IReadOnlyList<Lead>>(items);
        }

        public Task UpsertAsync(Lead lead, CancellationToken cancellationToken)
        {
            _lead = lead;
            LastSaved = lead;
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

    private sealed class FakeVoiceSessionRepository : IVoiceSessionRepository
    {
        private readonly Dictionary<string, VoiceSession> _store = new();

        public VoiceSession? LastSaved { get; private set; }

        public Task<VoiceSession?> GetByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken)
        {
            _store.TryGetValue(providerCallId, out var voiceSession);
            return Task.FromResult(voiceSession);
        }

        public Task UpsertAsync(VoiceSession voiceSession, CancellationToken cancellationToken)
        {
            LastSaved = voiceSession;
            _store[voiceSession.ProviderCallId] = voiceSession;
            return Task.CompletedTask;
        }
    }
}
