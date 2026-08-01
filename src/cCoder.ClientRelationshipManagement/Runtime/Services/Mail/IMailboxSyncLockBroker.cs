namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface IMailboxSyncLockBroker
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
