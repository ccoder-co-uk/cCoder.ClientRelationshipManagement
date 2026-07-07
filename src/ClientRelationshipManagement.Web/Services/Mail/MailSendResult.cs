namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class MailSendResult
{
    public bool Success { get; init; }
    public string ExternalMessageId { get; init; }
    public string ErrorMessage { get; init; }

    public static MailSendResult Sent(string externalMessageId = null) =>
        new()
        {
            Success = true,
            ExternalMessageId = externalMessageId,
        };

    public static MailSendResult Failed(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
        };
}
