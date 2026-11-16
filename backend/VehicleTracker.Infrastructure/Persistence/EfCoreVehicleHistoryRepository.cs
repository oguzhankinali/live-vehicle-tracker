using Microsoft.EntityFrameworkCore;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class EfCoreVehicleHistoryRepository
    : IVehicleHistoryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EfCoreVehicleHistoryRepository(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<VehiclePositionRecord>> GetHistoryAsync(
        VehicleHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var from = query.FromUtc
            ?? throw new InvalidOperationException(
                "History start time was not normalized.");

        var to = query.ToUtc
            ?? throw new InvalidOperationException(
                "History end time was not normalized.");

        var filteredQuery = _dbContext.VehiclePositions
            .AsNoTracking()
            .Where(position =>
                position.VehicleId == query.VehicleId &&
                position.Timestamp >= from &&
                position.Timestamp <= to);

        var totalCount = await filteredQuery.CountAsync(
            cancellationToken);

        var items = await filteredQuery
            .OrderByDescending(position => position.Timestamp)
            .ThenBy(position => position.FeedEntityId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<VehiclePositionRecord>(
            items,
            totalCount,
            query.Page,
            query.PageSize);
    }
}
