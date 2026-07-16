using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessInstance> ProcessInstances { get; set; }
    static void ConfigureProcessInstance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessInstance>().ToTable("ProcessInstances", ProcessSchema);
        ConfigureAuditable<ProcessInstance>(modelBuilder);

        modelBuilder.Entity<ProcessInstance>()
            .HasIndex(entity => new { entity.State, entity.ProcessDefinitionId });

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
}
