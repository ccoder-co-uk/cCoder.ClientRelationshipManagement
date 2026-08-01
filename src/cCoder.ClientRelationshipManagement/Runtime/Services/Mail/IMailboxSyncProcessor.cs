namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface IMailboxSyncProcessor
{
    ValueTask<int> SyncAsync(CancellationToken cancellationToken = default);
}
