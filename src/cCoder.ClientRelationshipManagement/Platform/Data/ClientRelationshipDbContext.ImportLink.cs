using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ImportLink> ImportLinks { get; set; }
    static void ConfigureImportLink(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportLink>().ToTable("ImportLinks", LeadsSchema);
        ConfigureAuditable<ImportLink>(modelBuilder);

        modelBuilder.Entity<ImportLink>().Property(entity => entity.SourceRowKey).HasMaxLength(256);
        modelBuilder.Entity<ImportLink>().HasIndex(entity => new { entity.ImportId, entity.SourceRowNumber });
        modelBuilder.Entity<ImportLink>().HasIndex(entity => new { entity.SourceId, entity.SourceRowKey });

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Import)
            .WithMany(import => import.Links)
            .HasForeignKey(entity => entity.ImportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.ImportLinks)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Company)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.Lead)
            .WithMany()
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ImportLink>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
