namespace VehicleTracker.Application.Common.Validation;

public sealed class VehicleValidationException : ArgumentException
{
    public string Code { get; }

    public VehicleValidationException(string code)
        : base(code)
    {
        Code = code;
    }
}
