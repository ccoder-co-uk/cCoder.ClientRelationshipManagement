using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientProcessTask(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProcessTask>(entity =>
        {
            entity.ToTable("ClientProcessTasks", "CRM");
            entity.HasKey(task => task.Id);
            entity.HasQueryFilter(task => task.Client != null);

            entity.HasIndex(task => task.ClientId);
            entity.HasIndex(task => task.ClientProcessInstanceId);
            entity.HasIndex(task => task.ClientProcessStepId);
            entity.HasIndex(task => task.State);
            entity.HasIndex(task => task.DueOn);
            entity.HasIndex(task => task.EmailId).IsUnique()
                .HasFilter("[EmailId] IS NOT NULL");

            entity.Property(task => task.ActionType).HasConversion<string>().HasMaxLength(64);
            entity.Property(task => task.State).HasConversion<string>().HasMaxLength(64);
            entity.Property(task => task.RenderedTitle).HasMaxLength(512).IsRequired();
            entity.Property(task => task.RenderedInstructions).HasMaxLength(4096);
            entity.Property(task => task.RenderedEmailSubject).HasMaxLength(512);
            entity.Property(task => task.RenderedEmailBody).HasMaxLength(4096);
            entity.Property(task => task.RenderedCallScript).HasMaxLength(4096);
            entity.Property(task => task.RenderedQuestionSet).HasMaxLength(4096);
            entity.Property(task => task.CompletionOutcomeKey).HasMaxLength(128);
            entity.Property(task => task.CompletedBy).HasMaxLength(256);
            entity.Property(task => task.CreatedBy).HasMaxLength(256);
            entity.Property(task => task.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(task => task.Client)
                .WithMany(client => client.ProcessTasks)
                .HasForeignKey(task => task.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.ClientProcessInstance)
                .WithMany(instance => instance.Tasks)
                .HasForeignKey(task => task.ClientProcessInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.ClientProcessStep)
                .WithMany(step => step.Tasks)
                .HasForeignKey(task => task.ClientProcessStepId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.Email)
                .WithMany()
                .HasForeignKey(task => task.EmailId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
