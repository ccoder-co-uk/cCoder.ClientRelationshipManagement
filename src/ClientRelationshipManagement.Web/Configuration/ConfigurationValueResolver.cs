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
}
