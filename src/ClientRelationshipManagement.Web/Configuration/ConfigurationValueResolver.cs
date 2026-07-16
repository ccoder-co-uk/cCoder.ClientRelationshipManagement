using Microsoft.Data.SqlClient;

namespace ClientRelationshipManagement.Web.Configuration;

public static class ConfigurationValueResolver
{
    public static string GetRequired(IConfiguration configuration, params string[] keys) =>
        GetOptional(configuration, keys)
        ?? throw new InvalidOperationException(
            $"A configuration value is required for one of: {string.Join(", ", keys)}.");

    public static string GetOptional(IConfiguration configuration, params string[] keys)
    {
        foreach (string key in keys)
        {
            string value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            string environmentVariableName = key.Contains(':')
                ? key.Replace(":", "__")
                : key;

            string processValue =
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

            if (!string.IsNullOrWhiteSpace(processValue))
                return processValue;

            string userValue =
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User);

            if (!string.IsNullOrWhiteSpace(userValue))
                return userValue;

            string machineValue =
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);

            if (!string.IsNullOrWhiteSpace(machineValue))
                return machineValue;
        }

        return null;
    }

    public static string GetRequiredSqlConnection(IConfiguration configuration, params string[] keys) =>
        NormalizeLocalSqlConnection(GetRequired(configuration, keys));

    public static string GetOptionalSqlConnection(IConfiguration configuration, params string[] keys)
    {
        string connectionString = GetOptional(configuration, keys);
        return string.IsNullOrWhiteSpace(connectionString)
            ? null
            : NormalizeLocalSqlConnection(connectionString);
    }

    static string NormalizeLocalSqlConnection(string connectionString)
    {
        SqlConnectionStringBuilder builder = new(connectionString);
        string server = builder.DataSource?.Trim() ?? string.Empty;
        string host = server.Split('\\', ',', StringSplitOptions.TrimEntries)[0];
        bool isLocal = host is "." or "(local)" or "localhost" or "127.0.0.1"
            || host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

        if (!isLocal)
            return connectionString;

        // Local development uses Windows authentication and never crosses the network.
        // Explicitly opt out of transport encryption because this laptop's local SQL
        // instance cannot currently negotiate the client encryption requested by SqlClient.
        builder["Encrypt"] = false;
        builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }
}
