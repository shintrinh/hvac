using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Interfaces;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class EventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public Task TrackAsync(string tenantId, string type, string phone, string conversationId, DateTimeOffset timestamp, CancellationToken cancellationToken)
        => _eventRepository.AddAsync(EventLog.Create(tenantId, type, phone, conversationId, timestamp), cancellationToken);
}
