using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Infrastructure;

public sealed class CRMAcceptanceFactAttribute : FactAttribute
{
    static readonly string[] RequiredVariables =
    [
        "CCODER_ACCEPTANCE_CRM_CONNECTION_STRING",
        "CCODER_ACCEPTANCE_SSO_CONNECTION_STRING"
    ];

    public CRMAcceptanceFactAttribute()
    {
        if (RequiredVariables.Any(variable => string.IsNullOrWhiteSpace(Read(variable))))
            Skip = "CRM acceptance tests require CRM and SSO acceptance connection strings.";
    }

    static string Read(string variableName) =>
        Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine)
        ?? Environment.GetEnvironmentVariable(variableName)
        ?? string.Empty;
}
