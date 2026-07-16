using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Source> Sources { get; set; }
    static void ConfigureSource(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Source>().ToTable("Sources", MasterdataSchema);
        ConfigureAuditable<Source>(modelBuilder);

        modelBuilder.Entity<Source>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<Source>().Property(entity => entity.CountryCode).HasMaxLength(16);
        modelBuilder.Entity<Source>().Property(entity => entity.Notes).HasMaxLength(2048);
        modelBuilder.Entity<Source>()
            .HasIndex(entity => new { entity.Name, entity.CountryCode })
            .IsUnique();
    }
}
