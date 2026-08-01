namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface IMailClient
{
    Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default);
}
