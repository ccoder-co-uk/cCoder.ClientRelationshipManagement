namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IMailboxSyncLockBroker
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
