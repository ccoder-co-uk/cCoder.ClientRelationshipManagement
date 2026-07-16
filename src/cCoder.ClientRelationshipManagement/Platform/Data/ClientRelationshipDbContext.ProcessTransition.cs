using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessTransition> ProcessTransitions { get; set; }
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
}
