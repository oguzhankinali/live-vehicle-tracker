using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Infrastructure.Services;

public sealed class EnturVehicleReferenceService : IVehicleReferenceService
{
    private const string Endpoint =
        "https://api.entur.io/journey-planner/v3/graphql";

    private static readonly TimeSpan CacheDuration =
        TimeSpan.FromHours(12);

    private const string LineQuery = """
        query ($id: ID!) {
          line(id: $id) {
            id
            publicCode
            name
            transportMode
            operator { name }
          }
        }
        """;

    private const string QuayQuery = """
        query ($id: String!) {
          quay(id: $id) {
            id
            name
            stopPlace { name }
          }
        }
        """;

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EnturVehicleReferenceService> _logger;

    public EnturVehicleReferenceService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<EnturVehicleReferenceService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VehicleReferenceDto> GetAsync(
        string? routeId,
        string? stopId,
        CancellationToken cancellationToken = default)
    {
        var lineTask = GetLineAsync(routeId, cancellationToken);
        var stopTask = GetStopAsync(stopId, cancellationToken);

        await Task.WhenAll(lineTask, stopTask);

        return new VehicleReferenceDto(
            await lineTask,
            await stopTask);
    }

    private async Task<LineReferenceDto?> GetLineAsync(
        string? routeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return null;

        var cacheKey = $"entur:line:{routeId}";

        if (_cache.TryGetValue(
                cacheKey, out LineReferenceDto? cached))
        {
            return cached;
        }

        var element = await QueryAsync(
            LineQuery,
            routeId,
            "line",
            cancellationToken);

        if (element is null)
            return null;

        var line = element.Value;

        var result = new LineReferenceDto(
            GetString(line, "id") ?? routeId,
            GetString(line, "publicCode"),
            GetString(line, "name"),
            GetString(line, "transportMode"),
            line.TryGetProperty("operator", out var operatorElement)
                && operatorElement.ValueKind == JsonValueKind.Object
                ? GetString(operatorElement, "name")
                : null);

        _cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    private async Task<StopReferenceDto?> GetStopAsync(
        string? stopId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stopId))
            return null;

        var cacheKey = $"entur:quay:{stopId}";

        if (_cache.TryGetValue(
                cacheKey, out StopReferenceDto? cached))
        {
            return cached;
        }

        var element = await QueryAsync(
            QuayQuery,
            stopId,
            "quay",
            cancellationToken);

        if (element is null)
            return null;

        var quay = element.Value;
        var name = GetString(quay, "name");

        if (string.IsNullOrWhiteSpace(name))
            return null;

        string? stopPlaceName = null;

        if (quay.TryGetProperty("stopPlace", out var stopPlace)
            && stopPlace.ValueKind == JsonValueKind.Object)
        {
            stopPlaceName = GetString(stopPlace, "name");
        }

        var result = new StopReferenceDto(
            GetString(quay, "id") ?? stopId,
            name,
            stopPlaceName);

        _cache.Set(cacheKey, result, CacheDuration);

        return result;
    }

    private async Task<JsonElement?> QueryAsync(
        string query,
        string id,
        string fieldName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    query,
                    variables = new { id }
                })
            };

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(
                    cancellationToken),
                cancellationToken: cancellationToken);

            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                _logger.LogWarning(
                    "Entur GraphQL hatasi. Alan={Field}, Id={Id}",
                    fieldName,
                    id);

                return null;
            }

            if (!root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty(fieldName, out var item)
                || item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return item.Clone();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is HttpRequestException
                or TaskCanceledException
                or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Entur referans verisi alinamadi. Alan={Field}, Id={Id}",
                fieldName,
                id);

            return null;
        }
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
