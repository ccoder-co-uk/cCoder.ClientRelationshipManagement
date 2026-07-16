using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<RelationshipContact> RelationshipContacts { get; set; }
    static void ConfigureRelationshipContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RelationshipContact>().ToTable("RelationshipContacts", CrmSchema);
        ConfigureAuditable<RelationshipContact>(modelBuilder);

        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.RelationshipRoute).HasMaxLength(512);
        modelBuilder.Entity<RelationshipContact>().Property(entity => entity.Source).HasMaxLength(256);
        modelBuilder.Entity<RelationshipContact>()
            .HasIndex(entity => new { entity.TenantCompanyRelationshipId, entity.CompanyContactId })
            .IsUnique();

        modelBuilder.Entity<RelationshipContact>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Contacts)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RelationshipContact>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.RelationshipContacts)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
