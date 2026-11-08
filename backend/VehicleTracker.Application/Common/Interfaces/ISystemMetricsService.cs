using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Common.Interfaces;

public interface ISystemMetricsService
{
    void RecordSuccessfulPoll(int writtenCount);
    void RecordFailedPoll();
    void IncrementConnectedClients();
    void DecrementConnectedClients();
    void RecordRecordsWritten(int count);
    SystemStatusDto GetStatus();
}