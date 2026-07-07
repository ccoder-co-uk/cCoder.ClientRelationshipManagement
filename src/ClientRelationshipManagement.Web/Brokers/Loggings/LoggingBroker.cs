namespace ClientRelationshipManagement.Web.Brokers.Loggings;

public sealed class LoggingBroker<T>(ILogger<T> logger) : ILoggingBroker<T>
    where T : class
{
    public void LogDebug(string message, params object[] args) =>
        logger.LogDebug(message, args);

    public void LogInformation(string message, params object[] args) =>
        logger.LogInformation(message, args);

    public void LogWarning(string message, params object[] args) =>
        logger.LogWarning(message, args);

    public void LogError(string message, params object[] args) =>
        logger.LogError(message, args);

    public void LogError(Exception exception, string message, params object[] args) =>
        logger.LogError(exception, message, args);
}
