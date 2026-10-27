using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed class HandleInboundSmsUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IOutboundSmsService _outboundSmsService;
    private readonly EventService _eventService;
    private readonly ConversationOrchestrator _orchestrator;
    private readonly ILogger<HandleInboundSmsUseCase> _logger;

    public HandleInboundSmsUseCase(
        ITenantRepository tenantRepository,
        IOutboundSmsService outboundSmsService,
        ConversationService conversationService,
        LeadService leadService,
        EventService eventService,
        ConversationFlowEngine flowEngine,
        ILogger<HandleInboundSmsUseCase> logger)
    {
        _tenantRepository = tenantRepository;
        _outboundSmsService = outboundSmsService;
        _eventService = eventService;
        _orchestrator = new ConversationOrchestrator(conversationService, leadService, eventService, flowEngine, NullLogger<ConversationOrchestrator>.Instance);
        _logger = logger;
    }

    public HandleInboundSmsUseCase(
        ITenantRepository tenantRepository,
        IOutboundSmsService outboundSmsService,
        ConversationOrchestrator orchestrator,
        EventService eventService,
        ILogger<HandleInboundSmsUseCase> logger)
    {
        _tenantRepository = tenantRepository;
        _outboundSmsService = outboundSmsService;
        _orchestrator = orchestrator;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<HandleInboundSmsResult> ExecuteAsync(HandleInboundSmsCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = await ResolveTenantAsync(command, cancellationToken);

        var result = await _orchestrator.ProcessCustomerMessageAsync(tenant, command.FromPhoneNumber, command.MessageBody, ConversationChannel.Sms, now, cancellationToken);

        await _outboundSmsService.SendAsync(tenant.TenantId, command.FromPhoneNumber, result.ReplyMessage, cancellationToken);
        await _eventService.TrackAsync(tenant.TenantId, "sms_sent", command.FromPhoneNumber, result.ConversationId, DateTimeOffset.UtcNow, cancellationToken);

        _logger.LogInformation("Inbound SMS processed for tenant {TenantId}, conversation {ConversationId}, lead {LeadId}, stage {Stage}", tenant.TenantId, result.ConversationId, result.LeadId, result.Stage);

        return new HandleInboundSmsResult(result.ConversationId, result.LeadId, result.ReplyMessage);
    }

    private async Task<AirwayHVAC.SmsAi.Domain.Entities.Tenant> ResolveTenantAsync(HandleInboundSmsCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.TenantId))
        {
            return await _tenantRepository.GetByTenantIdAsync(command.TenantId, cancellationToken)
                ?? throw new InvalidOperationException($"Tenant not found for tenant id {command.TenantId}");
        }

        if (!string.IsNullOrWhiteSpace(command.TenantPhoneNumber))
        {
            var tenant = await _tenantRepository.GetByPhoneNumberAsync(command.TenantPhoneNumber, cancellationToken);
            if (tenant is not null)
            {
                return tenant;
            }

            var normalizedPhone = NormalizePhoneForLookup(command.TenantPhoneNumber);
            if (!string.Equals(normalizedPhone, command.TenantPhoneNumber, StringComparison.Ordinal))
            {
                tenant = await _tenantRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
                if (tenant is not null)
                {
                    return tenant;
                }
            }

            throw new InvalidOperationException($"Tenant not found for Twilio number {command.TenantPhoneNumber}");
        }

        throw new InvalidOperationException("Tenant routing decision is missing both tenant id and phone mapping.");
    }

    private static string NormalizePhoneForLookup(string phoneNumber)
        => phoneNumber.Replace("+", string.Empty, StringComparison.Ordinal).Trim();
}
