using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Opportunity> Opportunities { get; set; }
    static void ConfigureOpportunity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Opportunity>().ToTable("Opportunities", CrmSchema);
        ConfigureAuditable<Opportunity>(modelBuilder);

        modelBuilder.Entity<Opportunity>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Opportunity>().HasIndex(entity => new { entity.TenantCompanyRelationshipId, entity.Stage });

        modelBuilder.Entity<Opportunity>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Opportunities)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Opportunity>()
            .HasOne(entity => entity.PrimaryRelationshipContact)
            .WithMany(contact => contact.Opportunities)
            .HasForeignKey(entity => entity.PrimaryRelationshipContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
