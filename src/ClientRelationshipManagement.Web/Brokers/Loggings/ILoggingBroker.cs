namespace ClientRelationshipManagement.Web.Brokers.Loggings;

public interface ILoggingBroker<T>
    where T : class
{
    void LogDebug(string message, params object[] args);
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
}
