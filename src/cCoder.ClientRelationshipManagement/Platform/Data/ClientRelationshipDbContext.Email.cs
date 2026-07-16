using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Email> Emails { get; set; }
    static void ConfigureEmail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Email>().ToTable("Emails", CrmSchema);
        ConfigureAuditable<Email>(modelBuilder);

        modelBuilder.Entity<Email>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Email>().Property(entity => entity.SenderUserId).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.FromDisplayName).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.FromEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Email>().Property(entity => entity.Subject).HasMaxLength(512).IsRequired();

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Emails)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Emails)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Emails)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.Material)
            .WithMany(material => material.Emails)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Email>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Emails)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
