using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessTask> ProcessTasks { get; set; }
    static void ConfigureProcessTask(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessTask>().ToTable("ProcessTasks", ProcessSchema);
        ConfigureAuditable<ProcessTask>(modelBuilder);

        modelBuilder.Entity<ProcessTask>().Property(entity => entity.RenderedTitle).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<ProcessTask>().Property(entity => entity.AgentClaimedBy).HasMaxLength(256);
        modelBuilder.Entity<ProcessTask>()
            .HasIndex(entity => new { entity.State, entity.AgentClaimExpiresOn });

        modelBuilder.Entity<ProcessTask>()
            .HasIndex(entity => new { entity.State, entity.DueOn })
            .IncludeProperties(entity => new
            {
                entity.LeadId,
                entity.TenantCompanyRelationshipId,
                entity.ProcessStepId,
                entity.RenderedTitle
            });

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
}
