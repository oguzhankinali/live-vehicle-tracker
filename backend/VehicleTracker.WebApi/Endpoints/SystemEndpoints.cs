using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;

namespace VehicleTracker.WebApi.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var systemGroup = app.MapGroup("/api/system").WithTags("System");

        systemGroup.MapGet("/status", ([FromServices] ISystemMetricsService metrics) =>
        {
            var status = metrics.GetStatus();
            return Results.Ok(status);
        })
        .WithName("GetSystemStatus")
        .WithSummary("Sistem durumu, son basarili okuma, saatlik yazim ve hata metriklerini doner.");

        return app;
    }
}