using VehicleTracker.Application.Vehicles;
using VehicleTracker.Application.Common.Validation;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class VehicleReferenceQueryValidatorTests
{
    private readonly VehicleReferenceQueryValidator _validator = new();

    [Theory]
    [InlineData("VKT:Line:54", "NSR:Quay:33333")]
    [InlineData("BRA:Line:4_6228", null)]
    [InlineData(null, "NSR:Quay:3902")]
    public void ValidIdentifiers_AreAccepted(
        string? routeId,
        string? stopId)
    {
        _validator.Validate(routeId, stopId);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", null)]
    public void MissingIdentifiers_AreRejected(
        string? routeId,
        string? stopId)
    {
        Assert.Throws<VehicleValidationException>(
            () => _validator.Validate(routeId, stopId));
    }

    [Theory]
    [InlineData("invalid-route", null)]
    [InlineData(null, "invalid-stop")]
    [InlineData("VKT:Line:54", "NSR:StopPlace:19045")]
    public void InvalidIdentifiers_AreRejected(
        string? routeId,
        string? stopId)
    {
        Assert.Throws<VehicleValidationException>(
            () => _validator.Validate(routeId, stopId));
    }

    [Fact]
    public void TooLongIdentifier_IsRejected()
    {
        var routeId = "VKT:Line:" + new string('a', 160);

        Assert.Throws<VehicleValidationException>(
            () => _validator.Validate(routeId, null));
    }

    [Fact]
    public void TripIdOnly_IsAccepted()
    {
        var validator = new VehicleReferenceQueryValidator();

        validator.Validate(
            null,
            null,
            "FIN:ServiceJourney:5726");
    }

    [Fact]
    public void InvalidTripId_IsRejected()
    {
        var validator = new VehicleReferenceQueryValidator();

        var exception = Assert.Throws<VehicleValidationException>(
            () => validator.Validate(
                null,
                null,
                "invalid-trip"));

        Assert.Equal(
            ValidationErrorCodes.InvalidReferenceId,
            exception.Code);
    }

}
