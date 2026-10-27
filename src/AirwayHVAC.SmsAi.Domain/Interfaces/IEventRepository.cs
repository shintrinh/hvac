using AirwayHVAC.SmsAi.Domain.Entities;

namespace AirwayHVAC.SmsAi.Domain.Interfaces;

public interface IEventRepository
{
    Task AddAsync(EventLog eventLog, CancellationToken cancellationToken);
}
