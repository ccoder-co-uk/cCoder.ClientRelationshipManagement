using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<AgentAutomationSetting> AgentAutomationSettings { get; set; }
    static void ConfigureAgentAutomationSetting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentAutomationSetting>().ToTable("AgentAutomationSettings", CrmSchema);
        ConfigureAuditable<AgentAutomationSetting>(modelBuilder);

        modelBuilder.Entity<AgentAutomationSetting>()
            .Property(entity => entity.UserId)
            .HasMaxLength(256)
            .IsRequired();

        modelBuilder.Entity<AgentAutomationSetting>()
            .Property(entity => entity.SelectedAiProfileKey)
            .HasMaxLength(128);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.SelectedAiModel).HasMaxLength(256);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.ApprovalAgentConcurrency).HasDefaultValue(2);

        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.LeadAiProfileKey).HasMaxLength(128);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.LeadAiModel).HasMaxLength(256);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.OpportunityAiProfileKey).HasMaxLength(128);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.OpportunityAiModel).HasMaxLength(256);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.ClientAiProfileKey).HasMaxLength(128);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.ClientAiModel).HasMaxLength(256);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.LeadAgentConcurrency).HasDefaultValue(1);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.OpportunityAgentConcurrency).HasDefaultValue(1);
        modelBuilder.Entity<AgentAutomationSetting>().Property(entity => entity.ClientAgentConcurrency).HasDefaultValue(1);

        modelBuilder.Entity<AgentAutomationSetting>()
            .HasIndex(entity => entity.UserId)
            .IsUnique();
    }
}
