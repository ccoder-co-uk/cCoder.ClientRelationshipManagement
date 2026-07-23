using System.Data;
using Microsoft.Data.SqlClient;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class MailboxSyncLockBroker(string connectionString) : IMailboxSyncLockBroker
{
    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = N'crm.mailbox-sync',
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 30000;
                SELECT @result;
                """;
            int result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
                throw new TimeoutException($"Could not acquire the mailbox sync lock (result {result}).");

            return new Lease(connection, transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    sealed class Lease(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
