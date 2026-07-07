using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientProcessStep(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProcessStep>(entity =>
        {
            entity.ToTable("ClientProcessSteps", "CRM");
            entity.HasKey(step => step.Id);
            entity.HasQueryFilter(step => step.ClientProcessDefinition != null);

            entity.HasIndex(step => step.ClientProcessDefinitionId);
            entity.HasIndex(step => new { step.ClientProcessDefinitionId, step.Sequence });

            entity.Property(step => step.Key).HasMaxLength(128).IsRequired();
            entity.Property(step => step.Name).HasMaxLength(256).IsRequired();
            entity.Property(step => step.ActionType).HasConversion<string>().HasMaxLength(64);
            entity.Property(step => step.StatusOnActivate).HasConversion<string>().HasMaxLength(64);
            entity.Property(step => step.StageOnActivate).HasConversion<string>().HasMaxLength(64);
            entity.Property(step => step.TaskTitleTemplate).HasMaxLength(512).IsRequired();
            entity.Property(step => step.TaskInstructionsTemplate).HasMaxLength(4096);
            entity.Property(step => step.EmailSubjectTemplate).HasMaxLength(512);
            entity.Property(step => step.EmailBodyTemplate).HasMaxLength(4096);
            entity.Property(step => step.CallScriptTemplate).HasMaxLength(4096);
            entity.Property(step => step.QuestionSetTemplate).HasMaxLength(4096);
            entity.Property(step => step.CreatedBy).HasMaxLength(256);
            entity.Property(step => step.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(step => step.ClientProcessDefinition)
                .WithMany(definition => definition.Steps)
                .HasForeignKey(step => step.ClientProcessDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
