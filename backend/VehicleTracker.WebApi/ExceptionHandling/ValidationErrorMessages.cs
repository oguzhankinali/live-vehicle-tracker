using VehicleTracker.Application.Common.Validation;

namespace VehicleTracker.WebApi.ExceptionHandling;

public static class ValidationErrorMessages
{
    public static string GetMessage(
        string code,
        string defaultMessage,
        string? acceptLanguage)
    {
        var preferredLanguage = acceptLanguage?
            .Split(',')[0]
            .Split(';')[0]
            .Trim();

        if (preferredLanguage is null ||
            !preferredLanguage.StartsWith(
                "tr",
                StringComparison.OrdinalIgnoreCase))
        {
            return defaultMessage;
        }

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

            _ => defaultMessage
        };
    }
}
