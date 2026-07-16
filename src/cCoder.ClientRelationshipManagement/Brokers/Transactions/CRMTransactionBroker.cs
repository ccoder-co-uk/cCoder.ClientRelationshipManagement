using cCoder.ClientRelationshipManagement.Platform.Data;

namespace cCoder.ClientRelationshipManagement.Brokers.Transactions;

internal sealed class CRMTransactionBroker(ClientRelationshipDbContext context) : ICRMTransactionBroker
{
    public ValueTask CommitAsync(CancellationToken cancellationToken = default) =>
        new(context.SaveChangesAsync(cancellationToken));
}
