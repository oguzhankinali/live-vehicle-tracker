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
            "NSR:Quay:33333",
            null);

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

        Assert.Null(result.Trip);
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
            "NSR:Quay:33333",
            null);

        await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333",
            null);

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
            "NSR:Quay:33333",
            null);

        Assert.Null(result.Line);
        Assert.Null(result.Stop);
        Assert.Null(result.Trip);
    }

    [Fact]
    public async Task GetAsync_WithMissingIds_DoesNotCallEntur()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(null, null, null);

        Assert.Null(result.Line);
        Assert.Null(result.Stop);
        Assert.Null(result.Trip);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_MapsServiceJourneyInformation()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(
            null,
            null,
            "FIN:ServiceJourney:5726");

        Assert.Null(result.Line);
        Assert.Null(result.Stop);

        Assert.NotNull(result.Trip);
        Assert.Equal(
            "FIN:ServiceJourney:5726",
            result.Trip.Id);
        Assert.Null(result.Trip.PublicCode);
        Assert.Equal(
            "Øksfjord ferjekai",
            result.Trip.Origin);
        Assert.Equal("Hasvik", result.Trip.Destination);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_UsesCacheForRepeatedTrips()
    {
        var handler = new FakeEnturHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        await service.GetAsync(
            null,
            null,
            "FIN:ServiceJourney:5726");

        await service.GetAsync(
            null,
            null,
            "FIN:ServiceJourney:5726");

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_MissingTrip_PreservesLineAndStop()
    {
        var handler = new FakeEnturHandler(
            missingTrip: true);

        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = CreateService(httpClient, cache);

        var result = await service.GetAsync(
            "VKT:Line:54",
            "NSR:Quay:33333",
            "FIN:ServiceJourney:5726");

        Assert.NotNull(result.Line);
        Assert.Equal("54", result.Line.PublicCode);

        Assert.NotNull(result.Stop);
        Assert.Equal("Kilen øst", result.Stop.Name);

        Assert.Null(result.Trip);
        Assert.Equal(3, handler.RequestCount);
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
        private readonly bool _missingTrip;
        private int _requestCount;

        public int RequestCount =>
            Volatile.Read(ref _requestCount);

        public FakeEnturHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            bool missingTrip = false)
        {
            _statusCode = statusCode;
            _missingTrip = missingTrip;
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

            var isTripQuery = requestBody.Contains(
                "serviceJourney(id:",
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

            string json;

            if (isLineQuery)
            {
                json = """
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
                    """;
            }
            else if (isTripQuery)
            {
                json = _missingTrip
                    ? """
                      {
                        "data": {
                          "serviceJourney": null
                        }
                      }
                      """
                    : """
                      {
                        "data": {
                          "serviceJourney": {
                            "id": "FIN:ServiceJourney:5726",
                            "publicCode": null,
                            "passingTimes": [
                              {
                                "quay": {
                                  "name": "Øksfjord ferjekai"
                                },
                                "destinationDisplay": {
                                  "frontText": "Hasvik"
                                }
                              },
                              {
                                "quay": {
                                  "name": "Hasvik kai"
                                },
                                "destinationDisplay": {
                                  "frontText": "Hasvik"
                                }
                              }
                            ]
                          }
                        }
                      }
                      """;
            }
            else
            {
                Assert.Contains(
                    "quay(id:",
                    requestBody);

                json = """
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
            }

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
