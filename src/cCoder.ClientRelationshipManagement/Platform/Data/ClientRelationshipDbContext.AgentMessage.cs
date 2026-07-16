using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<AgentMessage> AgentMessages { get; set; }
    static void ConfigureAgentMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentMessage>().ToTable("AgentMessages", CrmSchema);
        ConfigureAuditable<AgentMessage>(modelBuilder);

        modelBuilder.Entity<AgentMessage>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.CorrelationKey).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.Title).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.AgentName).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().Property(entity => entity.RespondedBy).HasMaxLength(256);
        modelBuilder.Entity<AgentMessage>().HasIndex(entity => entity.CorrelationKey);
        modelBuilder.Entity<AgentMessage>().HasIndex(entity => new { entity.TenantId, entity.State });

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
            .HasOne(entity => entity.ProcessStep)
            .WithMany()
            .HasForeignKey(entity => entity.ProcessStepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AgentMessage>().HasIndex(entity => entity.ProcessStepId);

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
