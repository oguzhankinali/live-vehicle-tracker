using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.WebApi.Endpoints;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var vehiclesGroup = app.MapGroup("/api/vehicles").WithTags("Vehicles");

        // 1. Uç Nokta: Tüm araçların anlık en son konumu
        vehiclesGroup.MapGet("/latest", (
            [FromServices] IVehicleLiveStateStore store,
            [FromQuery] string? routeId,
            [FromQuery] string? search) =>
        {
            var filtered = VehicleFilter
                .Apply(store.GetAllLatest(), routeId, search)
                .Select(VehiclePositionDto.From)
                .ToList();

            return Results.Ok(filtered);
        })
        .WithName("GetLatestVehiclePositions")
        .WithSummary("Tum aktif araclarin anlik guncel durumlarini doner.");

        // 2. Uç Nokta: Bir aracın zaman aralığına göre sayfalanmış konum geçmişi
        vehiclesGroup.MapGet("/{vehicleId}/history", async (
            [FromRoute] string vehicleId,
            [FromServices] IVehicleHistoryService historyService,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default) =>
        {
            try
            {
                var query = new VehicleHistoryQuery(vehicleId, from, to, page, pageSize);
                var result = await historyService.GetHistoryAsync(query, ct);
                
                var dtoResult = new PagedResult<VehiclePositionDto>(
                    result.Items.Select(VehiclePositionDto.From).ToList(),
                    result.TotalCount,
                    result.Page,
                    result.PageSize);

                return Results.Ok(dtoResult);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetVehicleHistory")
        .WithSummary("Secilen aracin son 3 saatlik filtrelenebilir konum gecmisini sayfali doner.");

        return app;
    }
}