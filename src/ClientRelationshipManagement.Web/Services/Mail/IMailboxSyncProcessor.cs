namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IMailboxSyncProcessor
{
    ValueTask<int> SyncAsync(CancellationToken cancellationToken = default);
}
