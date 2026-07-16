using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext(
    DbContextOptions<ClientRelationshipDbContext> options,
    ICRMAuthInfo authInfo = null)
    : DbContext(options)
{
    public const string MasterdataSchema = "masterdata";
    public const string LeadsSchema = "leads";
    public const string CrmSchema = "crm";
    public const string ProcessSchema = "process";

    readonly ICRMAuthInfo authInfo = authInfo;

    public string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId) ? "system" : authInfo.SSOUserId;

    public string[] ReadableTenantIds => authInfo?.ReadableTenants ?? [];
    public string[] WriteableTenantIds => authInfo?.WriteableTenants ?? [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureAddress(modelBuilder);
        ConfigureSource(modelBuilder);
        ConfigureImport(modelBuilder);
        ConfigureImportLink(modelBuilder);
        ConfigureCompany(modelBuilder);
        ConfigureCompanyHistory(modelBuilder);
        ConfigureCompanyContact(modelBuilder);
        ConfigureLead(modelBuilder);
        ConfigureLeadContact(modelBuilder);
        ConfigureTenantCompanyRelationship(modelBuilder);
        ConfigureRelationshipContact(modelBuilder);
        ConfigureOpportunity(modelBuilder);
        ConfigureClientAccount(modelBuilder);
        ConfigureHandoffPack(modelBuilder);
        ConfigureActivity(modelBuilder);
        ConfigureMaterial(modelBuilder);
        ConfigureEmail(modelBuilder);
        ConfigureEmailRecipient(modelBuilder);
        ConfigureProcessDefinition(modelBuilder);
        ConfigureProcessStep(modelBuilder);
        ConfigureProcessTransition(modelBuilder);
        ConfigureProcessInstance(modelBuilder);
        ConfigureProcessTask(modelBuilder);
        ConfigureAgentRun(modelBuilder);
        ConfigureAgentMessage(modelBuilder);
        ConfigureAgentMessageEntry(modelBuilder);
        ConfigureAgentAutomationSetting(modelBuilder);
        ConfigureMailboxMessageRecord(modelBuilder);
    }

    static void ConfigureAuditable<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ICrmEntity
    {
        modelBuilder.Entity<TEntity>().Property(entity => entity.CreatedBy).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<TEntity>().Property(entity => entity.LastUpdatedBy).HasMaxLength(256).IsRequired();
    }
}
