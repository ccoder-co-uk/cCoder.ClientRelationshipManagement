namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IMailClient
{
    Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default);
}
