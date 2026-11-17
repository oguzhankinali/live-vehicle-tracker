using VehicleTracker.Application.Common.Validation;

namespace VehicleTracker.WebApi.ExceptionHandling;

public static class ValidationErrorMessages
{
    public static string GetMessage(
        string code,
        string? acceptLanguage)
    {
        var preferredLanguage = acceptLanguage?
            .Split(',')[0]
            .Split(';')[0]
            .Trim();

        var isTurkish = preferredLanguage?
            .StartsWith(
                "tr",
                StringComparison.OrdinalIgnoreCase) == true;

        if (isTurkish)
        {
            return code switch
            {
                ValidationErrorCodes.VehicleIdRequired =>
                    "Araç kimliği zorunludur.",

                ValidationErrorCodes.InvalidPage =>
                    "Sayfa numarası sıfırdan büyük olmalıdır.",

                ValidationErrorCodes.InvalidPageSize =>
                    "Sayfa boyutu 1 ile 500 arasında olmalıdır.",

                ValidationErrorCodes.PageOffsetTooLarge =>
                    "İstenen sayfa numarası çok büyüktür.",

                ValidationErrorCodes.StartTimeInFuture =>
                    "Başlangıç zamanı gelecekte olamaz.",

                ValidationErrorCodes.StartTimeAfterEnd =>
                    "Başlangıç zamanı bitiş zamanından sonra olamaz.",

                ValidationErrorCodes.ReferenceIdRequired =>
                    "En az bir hat veya durak kimliği gereklidir.",

                ValidationErrorCodes.InvalidReferenceId =>
                    "Geçersiz Entur hat veya durak kimliği.",

                _ => "Geçersiz istek."
            };
        }

        return code switch
        {
            ValidationErrorCodes.VehicleIdRequired =>
                "Vehicle ID is required.",

            ValidationErrorCodes.InvalidPage =>
                "Page must be greater than zero.",

            ValidationErrorCodes.InvalidPageSize =>
                "Page size must be between 1 and 500.",

            ValidationErrorCodes.PageOffsetTooLarge =>
                "Requested page is too large.",

            ValidationErrorCodes.StartTimeInFuture =>
                "The start time cannot be in the future.",

            ValidationErrorCodes.StartTimeAfterEnd =>
                "The start time cannot be later than the end time.",

            ValidationErrorCodes.ReferenceIdRequired =>
                "At least one of routeId or stopId is required.",

            ValidationErrorCodes.InvalidReferenceId =>
                "Invalid Entur line or quay identifier.",

            _ => "Invalid request."
        };
    }
}
