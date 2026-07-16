using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ProcessStep> ProcessSteps { get; set; }
    static void ConfigureProcessStep(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessStep>().ToTable("ProcessSteps", ProcessSchema);
        ConfigureAuditable<ProcessStep>(modelBuilder);

        modelBuilder.Entity<ProcessStep>().Property(entity => entity.Key).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.Objective).HasMaxLength(1024);
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.RequiredFacts).HasMaxLength(2048);
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.ProducedFacts).HasMaxLength(2048);
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.ViabilityImpact).HasMaxLength(1024);
        modelBuilder.Entity<ProcessStep>().Property(entity => entity.TaskTitleTemplate).HasMaxLength(512).IsRequired();

        modelBuilder.Entity<ProcessStep>()
            .HasOne(entity => entity.ProcessDefinition)
            .WithMany(definition => definition.Steps)
            .HasForeignKey(entity => entity.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
