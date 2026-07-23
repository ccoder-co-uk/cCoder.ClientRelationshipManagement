using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessStepTask> ProcessStepTasks { get; set; }
    static void ConfigureProcessStepTask(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessStepTask>().ToTable("ProcessStepTasks", ProcessSchema);
        ConfigureAuditable<ProcessStepTask>(modelBuilder);
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.Key).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.HandlerKey).HasMaxLength(256);
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.RequiredContextKeys).HasMaxLength(2048);
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.ProducedContextKeys).HasMaxLength(2048);
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.NextTaskKey).HasMaxLength(128);
        modelBuilder.Entity<ProcessStepTask>().Property(entity => entity.FailureTaskKey).HasMaxLength(128);
        modelBuilder.Entity<ProcessStepTask>().HasIndex(entity => new { entity.ProcessStepId, entity.Key }).IsUnique();
        modelBuilder.Entity<ProcessStepTask>().HasOne(entity => entity.ProcessStep).WithMany(step => step.StepTasks)
            .HasForeignKey(entity => entity.ProcessStepId).OnDelete(DeleteBehavior.Restrict);
    }
}
