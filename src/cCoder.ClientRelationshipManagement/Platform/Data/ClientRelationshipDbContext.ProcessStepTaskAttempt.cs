using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessStepTaskAttempt> ProcessStepTaskAttempts { get; set; }
    static void ConfigureProcessStepTaskAttempt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessStepTaskAttempt>().ToTable("ProcessStepTaskAttempts", ProcessSchema);
        ConfigureAuditable<ProcessStepTaskAttempt>(modelBuilder);
        modelBuilder.Entity<ProcessStepTaskAttempt>().HasIndex(entity => new { entity.ProcessStepTaskRunId, entity.AttemptNumber }).IsUnique();
        modelBuilder.Entity<ProcessStepTaskAttempt>().HasOne(entity => entity.ProcessStepTaskRun).WithMany(run => run.Attempts)
            .HasForeignKey(entity => entity.ProcessStepTaskRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
