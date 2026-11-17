using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Vehicles;
using VehicleTracker.Application.Common.Validation;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class VehicleHistoryQueryValidatorTests
{
    private readonly VehicleHistoryQueryValidator _validator = new();

    private static readonly DateTime NowUtc =
        new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan Retention =
        TimeSpan.FromHours(3);

    [Fact]
    public void MissingDates_UseRetentionWindow()
    {
        var query = new VehicleHistoryQuery("vehicle-123");

        var result = _validator.ValidateAndNormalize(
            query, Retention, NowUtc);

        Assert.Equal(NowUtc.AddHours(-3), result.FromUtc);
        Assert.Equal(NowUtc, result.ToUtc);
    }

    [Fact]
    public void OldStartDate_IsClampedToRetentionWindow()
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            FromUtc: NowUtc.AddHours(-10));

        var result = _validator.ValidateAndNormalize(
            query, Retention, NowUtc);

        Assert.Equal(NowUtc.AddHours(-3), result.FromUtc);
    }

    [Fact]
    public void FutureEndDate_IsClampedToNow()
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            ToUtc: NowUtc.AddHours(1));

        var result = _validator.ValidateAndNormalize(
            query, Retention, NowUtc);

        Assert.Equal(NowUtc, result.ToUtc);
    }

    [Fact]
    public void FutureStartDate_IsRejected()
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            FromUtc: NowUtc.AddMinutes(10));

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }

    [Fact]
    public void ReversedDateRange_IsRejected()
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            FromUtc: NowUtc.AddMinutes(-10),
            ToUtc: NowUtc.AddMinutes(-30));

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidPage_IsRejected(int page)
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            Page: page);

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void InvalidPageSize_IsRejected(int pageSize)
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            PageSize: pageSize);

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }

    [Fact]
    public void PageOffsetOverflow_IsRejected()
    {
        var query = new VehicleHistoryQuery(
            "vehicle-123",
            Page: int.MaxValue,
            PageSize: 500);

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }

    [Fact]
    public void UnspecifiedDate_IsTreatedAsUtc()
    {
        var unspecifiedDate = new DateTime(
            2026, 11, 16, 10, 0, 0,
            DateTimeKind.Unspecified);

        var query = new VehicleHistoryQuery(
            "vehicle-123",
            FromUtc: unspecifiedDate);

        var result = _validator.ValidateAndNormalize(
            query, Retention, NowUtc);

        Assert.Equal(DateTimeKind.Utc, result.FromUtc!.Value.Kind);
        Assert.Equal(10, result.FromUtc.Value.Hour);
    }

    [Fact]
    public void BlankVehicleId_IsRejected()
    {
        var query = new VehicleHistoryQuery(" ");

        Assert.Throws<VehicleValidationException>(() =>
            _validator.ValidateAndNormalize(
                query, Retention, NowUtc));
    }
}
