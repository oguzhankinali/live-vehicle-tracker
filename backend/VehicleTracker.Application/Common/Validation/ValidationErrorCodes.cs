namespace VehicleTracker.Application.Common.Validation;

public static class ValidationErrorCodes
{
    public const string VehicleIdRequired = "VEHICLE_ID_REQUIRED";
    public const string InvalidPage = "INVALID_PAGE";
    public const string InvalidPageSize = "INVALID_PAGE_SIZE";
    public const string PageOffsetTooLarge = "PAGE_OFFSET_TOO_LARGE";
    public const string StartTimeInFuture = "START_TIME_IN_FUTURE";
    public const string StartTimeAfterEnd = "START_TIME_AFTER_END";
    public const string ReferenceIdRequired = "REFERENCE_ID_REQUIRED";
    public const string InvalidReferenceId = "INVALID_REFERENCE_ID";
}
