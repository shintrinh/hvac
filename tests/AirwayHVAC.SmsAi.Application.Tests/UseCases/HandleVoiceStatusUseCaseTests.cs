using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AirwayHVAC.SmsAi.Application.Tests.UseCases;

public sealed class HandleVoiceStatusUseCaseTests
{
    [Fact]
    public void Constructor_DependenciesProvided_CreatesUseCase()
    {
        var voiceSessionRepository = new Mock<IVoiceSessionRepository>(MockBehavior.Strict);
        var eventRepository = new Mock<IEventRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<HandleVoiceStatusUseCase>>();

        var sut = new HandleVoiceStatusUseCase(
            voiceSessionRepository.Object,
            new EventService(eventRepository.Object),
            logger.Object);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task ExecuteAsync_SessionNotFound_LogsWarningAndDoesNotPersistOrTrackEvent()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var voiceSessionRepository = new Mock<IVoiceSessionRepository>(MockBehavior.Strict);
        var eventRepository = new Mock<IEventRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<HandleVoiceStatusUseCase>>();
        var command = new HandleVoiceStatusCommand("CA_MISSING", "completed");

        voiceSessionRepository
            .Setup(x => x.GetByProviderCallIdAsync("CA_MISSING", cancellationToken))
            .ReturnsAsync((VoiceSession?)null);

        var sut = new HandleVoiceStatusUseCase(
            voiceSessionRepository.Object,
            new EventService(eventRepository.Object),
            logger.Object);

        await sut.ExecuteAsync(command, cancellationToken);

        voiceSessionRepository.Verify(x => x.GetByProviderCallIdAsync("CA_MISSING", cancellationToken), Times.Once);
        voiceSessionRepository.Verify(x => x.UpsertAsync(It.IsAny<VoiceSession>(), It.IsAny<CancellationToken>()), Times.Never);
        eventRepository.Verify(x => x.AddAsync(It.IsAny<EventLog>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("session was not found", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, "completed", "voice_call_completed")]
    [InlineData("", "completed", "voice_call_completed")]
    [InlineData("   ", "completed", "voice_call_completed")]
    [InlineData(" Completed ", "completed", "voice_call_completed")]
    [InlineData("BUSY", "busy", "voice_call_completed")]
    [InlineData(" no-answer ", "no-answer", "voice_call_completed")]
    [InlineData("Canceled", "canceled", "voice_call_completed")]
    [InlineData("failed", "failed", "voice_call_failed")]
    [InlineData("in-progress", "in-progress", "voice_call_failed")]
    public async Task ExecuteAsync_SessionExists_CompletesSessionAndTracksExpectedEvent(string? callStatus, string expectedStatus, string expectedEventType)
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var voiceSession = CreateVoiceSession();
        var voiceSessionRepository = new Mock<IVoiceSessionRepository>(MockBehavior.Strict);
        var eventRepository = new Mock<IEventRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<HandleVoiceStatusUseCase>>();
        var command = new HandleVoiceStatusCommand("CA_STATUS", callStatus!);
        VoiceSession? savedVoiceSession = null;
        EventLog? savedEvent = null;

        voiceSessionRepository
            .Setup(x => x.GetByProviderCallIdAsync("CA_STATUS", cancellationToken))
            .ReturnsAsync(voiceSession);
        voiceSessionRepository
            .Setup(x => x.UpsertAsync(voiceSession, cancellationToken))
            .Callback<VoiceSession, CancellationToken>((saved, _) => savedVoiceSession = saved)
            .Returns(Task.CompletedTask);
        eventRepository
            .Setup(x => x.AddAsync(It.IsAny<EventLog>(), cancellationToken))
            .Callback<EventLog, CancellationToken>((eventLog, _) => savedEvent = eventLog)
            .Returns(Task.CompletedTask);

        var sut = new HandleVoiceStatusUseCase(
            voiceSessionRepository.Object,
            new EventService(eventRepository.Object),
            logger.Object);
        var before = DateTimeOffset.UtcNow;

        await sut.ExecuteAsync(command, cancellationToken);

        var after = DateTimeOffset.UtcNow;
        Assert.Same(voiceSession, savedVoiceSession);
        Assert.Equal(expectedStatus, voiceSession.Status);
        Assert.NotNull(voiceSession.EndedAt);
        Assert.True(voiceSession.EndedAt >= before);
        Assert.True(voiceSession.EndedAt <= after);
        Assert.Equal(voiceSession.EndedAt, voiceSession.LastActivityAt);
        Assert.NotNull(savedEvent);
        Assert.Equal("tenant_123", savedEvent.TenantId);
        Assert.Equal(expectedEventType, savedEvent.Type);
        Assert.Equal("+15555550123", savedEvent.Phone);
        Assert.Equal("conversation_123", savedEvent.ConversationId);
        Assert.Equal(voiceSession.EndedAt, savedEvent.Timestamp);
        voiceSessionRepository.Verify(x => x.GetByProviderCallIdAsync("CA_STATUS", cancellationToken), Times.Once);
        voiceSessionRepository.Verify(x => x.UpsertAsync(voiceSession, cancellationToken), Times.Once);
        eventRepository.Verify(x => x.AddAsync(It.IsAny<EventLog>(), cancellationToken), Times.Once);
    }

    private static VoiceSession CreateVoiceSession()
        => new(
            "voice_123",
            "tenant_123",
            "conversation_123",
            "twilio",
            "CA_STATUS",
            "+15555550123",
            "+18777804236",
            "in-progress",
            ConversationStage.Issue,
            0,
            0,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(-5));
}
