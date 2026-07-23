using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessStepTaskRun> ProcessStepTaskRuns { get; set; }
    static void ConfigureProcessStepTaskRun(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessStepTaskRun>().ToTable("ProcessStepTaskRuns", ProcessSchema);
        ConfigureAuditable<ProcessStepTaskRun>(modelBuilder);
        modelBuilder.Entity<ProcessStepTaskRun>().HasIndex(entity => new { entity.ProcessTaskId, entity.ProcessStepTaskId }).IsUnique();
        modelBuilder.Entity<ProcessStepTaskRun>().HasIndex(entity => new { entity.State, entity.LastUpdated });
        modelBuilder.Entity<ProcessStepTaskRun>().HasOne(entity => entity.ProcessTask).WithMany(task => task.StepTaskRuns)
            .HasForeignKey(entity => entity.ProcessTaskId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProcessStepTaskRun>().HasOne(entity => entity.ProcessStepTask).WithMany(task => task.Runs)
            .HasForeignKey(entity => entity.ProcessStepTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
