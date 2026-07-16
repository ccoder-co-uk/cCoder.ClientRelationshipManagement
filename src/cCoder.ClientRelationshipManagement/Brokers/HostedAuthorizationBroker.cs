using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers;

public sealed class HostedAuthorizationBroker(string connectionString, string userId) : ICRMAuthInfo
{
    readonly Lazy<string[]> tenantIds = new(() => LoadTenantIds(connectionString));

    public string SSOUserId { get; } = string.IsNullOrWhiteSpace(userId) ? "system" : userId;
    public string[] ReadableTenants => tenantIds.Value;
    public string[] WriteableTenants => tenantIds.Value;

    static string[] LoadTenantIds(string connectionString)
    {
        DbContextOptions<ClientRelationshipDbContext> options =
            new DbContextOptionsBuilder<ClientRelationshipDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        using ClientRelationshipDbContext context = new(options);
        return context.TenantCompanyRelationships.Select(item => item.TenantId)
            .Concat(context.Leads.Select(item => item.TenantId))
            .Concat(context.ProcessDefinitions.Select(item => item.TenantId))
            .Concat(context.AgentMessages.Select(item => item.TenantId))
            .Concat(context.CompanyHistory.Select(item => item.TenantId))
            .Distinct()
            .ToArray();
    }
}
