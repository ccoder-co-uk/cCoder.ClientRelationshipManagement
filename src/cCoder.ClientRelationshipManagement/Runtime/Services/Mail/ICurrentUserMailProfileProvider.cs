namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface ICurrentUserMailProfileProvider
{
    ValueTask<MailSenderProfile> GetCurrentAsync(CancellationToken cancellationToken = default);
    ValueTask<MailSenderProfile> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
