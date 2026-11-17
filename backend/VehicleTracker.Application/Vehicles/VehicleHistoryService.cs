using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Vehicles;

public sealed class VehicleHistoryService : IVehicleHistoryService
{
    private readonly IVehicleHistoryRepository _repository;
    private readonly VehicleHistoryQueryValidator _validator;
    private readonly IngestionOptions _options;
    private readonly TimeProvider _timeProvider;

    public VehicleHistoryService(
        IVehicleHistoryRepository repository,
        VehicleHistoryQueryValidator validator,
        IngestionOptions options,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task<PagedResult<VehiclePositionRecord>> GetHistoryAsync(
        VehicleHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = _validator.ValidateAndNormalize(
            query,
            _options.Retention,
            _timeProvider.GetUtcNow().UtcDateTime);

        return _repository.GetHistoryAsync(
            normalizedQuery,
            cancellationToken);
    }
}
