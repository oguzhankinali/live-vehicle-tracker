namespace VehicleTracker.Application.Common.Validation;

public sealed class VehicleValidationException : ArgumentException
{
    public string Code { get; }

    public VehicleValidationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }
}
