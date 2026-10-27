using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class LeadService
{
    private readonly ILeadRepository _leadRepository;

    public LeadService(ILeadRepository leadRepository)
    {
        _leadRepository = leadRepository;
    }

    public async Task<(Lead Lead, bool Created)> CreateOrUpdateFromConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        var existing = await _leadRepository.GetByConversationIdAsync(conversation.TenantId, conversation.Id, cancellationToken);

        if (existing is null)
        {
            var lead = Lead.CreateNew(conversation.TenantId, conversation.Phone, conversation.Id, DateTimeOffset.UtcNow, conversation.Channel.ToString().ToLowerInvariant());
            lead.Enrich(conversation.Issue, conversation.Address, conversation.PreferredTime);
            if (!string.IsNullOrWhiteSpace(conversation.Issue))
            {
                lead.SetStatus(LeadStatus.Contacted);
            }

            await _leadRepository.UpsertAsync(lead, cancellationToken);
            return (lead, true);
        }

        existing.Enrich(conversation.Issue, conversation.Address, conversation.PreferredTime);
        if (!string.IsNullOrWhiteSpace(existing.Address) && !string.IsNullOrWhiteSpace(existing.PreferredTime))
        {
            existing.SetStatus(LeadStatus.Booked);
        }
        else if (!string.IsNullOrWhiteSpace(existing.Issue))
        {
            existing.SetStatus(LeadStatus.Contacted);
        }

        await _leadRepository.UpsertAsync(existing, cancellationToken);
        return (existing, false);
    }
}
