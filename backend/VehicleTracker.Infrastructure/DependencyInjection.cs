using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Infrastructure.Options;
using VehicleTracker.Infrastructure.Persistence;
using VehicleTracker.Infrastructure.Services;

namespace VehicleTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddOptions<EnturOptions>()
            .Bind(configuration.GetSection(EnturOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.FeedUrl, UriKind.Absolute, out var uri)
                          && uri.Scheme == Uri.UriSchemeHttps,
                "Entur FeedUrl must be a valid HTTPS URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientName),
                "Entur ClientName must not be empty.")
            .Validate(
                options => options.PollIntervalSeconds > 0,
                "Entur PollIntervalSeconds must be greater than zero.")
            .Validate(
                options => options.RetentionHours > 0,
                "Entur RetentionHours must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IVehicleFeedService, EnturVehicleFeedService>();

        return services;
    }
}
