using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientProcessInstance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProcessInstance>(entity =>
        {
            entity.ToTable("ClientProcessInstances", "CRM");
            entity.HasKey(instance => instance.Id);
            entity.HasQueryFilter(instance => instance.Client != null);

            entity.HasIndex(instance => instance.ClientId);
            entity.HasIndex(instance => instance.ClientProcessDefinitionId);
            entity.HasIndex(instance => new { instance.ClientId, instance.State });

            entity.Property(instance => instance.State).HasConversion<string>().HasMaxLength(64);
            entity.Property(instance => instance.CompletionOutcomeKey).HasMaxLength(128);
            entity.Property(instance => instance.CreatedBy).HasMaxLength(256);
            entity.Property(instance => instance.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(instance => instance.Client)
                .WithMany(client => client.ProcessInstances)
                .HasForeignKey(instance => instance.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(instance => instance.ClientProcessDefinition)
                .WithMany(definition => definition.Instances)
                .HasForeignKey(instance => instance.ClientProcessDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(instance => instance.CurrentClientProcessStep)
                .WithMany(step => step.CurrentInstances)
                .HasForeignKey(instance => instance.CurrentClientProcessStepId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(instance => instance.CurrentClientProcessTask)
                .WithMany()
                .HasForeignKey(instance => instance.CurrentClientProcessTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
