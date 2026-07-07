using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientActivity>(entity =>
        {
            entity.ToTable("ClientActivities", "CRM");
            entity.HasKey(activity => activity.Id);
            entity.HasQueryFilter(activity => activity.Client != null);
            entity.HasIndex(activity => activity.ActivityOn);

            entity.Property(activity => activity.Type).HasConversion<string>().HasMaxLength(64);
            entity.Property(activity => activity.Direction).HasConversion<string>().HasMaxLength(64);
            entity.Property(activity => activity.Summary).HasMaxLength(2048);
            entity.Property(activity => activity.Outcome).HasMaxLength(2048);
            entity.Property(activity => activity.NextAction).HasMaxLength(1024);
            entity.Property(activity => activity.CreatedBy).HasMaxLength(256);

            entity.HasOne(activity => activity.Client)
                .WithMany(client => client.Activities)
                .HasForeignKey(activity => activity.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.ClientContact)
                .WithMany(contact => contact.Activities)
                .HasForeignKey(activity => activity.ClientContactId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.ClientOpportunity)
                .WithMany(opportunity => opportunity.Activities)
                .HasForeignKey(activity => activity.ClientOpportunityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activity => activity.ClientMaterial)
                .WithMany(material => material.Activities)
                .HasForeignKey(activity => activity.ClientMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
