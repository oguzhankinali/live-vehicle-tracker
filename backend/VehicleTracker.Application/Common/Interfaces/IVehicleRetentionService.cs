namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleRetentionService
{
    // Belirtilen süreden eski TimescaleDB chunk'larını temizler, geriye silinen chunk sayısını döner.
    Task<int> DropStaleChunksAsync(TimeSpan retentionWindow, CancellationToken cancellationToken = default);
}