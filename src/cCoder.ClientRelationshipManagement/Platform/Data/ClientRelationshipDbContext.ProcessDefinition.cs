using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessDefinition> ProcessDefinitions { get; set; }
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
}
