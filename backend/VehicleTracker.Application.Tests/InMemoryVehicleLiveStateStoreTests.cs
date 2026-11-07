using VehicleTracker.Domain.Entities;
using VehicleTracker.Infrastructure.Persistence;
using Xunit;

namespace VehicleTracker.Application.Tests;

public class InMemoryVehicleLiveStateStoreTests
{
    [Fact]
    public void PruneStaleVehicles_DoesNotDeleteNewerRecords_WhenUpdatedConcurrently()
    {
        // Arrange
        var store = new InMemoryVehicleLiveStateStore();
        var oldTime = DateTime.UtcNow.AddHours(-4);
        var newTime = DateTime.UtcNow;
        var threshold = DateTime.UtcNow.AddHours(-3);

        // Başlangıçta eski bir kayıt ekle
        store.Update(new[] { new VehiclePositionRecord { VehicleId = "V1", Timestamp = oldTime } });

        // Act
        // Prune işlemi eski kaydı silmeye çalışırken, aynı anda Update işlemi yeni kaydı yazıyor
        Parallel.Invoke(
            () => store.Update(new[] { new VehiclePositionRecord { VehicleId = "V1", Timestamp = newTime } }),
            () => store.PruneStaleVehicles(threshold)
        );

        // Assert
        var latest = store.GetLatestByVehicleId("V1");
        Assert.NotNull(latest);
        Assert.Equal(newTime, latest.Timestamp); // Yeni kayıt silinmemiş olmalı!
    }
}