using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Address> Addresses { get; set; }
    static void ConfigureAddress(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>().ToTable("Addresses", MasterdataSchema);
        ConfigureAuditable<Address>(modelBuilder);

        modelBuilder.Entity<Address>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Address>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Address>().Property(entity => entity.CountryId).HasMaxLength(64);
        modelBuilder.Entity<Address>()
            .HasIndex(entity => new { entity.SourceSystem, entity.LegacyId })
            .IsUnique()
            .HasFilter("[SourceSystem] IS NOT NULL AND [LegacyId] IS NOT NULL");
    }
}
