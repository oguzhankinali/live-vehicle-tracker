namespace VehicleTracker.WebApi.Services;

public interface IVehicleClientRegistry
{
    void AddClient(string connectionId);

    void RemoveClient(string connectionId);
}
