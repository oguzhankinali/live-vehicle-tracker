using Microsoft.OpenApi;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.WebApi.Services;

namespace VehicleTracker.WebApi.Configuration;

public static class WebApiExtensions
{
    public static IServiceCollection AddWebApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Live Vehicle Tracker API",
                Version = "v1",
                Description = "Entur GTFS-Realtime Canli Arac Takip Platformu"
            });
        });

        var allowedOrigins = GetAllowedOrigins(configuration);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 32768;
            options.StreamBufferCapacity = 10;
        });

        services.AddSingleton<SignalRVehiclePublisher>();

        services.AddSingleton<IVehiclePublisher>(provider =>
            provider.GetRequiredService<SignalRVehiclePublisher>());

        return services;
    }

    public static WebApplication UseWebApiMiddleware(
        this WebApplication app)
    {
        var allowedOrigins = GetAllowedOrigins(app.Configuration);

        app.UseCors();

        var webSocketOptions = new WebSocketOptions();

        foreach (var origin in allowedOrigins)
        {
            webSocketOptions.AllowedOrigins.Add(origin);
        }

        app.UseWebSockets(webSocketOptions);

        var swaggerEnabled =
            app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Swagger:Enabled");

        if (swaggerEnabled)
        {
            app.UseSwagger(options =>
            {
                options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            });

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint(
                    "/swagger/v1/swagger.json",
                    "Live Vehicle Tracker API v1");

                options.RoutePrefix = "swagger";
            });

            app.MapGet("/", () => Results.Redirect("/swagger"))
                .ExcludeFromDescription();
        }

        return app;
    }

    private static string[] GetAllowedOrigins(
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain at least one trusted origin.");
        }

        foreach (var origin in allowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                origin != uri.GetLeftPart(UriPartial.Authority))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins contains an invalid origin.");
            }
        }

        return allowedOrigins;
    }
}
