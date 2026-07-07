using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientProcessTransition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProcessTransition>(entity =>
        {
            entity.ToTable("ClientProcessTransitions", "CRM");
            entity.HasKey(transition => transition.Id);
            entity.HasQueryFilter(transition => transition.ClientProcessStep != null);

            entity.HasIndex(transition => transition.ClientProcessStepId);
            entity.HasIndex(transition => transition.NextClientProcessStepId);

            entity.Property(transition => transition.OutcomeKey).HasMaxLength(128).IsRequired();
            entity.Property(transition => transition.OutcomeLabel).HasMaxLength(256).IsRequired();
            entity.Property(transition => transition.TerminalStatus).HasConversion<string>().HasMaxLength(64);
            entity.Property(transition => transition.TerminalStage).HasConversion<string>().HasMaxLength(64);
            entity.Property(transition => transition.CreatedBy).HasMaxLength(256);
            entity.Property(transition => transition.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(transition => transition.ClientProcessStep)
                .WithMany(step => step.OutgoingTransitions)
                .HasForeignKey(transition => transition.ClientProcessStepId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(transition => transition.NextClientProcessStep)
                .WithMany(step => step.IncomingTransitions)
                .HasForeignKey(transition => transition.NextClientProcessStepId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
