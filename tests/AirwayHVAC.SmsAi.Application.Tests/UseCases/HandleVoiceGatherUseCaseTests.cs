using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.UseCases;

public sealed class HandleVoiceGatherUseCaseTests
{
    [Theory]
    [InlineData("I need to talk to a human")]
    [InlineData("Please get me an agent")]
    [InlineData("Can I speak with a representative?")]
    [InlineData("Connect me to an operator")]
    [InlineData("I want a real person")]
    public async Task ExecuteAsync_SpeechRequestsHandoff_RequestsHandoffAndTracksEvent(string speechResult)
    {
        var cancellationToken = CancellationToken.None;
        var voiceSession = CreateVoiceSession();
        var tenantRepository = new Mock<ITenantRepository>(MockBehavior.Strict);
        var voiceSessionRepository = new Mock<IVoiceSessionRepository>(MockBehavior.Strict);
        var eventRepository = new Mock<IEventRepository>(MockBehavior.Strict);
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var leadRepository = new Mock<ILeadRepository>(MockBehavior.Strict);

        voiceSessionRepository
            .Setup(x => x.GetByProviderCallIdAsync("CA_TEST_HANDOFF", cancellationToken))
            .ReturnsAsync(voiceSession);
        voiceSessionRepository
            .Setup(x => x.UpsertAsync(voiceSession, cancellationToken))
            .Returns(Task.CompletedTask);
        eventRepository
            .Setup(x => x.AddAsync(
                It.Is<EventLog>(e =>
                    e.TenantId == "tenant_123" &&
                    e.Type == "voice_handoff_requested" &&
                    e.Phone == "+15555550123" &&
                    e.ConversationId == "conversation_123"),
                cancellationToken))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            tenantRepository,
            voiceSessionRepository,
            eventRepository,
            conversationRepository,
            leadRepository);

        var result = await sut.ExecuteAsync(new HandleVoiceGatherCommand("CA_TEST_HANDOFF", speechResult, 0.98m), cancellationToken);

        Assert.Equal("conversation_123", result.ConversationId);
        Assert.Equal("voice_CA_TEST_HANDOFF", result.VoiceSessionId);
        Assert.Equal("Please hold while I connect you to our team.", result.ReplyMessage);
        Assert.Equal(ConversationStage.Address, result.Stage);
        Assert.False(result.Completed);
        Assert.True(result.HandoffRequested);
        Assert.Equal("customer_requested_human", result.HandoffReason);
        Assert.Equal("handoff", voiceSession.Status);
        Assert.Equal("customer_requested_human", voiceSession.HandoffReason);
        voiceSessionRepository.Verify(x => x.UpsertAsync(voiceSession, cancellationToken), Times.Once);
        eventRepository.VerifyAll();
        tenantRepository.VerifyNoOtherCalls();
        conversationRepository.VerifyNoOtherCalls();
        leadRepository.VerifyNoOtherCalls();
    }

    private static HandleVoiceGatherUseCase CreateSut(
        Mock<ITenantRepository> tenantRepository,
        Mock<IVoiceSessionRepository> voiceSessionRepository,
        Mock<IEventRepository> eventRepository,
        Mock<IConversationRepository> conversationRepository,
        Mock<ILeadRepository> leadRepository)
    {
        var eventService = new EventService(eventRepository.Object);
        var conversationService = new ConversationService(
            conversationRepository.Object,
            new Mock<ILogger<ConversationService>>().Object);
        var orchestrator = new ConversationOrchestrator(
            conversationService,
            new LeadService(leadRepository.Object),
            eventService,
            new ConversationFlowEngine(),
            new Mock<ILogger<ConversationOrchestrator>>().Object);

        return new HandleVoiceGatherUseCase(
            tenantRepository.Object,
            voiceSessionRepository.Object,
            orchestrator,
            eventService,
            new Mock<ILogger<HandleVoiceGatherUseCase>>().Object);
    }

    private static VoiceSession CreateVoiceSession()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        return new VoiceSession(
            "voice_CA_TEST_HANDOFF",
            "tenant_123",
            "conversation_123",
            "twilio",
            "CA_TEST_HANDOFF",
            "+15555550123",
            "+18777804236",
            "in-progress",
            ConversationStage.Address,
            2,
            0,
            null,
            now,
            now);
    }
}
