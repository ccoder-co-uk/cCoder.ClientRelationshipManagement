using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Activity> Activities { get; set; }
    static void ConfigureActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>().ToTable("Activities", CrmSchema);
        ConfigureAuditable<Activity>(modelBuilder);

        modelBuilder.Entity<Activity>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Activity>()
            .HasIndex(entity => entity.LegacyId)
            .IsUnique()
            .HasFilter("[LegacyId] IS NOT NULL");

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Activities)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Activities)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Activities)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Activities)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Activity>()
            .HasOne(entity => entity.Material)
            .WithMany(material => material.Activities)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
