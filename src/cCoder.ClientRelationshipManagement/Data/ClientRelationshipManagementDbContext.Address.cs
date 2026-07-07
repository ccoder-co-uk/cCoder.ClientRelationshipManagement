using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureAddress(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Addresses", "CRM");
            entity.HasKey(address => address.Id);
            entity.HasQueryFilter(address => address.Companies.Any(company => company.Client != null));

            entity.Property(address => address.PoBox).HasMaxLength(128);
            entity.Property(address => address.Line1).HasMaxLength(256);
            entity.Property(address => address.Line2).HasMaxLength(256);
            entity.Property(address => address.ZipOrPostalCode).HasMaxLength(64);
            entity.Property(address => address.TownOrCity).HasMaxLength(128);
            entity.Property(address => address.StateOrProvince).HasMaxLength(128);
            entity.Property(address => address.CountryId).HasMaxLength(16);
        });
    }
}
