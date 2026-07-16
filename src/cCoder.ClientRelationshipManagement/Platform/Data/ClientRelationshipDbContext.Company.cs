using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Company> Companies { get; set; }
    static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().ToTable("Companies", MasterdataSchema);
        ConfigureAuditable<Company>(modelBuilder);

        modelBuilder.Entity<Company>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.SourceRecordId).HasMaxLength(128);
        modelBuilder.Entity<Company>().Property(entity => entity.AuthorityRecordHash).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.OfficialName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Company>().Property(entity => entity.LegalEntityName).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.TradingName).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.VatNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyCategory).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.CompanyStatus).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.CountryOfOrigin).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.PrimarySicCodes).HasMaxLength(2048);
        modelBuilder.Entity<Company>().Property(entity => entity.RegistryUri).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.WebsiteUrl).HasMaxLength(512);
        modelBuilder.Entity<Company>().Property(entity => entity.ContactEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Company>().Property(entity => entity.ContactPhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<Company>().Property(entity => entity.RevenueCurrency).HasMaxLength(16);
        modelBuilder.Entity<Company>().Property(entity => entity.RankingRationale).HasMaxLength(2048);
        modelBuilder.Entity<Company>().Property(entity => entity.ProspectingSuppressedReason).HasMaxLength(2048);
        modelBuilder.Entity<Company>().HasIndex(entity => entity.CompanyNumber);
        modelBuilder.Entity<Company>().HasIndex(entity => entity.OfficialName);
        modelBuilder.Entity<Company>().HasIndex(entity => entity.VatNumber);
        modelBuilder.Entity<Company>()
            .HasIndex(entity => new { entity.SourceSystem, entity.SourceRecordId })
            .IsUnique()
            .HasFilter("[SourceSystem] IS NOT NULL AND [SourceRecordId] IS NOT NULL");
        modelBuilder.Entity<Company>()
            .HasIndex(entity => new { entity.SourceSystem, entity.IsProspectingSuppressed, entity.RankingScore });

        modelBuilder.Entity<Company>()
            .HasOne(entity => entity.RegisteredAddress)
            .WithMany(address => address.RegisteredCompanies)
            .HasForeignKey(entity => entity.RegisteredAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
