using VehicleTracker.Infrastructure.Services;
using Xunit;

namespace VehicleTracker.Application.Tests;

public class SystemMetricsServiceTests
{
    [Fact]
    public void RecordSuccessfulPoll_WithZeroRecords_UpdatesLastSuccessfulPollUtc()
    {
        // Arrange
        var metricsService = new SystemMetricsService();
        var initialStatus = metricsService.GetStatus();
        Assert.Null(initialStatus.LastSuccessfulPollUtc);

        // Act
        metricsService.RecordSuccessfulPoll(0); // Duplicate payload durumu

        // Assert
        var updatedStatus = metricsService.GetStatus();
        Assert.NotNull(updatedStatus.LastSuccessfulPollUtc);
        Assert.Equal(0, updatedStatus.RecordsWrittenLastHour);
    }
}