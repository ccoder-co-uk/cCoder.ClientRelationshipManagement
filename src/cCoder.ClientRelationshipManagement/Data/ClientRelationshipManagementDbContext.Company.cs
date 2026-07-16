using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies", "CRM");
            entity.HasKey(company => company.Id);
            entity.HasQueryFilter(company => company.Client != null);
            entity.HasIndex(company => company.ClientId).IsUnique();
            entity.HasIndex(company => company.CompanyNumber);

            entity.Property(company => company.Name).HasMaxLength(256).IsRequired();
            entity.Property(company => company.LegalEntityName).HasMaxLength(256);
            entity.Property(company => company.TradingName).HasMaxLength(256);
            entity.Property(company => company.CompanyNumber).HasMaxLength(64);
            entity.Property(company => company.VatNumber).HasMaxLength(64);
            entity.Property(company => company.ContactEmailAddress).HasMaxLength(320);
            entity.Property(company => company.ContactPhoneNumber).HasMaxLength(64);
            entity.Property(company => company.WebsiteUrl).HasMaxLength(512);
            entity.Property(company => company.CreatedBy).HasMaxLength(256);
            entity.Property(company => company.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(company => company.RegisteredAddress)
                .WithMany(address => address.Companies)
                .HasForeignKey(company => company.RegisteredAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(company => company.Client)
                .WithOne(client => client.Company)
                .HasForeignKey<Company>(company => company.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
