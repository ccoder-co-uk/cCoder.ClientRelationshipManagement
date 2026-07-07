using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options,
    ICRMAuthInfo authInfo = null)
    : DbContext(options)
{
    public const string MasterdataSchema = "masterdata";
    public const string LeadsSchema = "leads";
    public const string CrmSchema = "crm";
    public const string ProcessSchema = "process";

    readonly ICRMAuthInfo authInfo = authInfo;

    public DbSet<Address> Addresses { get; set; }
    public DbSet<Source> Sources { get; set; }
    public DbSet<Import> Imports { get; set; }
    public DbSet<ImportLink> ImportLinks { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<CompanyContact> CompanyContacts { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<LeadContact> LeadContacts { get; set; }
    public DbSet<TenantCompanyRelationship> TenantCompanyRelationships { get; set; }
    public DbSet<RelationshipContact> RelationshipContacts { get; set; }
    public DbSet<Opportunity> Opportunities { get; set; }
    public DbSet<ClientAccount> ClientAccounts { get; set; }
    public DbSet<HandoffPack> HandoffPacks { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<Email> Emails { get; set; }
    public DbSet<EmailRecipient> EmailRecipients { get; set; }
    public DbSet<ProcessDefinition> ProcessDefinitions { get; set; }
    public DbSet<ProcessStep> ProcessSteps { get; set; }
    public DbSet<ProcessTransition> ProcessTransitions { get; set; }
    public DbSet<ProcessInstance> ProcessInstances { get; set; }
    public DbSet<ProcessTask> ProcessTasks { get; set; }
    public DbSet<AgentRun> AgentRuns { get; set; }
    public DbSet<AgentMessage> AgentMessages { get; set; }

    public string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            ? "system"
            : authInfo.SSOUserId;

    public string[] ReadableTenantIds =>
        authInfo?.ReadableTenants ?? [];

    public string[] WriteableTenantIds =>
        authInfo?.WriteableTenants ?? [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAddress(modelBuilder);
        ConfigureSource(modelBuilder);
        ConfigureImport(modelBuilder);
        ConfigureImportLink(modelBuilder);
        ConfigureCompany(modelBuilder);
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
    }

    static void ConfigureAuditable<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditableEntity
    {
        modelBuilder.Entity<TEntity>()
            .Property(entity => entity.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        modelBuilder.Entity<TEntity>()
            .Property(entity => entity.LastUpdatedBy)
            .HasMaxLength(256)
            .IsRequired();
    }

    static void ConfigureAddress(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>().ToTable("Addresses", MasterdataSchema);
        ConfigureAuditable<Address>(modelBuilder);

        modelBuilder.Entity<Address>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Address>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Address>().Property(entity => entity.CountryId).HasMaxLength(64);
    }

    static void ConfigureSource(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Source>().ToTable("Sources", MasterdataSchema);
        ConfigureAuditable<Source>(modelBuilder);

        modelBuilder.Entity<Source>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<Source>().Property(entity => entity.CountryCode).HasMaxLength(16);
        modelBuilder.Entity<Source>().Property(entity => entity.Notes).HasMaxLength(2048);
        modelBuilder.Entity<Source>()
            .HasIndex(entity => new { entity.Name, entity.CountryCode })
            .IsUnique();
    }

    static void ConfigureImport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Import>().ToTable("Imports", LeadsSchema);
        ConfigureAuditable<Import>(modelBuilder);

        modelBuilder.Entity<Import>().Property(entity => entity.OriginalFileName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Import>().Property(entity => entity.ContentType).HasMaxLength(256);
        modelBuilder.Entity<Import>().Property(entity => entity.StoredFilePath).HasMaxLength(1024);
        modelBuilder.Entity<Import>().Property(entity => entity.StoredObjectKey).HasMaxLength(512);
        modelBuilder.Entity<Import>().Property(entity => entity.WarningSummary).HasMaxLength(4096);
        modelBuilder.Entity<Import>().Property(entity => entity.ErrorSummary).HasMaxLength(4096);
        modelBuilder.Entity<Import>().Property(entity => entity.ProcessingCheckpoint).HasMaxLength(128);
        modelBuilder.Entity<Import>().Property(entity => entity.UploadSessionId).HasMaxLength(128);
        modelBuilder.Entity<Import>().HasIndex(entity => entity.JobStatus);
        modelBuilder.Entity<Import>().HasIndex(entity => entity.UploadSessionId);

        modelBuilder.Entity<Import>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.Imports)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureImportLink(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportLink>().ToTable("ImportLinks", LeadsSchema);
        ConfigureAuditable<ImportLink>(modelBuilder);

        modelBuilder.Entity<ImportLink>().Property(entity => entity.SourceRowKey).HasMaxLength(256);
        modelBuilder.Entity<ImportLink>().HasIndex(entity => new { entity.ImportId, entity.SourceRowNumber });
        modelBuilder.Entity<ImportLink>().HasIndex(entity => new { entity.SourceId, entity.SourceRowKey });

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Import)
            .WithMany(import => import.Links)
            .HasForeignKey(entity => entity.ImportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.ImportLinks)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Company)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Lead)
            .WithMany()
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().ToTable("Companies", MasterdataSchema);
        ConfigureAuditable<Company>(modelBuilder);

        modelBuilder.Entity<Company>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.SourceRecordId).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.OfficialName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Company>().Property(entity => entity.LegalEntityName).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.TradingName).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.VatNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyCategory).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyStatus).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.CountryOfOrigin).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.PrimarySicCodes).HasMaxLength(2048);
        modelBuilder.Entity<Company>().Property(entity => entity.RegistryUri).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.WebsiteUrl).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.ContactEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.ContactPhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.RevenueCurrency).HasMaxLength(16);
        modelBuilder.Entity<Company>().Property(entity => entity.RankingRationale).HasMaxLength(2048);
        modelBuilder.Entity<Company>().HasIndex(entity => entity.CompanyNumber);
        modelBuilder.Entity<Company>().HasIndex(entity => entity.VatNumber);

        modelBuilder.Entity<Company>()
            .HasOne(entity => entity.RegisteredAddress)
            .WithMany(address => address.RegisteredCompanies)
            .HasForeignKey(entity => entity.RegisteredAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureCompanyContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyContact>().ToTable("CompanyContacts", MasterdataSchema);
        ConfigureAuditable<CompanyContact>(modelBuilder);

        modelBuilder.Entity<CompanyContact>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.Position).HasMaxLength(256);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.EmailAddress).HasMaxLength(256);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.PhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.LinkedInUrl).HasMaxLength(512);

        modelBuilder.Entity<CompanyContact>()
            .HasOne(entity => entity.Company)
            .WithMany(company => company.Contacts)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureLead(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>().ToTable("Leads", LeadsSchema);
        ConfigureAuditable<Lead>(modelBuilder);

        modelBuilder.Entity<Lead>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Lead>().Property(entity => entity.SourceRecordId).HasMaxLength(128);
        modelBuilder.Entity<Lead>().Property(entity => entity.SourceFileName).HasMaxLength(256);
        modelBuilder.Entity<Lead>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<Lead>().Property(entity => entity.RawCompanyName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Lead>().Property(entity => entity.RawTradingName).HasMaxLength(512);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawCompanyNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawVatNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawWebsiteUrl).HasMaxLength(512);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawContactEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawContactPhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RankingRationale).HasMaxLength(2048);
        modelBuilder.Entity<Lead>().HasIndex(entity => new { entity.TenantId, entity.Status });

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.Leads)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Company)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ConvertedLeads)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Leads)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureLeadContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadContact>().ToTable("LeadContacts", LeadsSchema);
        ConfigureAuditable<LeadContact>(modelBuilder);

        modelBuilder.Entity<LeadContact>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<LeadContact>().Property(entity => entity.Position).HasMaxLength(256);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.EmailAddress).HasMaxLength(256);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.PhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.LinkedInUrl).HasMaxLength(512);

        modelBuilder.Entity<LeadContact>()
            .HasOne(entity => entity.Lead)
            .WithMany(lead => lead.Contacts)
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureTenantCompanyRelationship(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantCompanyRelationship>().ToTable("TenantCompanyRelationships", CrmSchema);
        ConfigureAuditable<TenantCompanyRelationship>(modelBuilder);

        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.AccountOwnerUserId).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.AccountOwnerDisplayName).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.LeadSource).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().HasIndex(entity => new { entity.TenantId, entity.Status });

        modelBuilder.Entity<TenantCompanyRelationship>()
            .HasOne(entity => entity.Company)
            .WithMany(company => company.Relationships)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureRelationshipContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RelationshipContact>().ToTable("RelationshipContacts", CrmSchema);
        ConfigureAuditable<RelationshipContact>(modelBuilder);

        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.RelationshipRoute).HasMaxLength(512);
        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.Source).HasMaxLength(256);
        modelBuilder.Entity<RelationshipContact>()
            .HasIndex(entity => new { entity.TenantCompanyRelationshipId, entity.CompanyContactId })
            .IsUnique();

        modelBuilder.Entity<RelationshipContact>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Contacts)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RelationshipContact>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.RelationshipContacts)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureOpportunity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Opportunity>().ToTable("Opportunities", CrmSchema);
        ConfigureAuditable<Opportunity>(modelBuilder);

        modelBuilder.Entity<Opportunity>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Opportunity>().HasIndex(entity => new { entity.TenantCompanyRelationshipId, entity.Stage });

        modelBuilder.Entity<Opportunity>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Opportunities)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Opportunity>()
            .HasOne(entity => entity.PrimaryRelationshipContact)
            .WithMany(contact => contact.Opportunities)
            .HasForeignKey(entity => entity.PrimaryRelationshipContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureClientAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientAccount>().ToTable("ClientAccounts", CrmSchema);
        ConfigureAuditable<ClientAccount>(modelBuilder);

        modelBuilder.Entity<ClientAccount>().Property(entity => entity.AccountReference).HasMaxLength(128);

        modelBuilder.Entity<ClientAccount>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ClientAccounts)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClientAccount>()
            .HasOne(entity => entity.WonOpportunity)
            .WithMany()
            .HasForeignKey(entity => entity.WonOpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureHandoffPack(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HandoffPack>().ToTable("HandoffPacks", CrmSchema);
        ConfigureAuditable<HandoffPack>(modelBuilder);

        modelBuilder.Entity<HandoffPack>().Property(entity => entity.LegacyId).HasMaxLength(128);

        modelBuilder.Entity<HandoffPack>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.HandoffPacks)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>().ToTable("Activities", CrmSchema);
        ConfigureAuditable<Activity>(modelBuilder);

        modelBuilder.Entity<Activity>().Property(entity => entity.LegacyId).HasMaxLength(128);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Activities)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Activities)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Activities)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Activities)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.Material)
            .WithMany(material => material.Activities)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureMaterial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>().ToTable("Materials", CrmSchema);
        ConfigureAuditable<Material>(modelBuilder);

        modelBuilder.Entity<Material>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Material>().Property(entity => entity.Name).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Material>().Property(entity => entity.FilePath).HasMaxLength(1024);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Materials)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Materials)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Materials)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Materials)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureEmail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Email>().ToTable("Emails", CrmSchema);
        ConfigureAuditable<Email>(modelBuilder);

        modelBuilder.Entity<Email>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Email>().Property(entity => entity.SenderUserId).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.FromDisplayName).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.FromEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.Subject).HasMaxLength(512).IsRequired();

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Emails)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Emails)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Emails)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.Material)
            .WithMany(material => material.Emails)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Emails)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureEmailRecipient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailRecipient>().ToTable("EmailRecipients", CrmSchema);
        ConfigureAuditable<EmailRecipient>(modelBuilder);

        modelBuilder.Entity<EmailRecipient>().Property(entity => entity.Address).HasMaxLength(256).IsRequired();

        modelBuilder.Entity<EmailRecipient>()
            .HasOne(entity => entity.Email)
            .WithMany(email => email.Recipients)
            .HasForeignKey(entity => entity.EmailId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmailRecipient>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.EmailRecipients)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureProcessDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessDefinition>().ToTable("ProcessDefinitions", ProcessSchema);
        ConfigureAuditable<ProcessDefinition>(modelBuilder);

        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.ChangeSummary).HasMaxLength(1024);
        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.ApprovalNotes).HasMaxLength(2048);
        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.ApprovedBy).HasMaxLength(256);
        modelBuilder.Entity<ProcessDefinition>().Property(entity => entity.ProposedByAgent).HasMaxLength(256);
        modelBuilder.Entity<ProcessDefinition>().HasIndex(entity => new { entity.TenantId, entity.ScopeType, entity.IsDefault });
        modelBuilder.Entity<ProcessDefinition>().HasIndex(entity => new { entity.TenantId, entity.ScopeType, entity.FamilyId, entity.VersionNumber });

        modelBuilder.Entity<ProcessDefinition>()
            .HasOne(entity => entity.SupersedesProcessDefinition)
            .WithMany(entity => entity.ProposedVersions)
            .HasForeignKey(entity => entity.SupersedesProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureProcessStep(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessStep>().ToTable("ProcessSteps", ProcessSchema);
        ConfigureAuditable<ProcessStep>(modelBuilder);

        modelBuilder.Entity<ProcessStep>().Property(entity => entity.Key).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.TaskTitleTemplate).HasMaxLength(512).IsRequired();

        modelBuilder.Entity<ProcessStep>()
            .HasOne(entity => entity.ProcessDefinition)
            .WithMany(definition => definition.Steps)
            .HasForeignKey(entity => entity.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureProcessTransition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessTransition>().ToTable("ProcessTransitions", ProcessSchema);
        ConfigureAuditable<ProcessTransition>(modelBuilder);

        modelBuilder.Entity<ProcessTransition>().Property(entity => entity.OutcomeKey).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<ProcessTransition>().Property(entity => entity.OutcomeLabel).HasMaxLength(256).IsRequired();

        modelBuilder.Entity<ProcessTransition>()
            .HasOne(entity => entity.ProcessStep)
            .WithMany(step => step.OutgoingTransitions)
            .HasForeignKey(entity => entity.ProcessStepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTransition>()
            .HasOne(entity => entity.NextProcessStep)
            .WithMany(step => step.IncomingTransitions)
            .HasForeignKey(entity => entity.NextProcessStepId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureProcessInstance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessInstance>().ToTable("ProcessInstances", ProcessSchema);
        ConfigureAuditable<ProcessInstance>(modelBuilder);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.ProcessDefinition)
            .WithMany(definition => definition.Instances)
            .HasForeignKey(entity => entity.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.Lead)
            .WithMany(lead => lead.ProcessInstances)
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ProcessInstances)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.ProcessInstances)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.ProcessInstances)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.CurrentProcessStep)
            .WithMany(step => step.CurrentInstances)
            .HasForeignKey(entity => entity.CurrentProcessStepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(entity => entity.CurrentProcessTask)
            .WithMany()
            .HasForeignKey(entity => entity.CurrentProcessTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureProcessTask(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessTask>().ToTable("ProcessTasks", ProcessSchema);
        ConfigureAuditable<ProcessTask>(modelBuilder);

        modelBuilder.Entity<ProcessTask>().Property(entity => entity.RenderedTitle).HasMaxLength(512).IsRequired();

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.ProcessInstance)
            .WithMany(instance => instance.Tasks)
            .HasForeignKey(entity => entity.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.ProcessStep)
            .WithMany(step => step.Tasks)
            .HasForeignKey(entity => entity.ProcessStepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.Lead)
            .WithMany(lead => lead.ProcessTasks)
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ProcessTasks)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.ProcessTasks)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.ProcessTasks)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(entity => entity.Email)
            .WithMany()
            .HasForeignKey(entity => entity.EmailId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    static void ConfigureAgentRun(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentRun>().ToTable("AgentRuns", CrmSchema);
        ConfigureAuditable<AgentRun>(modelBuilder);

        modelBuilder.Entity<AgentRun>().Property(entity => entity.ExecutionUserId).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<AgentRun>().Property(entity => entity.Provider).HasMaxLength(128);
        modelBuilder.Entity<AgentRun>().Property(entity => entity.Model).HasMaxLength(256);
        modelBuilder.Entity<AgentRun>().Property(entity => entity.WorkingDirectory).HasMaxLength(1024);
        modelBuilder.Entity<AgentRun>().Property(entity => entity.Summary).HasMaxLength(4096);
        modelBuilder.Entity<AgentRun>().Property(entity => entity.ErrorMessage).HasMaxLength(4096);
    }

    static void ConfigureAgentMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentMessage>().ToTable("AgentMessages", CrmSchema);
        ConfigureAuditable<AgentMessage>(modelBuilder);

        modelBuilder.Entity<AgentMessage>().Property(entity => entity.CorrelationKey).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.Title).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.AgentName).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.RespondedBy).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().HasIndex(entity => entity.CorrelationKey);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.AgentRun)
            .WithMany()
            .HasForeignKey(entity => entity.AgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.Lead)
            .WithMany()
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany()
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.Opportunity)
            .WithMany()
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany()
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.ProcessTask)
            .WithMany()
            .HasForeignKey(entity => entity.ProcessTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.Email)
            .WithMany()
            .HasForeignKey(entity => entity.EmailId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.ProcessDefinition)
            .WithMany(entity => entity.Messages)
            .HasForeignKey(entity => entity.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(entity => entity.ProposedProcessDefinition)
            .WithMany(entity => entity.ProposedMessages)
            .HasForeignKey(entity => entity.ProposedProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
