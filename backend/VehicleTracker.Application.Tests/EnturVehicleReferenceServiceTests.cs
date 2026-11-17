using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleTracker.Infrastructure.Services;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class EnturVehicleReferenceServiceTests
{
    [Fact]
    public async Task GetAsync_MapsLineAndStopNames()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333");

        Assert.NotNull(result.Line);
        Assert.Equal("54", result.Line.PublicCode);
        Assert.Equal(
            "Kaldnes-Tønsberg-Skallev",
            result.Line.Name);
        Assert.Equal(
            "Tide avd. Vestfold",
            result.Line.OperatorName);

        Assert.NotNull(result.Stop);
        Assert.Equal("Kilen øst", result.Stop.Name);
        Assert.Equal(
            "Kilen øst",
            result.Stop.StopPlaceName);

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_UsesCacheForRepeatedIdentifiers()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333");

        await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333");

        // Iki ayri referans icin toplam iki HTTP istegi.
        // Ikinci GetAsync yeni istek olusturmamali.
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_HandlesUnavailableEntur()
    {
        var handler = new FakeEnturHandler(
            HttpStatusCode.ServiceUnavailable);

        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333");

        Assert.Null(result.Line);
        Assert.Null(result.Stop);
    }

    [Fact]
    public async Task GetAsync_WithMissingIds_DoesNotCallEntur()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(null, null);

        Assert.Null(result.Line);
        Assert.Null(result.Stop);
        Assert.Equal(0, handler.RequestCount);
    }

    private static EnturVehicleReferenceService CreateService(
        HttpClient httpClient,
        IMemoryCache cache)
    {
        return new EnturVehicleReferenceService(
            httpClient,
            cache,
            NullLogger<EnturVehicleReferenceService>.Instance);
    }

    private sealed class FakeEnturHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private int _requestCount;

        public int RequestCount =>
            Volatile.Read(ref _requestCount);

        public FakeEnturHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            Assert.Equal(HttpMethod.Post, request.Method);

            Assert.Equal(
                "https://api.entur.io/journey-planner/v3/graphql",
                request.RequestUri?.ToString());

            var requestBody = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            var isLineQuery = requestBody.Contains(
                "line(id:",
                StringComparison.Ordinal);
            if (isLineQuery)
                {
                    Assert.Contains(
                        "query ($id: ID!)",
                        requestBody);
                }
                else
                {
                    Assert.Contains(
                        "query ($id: String!)",
                        requestBody);
                }

            var json = isLineQuery
                ? """
                  {
                    "data": {
                      "line": {
                        "id": "VKT:Line:54",
                        "publicCode": "54",
                        "name": "Kaldnes-Tønsberg-Skallev",
                        "transportMode": "bus",
                        "operator": {
                          "name": "Tide avd. Vestfold"
                        }
                      }
                    }
                  }
                  """
                : """
                  {
                    "data": {
                      "quay": {
                        "id": "NSR:Quay:33333",
                        "name": "Kilen øst",
                        "stopPlace": {
                          "name": "Kilen øst"
                        }
                      }
                    }
                  }
                  """;

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
