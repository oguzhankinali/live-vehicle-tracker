using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleTracker.WebApi.ExceptionHandling;
using Xunit;

namespace VehicleTracker.WebApi.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task ArgumentException_Returns400WithUsefulMessage()
    {
        using var services = CreateServices();
        var context = CreateContext(services);

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new ArgumentException("Invalid page number."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType);

        context.Response.Body.Position = 0;

        using var json = await JsonDocument.ParseAsync(
            context.Response.Body);

        Assert.Equal(
            400,
            json.RootElement.GetProperty("status").GetInt32());

        Assert.Equal(
            "Invalid page number.",
            json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutSensitiveDetails()
    {
        using var services = CreateServices();
        var context = CreateContext(services);

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException(
                "Sensitive database password: secret-123"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType);

        context.Response.Body.Position = 0;

        using var json = await JsonDocument.ParseAsync(
            context.Response.Body);

        Assert.Equal(
            500,
            json.RootElement.GetProperty("status").GetInt32());

        Assert.Equal(
            "An unexpected server error occurred.",
            json.RootElement.GetProperty("detail").GetString());

        Assert.DoesNotContain(
            "secret-123",
            json.RootElement.GetRawText());
    }

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(
        ServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        context.Request.Path = "/api/vehicles/test/history";
        context.Response.Body = new MemoryStream();

        return context;
    }
}
