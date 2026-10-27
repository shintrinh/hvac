using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class ConversationOrchestrator
{
    private readonly ConversationService _conversationService;
    private readonly LeadService _leadService;
    private readonly EventService _eventService;
    private readonly ConversationFlowEngine _flowEngine;
    private readonly ILogger<ConversationOrchestrator> _logger;

    public ConversationOrchestrator(
        ConversationService conversationService,
        LeadService leadService,
        EventService eventService,
        ConversationFlowEngine flowEngine,
        ILogger<ConversationOrchestrator> logger)
    {
        _conversationService = conversationService;
        _leadService = leadService;
        _eventService = eventService;
        _flowEngine = flowEngine;
        _logger = logger;
    }

    public async Task<ConversationOrchestrationResult> ProcessCustomerMessageAsync(
        AirwayHVAC.SmsAi.Domain.Entities.Tenant tenant,
        string customerPhoneNumber,
        string messageBody,
        ConversationChannel channel,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationService.LoadOrCreateAsync(tenant.TenantId, customerPhoneNumber, channel, timestamp, cancellationToken);
        var previousStage = conversation.Stage;
        var channelName = channel.ToString().ToLowerInvariant();

        conversation.AppendCustomerMessage(messageBody, timestamp);
        await _eventService.TrackAsync(tenant.TenantId, $"{channelName}_received", customerPhoneNumber, conversation.Id, timestamp, cancellationToken);

        var flowResult = _flowEngine.ProcessCustomerMessage(conversation, messageBody, channel);
        var reply = flowResult.ReplyMessage;

        conversation.AppendAiMessage(reply, DateTimeOffset.UtcNow);

        var (lead, created) = await _leadService.CreateOrUpdateFromConversationAsync(conversation, cancellationToken);
        conversation.AttachLead(lead.Id);

        await _conversationService.SaveAsync(conversation, cancellationToken);

        if (created)
        {
            await _eventService.TrackAsync(tenant.TenantId, "lead_created", customerPhoneNumber, conversation.Id, DateTimeOffset.UtcNow, cancellationToken);
        }

        if (previousStage != conversation.Stage)
        {
            await _eventService.TrackAsync(tenant.TenantId, "stage_changed", customerPhoneNumber, conversation.Id, DateTimeOffset.UtcNow, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(lead.PreferredTime))
        {
            await _eventService.TrackAsync(tenant.TenantId, "appointment_requested", customerPhoneNumber, conversation.Id, DateTimeOffset.UtcNow, cancellationToken);
        }

        if (conversation.Stage == ConversationStage.Completed)
        {
            await _eventService.TrackAsync(tenant.TenantId, "booking_confirmed", customerPhoneNumber, conversation.Id, DateTimeOffset.UtcNow, cancellationToken);
        }

        _logger.LogInformation(
            "{Channel} message processed for tenant {TenantId}, conversation {ConversationId}, lead {LeadId}, stage {Stage}",
            channel,
            tenant.TenantId,
            conversation.Id,
            lead.Id,
            conversation.Stage);

        return new ConversationOrchestrationResult(conversation.Id, lead.Id, reply, conversation.Stage, conversation.Channel);
    }
}

public sealed record ConversationOrchestrationResult(
    string ConversationId,
    string LeadId,
    string ReplyMessage,
    ConversationStage Stage,
    ConversationChannel Channel);
