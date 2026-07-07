namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IEmailDispatchProcessor
{
    ValueTask<int> DispatchDueEmailsAsync(CancellationToken cancellationToken = default);
}
