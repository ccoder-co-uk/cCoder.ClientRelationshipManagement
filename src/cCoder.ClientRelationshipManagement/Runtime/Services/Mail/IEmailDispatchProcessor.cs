namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface IEmailDispatchProcessor
{
    ValueTask<int> DispatchDueEmailsAsync(CancellationToken cancellationToken = default);
}
