using VehicleTracker.Application.Common.Validation;
using VehicleTracker.Application.Vehicles;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class VehicleValidationExceptionTests
{
    [Fact]
    public void InvalidReference_ProvidesStableErrorCode()
    {
        var validator = new VehicleReferenceQueryValidator();

        var exception = Assert.Throws<VehicleValidationException>(
            () => validator.Validate("invalid-route", null));

        Assert.Equal(
            ValidationErrorCodes.InvalidReferenceId,
            exception.Code);

        Assert.IsAssignableFrom<ArgumentException>(exception);

        Assert.Equal(
            "Invalid Entur line or quay identifier.",
            exception.Message);
    }

    [Fact]
    public void MissingReference_ProvidesStableErrorCode()
    {
        var validator = new VehicleReferenceQueryValidator();

        var exception = Assert.Throws<VehicleValidationException>(
            () => validator.Validate(null, null));

        Assert.Equal(
            ValidationErrorCodes.ReferenceIdRequired,
            exception.Code);
    }
}
