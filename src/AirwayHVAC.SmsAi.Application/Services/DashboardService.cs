using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Domain.Interfaces;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class DashboardService
{
    private static readonly Dictionary<LeadStatus, LeadStatus> AllowedTransitions = new()
    {
        [LeadStatus.New] = LeadStatus.Contacted,
        [LeadStatus.Contacted] = LeadStatus.Booked,
        [LeadStatus.Booked] = LeadStatus.Closed,
    };

    private readonly IConversationRepository _conversationRepository;
    private readonly ILeadRepository _leadRepository;

    public DashboardService(IConversationRepository conversationRepository, ILeadRepository leadRepository)
    {
        _conversationRepository = conversationRepository;
        _leadRepository = leadRepository;
    }

    public Task<(IReadOnlyList<Conversation> Items, int TotalCount)> GetConversationsAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken)
        => _conversationRepository.GetPagedAsync(tenantId, page, pageSize, status, cancellationToken);

    public async Task<(IReadOnlyList<Message> Items, int TotalCount)> GetMessagesAsync(
        string tenantId,
        string conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(tenantId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ([], 0);
        }

        var ordered = conversation.Messages.OrderBy(x => x.Timestamp).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, ordered.Count);
    }

    public Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetLeadsAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken)
        => _leadRepository.GetPagedAsync(tenantId, page, pageSize, status, cancellationToken);

    public async Task<Lead> UpdateLeadStatusAsync(string tenantId, string leadId, LeadStatus targetStatus, CancellationToken cancellationToken)
    {
        var lead = await _leadRepository.GetByIdAsync(tenantId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found.");

        if (lead.Status == targetStatus)
        {
            return lead;
        }

        if (!AllowedTransitions.TryGetValue(lead.Status, out var expectedNext) || expectedNext != targetStatus)
        {
            throw new InvalidOperationException($"Invalid lead status transition from '{lead.Status}' to '{targetStatus}'.");
        }

        lead.SetStatus(targetStatus);
        await _leadRepository.UpsertAsync(lead, cancellationToken);
        return lead;
    }

    public async Task<DashboardAnalytics> GetAnalyticsAsync(
        string tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("'to' must be greater than or equal to 'from'.");
        }

        var allConversations = await _conversationRepository.GetPagedAsync(tenantId, 1, 1, null, cancellationToken);
        var allLeads = await _leadRepository.GetPagedAsync(tenantId, 1, 1, null, cancellationToken);
        var bookedLeads = await _leadRepository.GetPagedAsync(tenantId, 1, 1, LeadStatus.Booked.ToString().ToLowerInvariant(), cancellationToken);

        var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(to.AddDays(1).Year, to.AddDays(1).Month, to.AddDays(1).Day, 0, 0, 0, DateTimeKind.Utc);

        var leadsInRange = await _leadRepository.GetByCreatedAtRangeAsync(tenantId, startUtc, endUtc, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStartUtc = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
        var todayEndUtc = new DateTime(today.AddDays(1).Year, today.AddDays(1).Month, today.AddDays(1).Day, 0, 0, 0, DateTimeKind.Utc);
        var newLeadsToday = await _leadRepository.GetByCreatedAtRangeAsync(tenantId, todayStartUtc, todayEndUtc, cancellationToken);

        var conversationSamples = await _conversationRepository.GetByLastMessageAtRangeAsync(tenantId, startUtc, endUtc, cancellationToken);

        var leadsPerDay = leadsInRange
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime))
            .OrderBy(x => x.Key)
            .Select(x => new LeadsPerDayPoint(x.Key, x.Count()))
            .ToList();

        var conversionRate = allLeads.TotalCount == 0
            ? 0d
            : (double)bookedLeads.TotalCount / allLeads.TotalCount;

        var averageResponseTimeSeconds = CalculateAverageResponseSeconds(conversationSamples);

        return new DashboardAnalytics(
            allConversations.TotalCount,
            newLeadsToday.Count,
            conversionRate,
            averageResponseTimeSeconds,
            leadsPerDay);
    }

    private static double CalculateAverageResponseSeconds(IReadOnlyList<Conversation> conversations)
    {
        var durations = new List<double>();

        foreach (var conversation in conversations)
        {
            var ordered = conversation.Messages.OrderBy(x => x.Timestamp).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].Role.Equals("customer", StringComparison.OrdinalIgnoreCase)
                    && ordered[i + 1].Role.Equals("ai", StringComparison.OrdinalIgnoreCase))
                {
                    durations.Add((ordered[i + 1].Timestamp - ordered[i].Timestamp).TotalSeconds);
                }
            }
        }

        return durations.Count == 0 ? 0d : durations.Average();
    }
}

public sealed record DashboardAnalytics(
    int TotalConversations,
    int NewLeadsToday,
    double ConversionRate,
    double AverageResponseTimeSeconds,
    IReadOnlyList<LeadsPerDayPoint> LeadsPerDay);

public sealed record LeadsPerDayPoint(DateOnly Date, int Count);
