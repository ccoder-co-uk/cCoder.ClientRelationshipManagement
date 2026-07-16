using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<AgentRun> AgentRuns { get; set; }
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
        modelBuilder.Entity<AgentRun>().Property(entity => entity.ProcessStepKey).HasMaxLength(128);
        modelBuilder.Entity<AgentRun>().HasIndex(entity => new { entity.Kind, entity.WorkLane, entity.CompletedOn });
        modelBuilder.Entity<AgentRun>().HasIndex(entity => entity.ProcessTaskId);
        modelBuilder.Entity<AgentRun>().HasIndex(entity => entity.ProcessStepId);
    }
}
