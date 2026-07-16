using ClientRelationshipManagement.Web.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class ConfigurationValueResolverTests
{
    [Fact]
    public void GetRequiredSqlConnection_DisablesEncryptionForLocalSqlServer()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:CRM"] =
                    "Data Source=.;Initial Catalog=CRM;Integrated Security=True;Encrypt=True"
            })
            .Build();

        string connectionString = ConfigurationValueResolver.GetRequiredSqlConnection(
            configuration,
            "ConnectionStrings:CRM");

        connectionString.Should().Contain("Encrypt=False");
        connectionString.Should().Contain("Trust Server Certificate=True");
    }

    [Fact]
    public void GetRequiredSqlConnection_PreservesRemoteSqlServerEncryptionPolicy()
    {
        const string configured =
            "Data Source=sql.example.internal;Initial Catalog=CRM;Integrated Security=True;Encrypt=True";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:CRM"] = configured
            })
            .Build();

        string connectionString = ConfigurationValueResolver.GetRequiredSqlConnection(
            configuration,
            "ConnectionStrings:CRM");

        connectionString.Should().Be(configured);
    }
}
