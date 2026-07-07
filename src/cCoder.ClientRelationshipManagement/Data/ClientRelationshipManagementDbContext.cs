using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext : DbContext
{
    readonly ICRMAuthInfo authInfo;
    string[] readableClientTenantIds;

    public ClientRelationshipManagementDbContext(
        DbContextOptions<ClientRelationshipManagementDbContext> options,
        ICRMAuthInfo authInfo = null)
        : base(options)
    {
        this.authInfo = authInfo;
    }

    public DbSet<Address> Addresses { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<ClientContact> ClientContacts { get; set; }
    public DbSet<ClientOpportunity> ClientOpportunities { get; set; }
    public DbSet<ClientActivity> ClientActivities { get; set; }
    public DbSet<ClientMaterial> ClientMaterials { get; set; }
    public DbSet<Email> Emails { get; set; }
    public DbSet<ClientHandoffPack> ClientHandoffPacks { get; set; }
    public DbSet<ClientProcessDefinition> ClientProcessDefinitions { get; set; }
    public DbSet<ClientProcessStep> ClientProcessSteps { get; set; }
    public DbSet<ClientProcessTransition> ClientProcessTransitions { get; set; }
    public DbSet<ClientProcessInstance> ClientProcessInstances { get; set; }
    public DbSet<ClientProcessTask> ClientProcessTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAddress(modelBuilder);
        ConfigureCompany(modelBuilder);
        ConfigureClient(modelBuilder);
        ConfigureClientContact(modelBuilder);
        ConfigureClientOpportunity(modelBuilder);
        ConfigureClientActivity(modelBuilder);
        ConfigureClientMaterial(modelBuilder);
        ConfigureEmail(modelBuilder);
        ConfigureClientHandoffPack(modelBuilder);
        ConfigureClientProcessDefinition(modelBuilder);
        ConfigureClientProcessStep(modelBuilder);
        ConfigureClientProcessTransition(modelBuilder);
        ConfigureClientProcessInstance(modelBuilder);
        ConfigureClientProcessTask(modelBuilder);
    }

    string[] ReadableClientTenantIds =>
        readableClientTenantIds ??= authInfo is null
            ? Array.Empty<string>()
            : authInfo.ReadableTenants;
}
