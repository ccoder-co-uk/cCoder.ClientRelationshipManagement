using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<CompanyContact> CompanyContacts { get; set; }
    static void ConfigureCompanyContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyContact>().ToTable("CompanyContacts", MasterdataSchema);
        ConfigureAuditable<CompanyContact>(modelBuilder);

        modelBuilder.Entity<CompanyContact>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.Position).HasMaxLength(256);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.EmailAddress).HasMaxLength(256);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.PhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<CompanyContact>().Property(entity => entity.LinkedInUrl).HasMaxLength(512);

        modelBuilder.Entity<CompanyContact>()
            .HasOne(entity => entity.Company)
            .WithMany(company => company.Contacts)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
