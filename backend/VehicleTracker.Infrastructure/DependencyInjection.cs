using Microsoft.Extensions.DependencyInjection;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Infrastructure.Services;

namespace VehicleTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpClient<IVehicleFeedService, EnturVehicleFeedService>();

        return services;
    }
}