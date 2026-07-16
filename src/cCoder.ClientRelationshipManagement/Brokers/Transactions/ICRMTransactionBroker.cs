namespace cCoder.ClientRelationshipManagement.Brokers.Transactions;

public interface ICRMTransactionBroker
{
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
