using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<CompanyEvidence> CompanyEvidence { get; set; }

    static void ConfigureCompanyEvidence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyEvidence>().ToTable("CompanyEvidence", MasterdataSchema);
        ConfigureAuditable<CompanyEvidence>(modelBuilder);

        modelBuilder.Entity<CompanyEvidence>().Property(entity => entity.Key).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<CompanyEvidence>().Property(entity => entity.SourceUrl).HasMaxLength(2048);
        modelBuilder.Entity<CompanyEvidence>().Property(entity => entity.SourceTitle).HasMaxLength(512);
        modelBuilder.Entity<CompanyEvidence>().Property(entity => entity.Extractor).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<CompanyEvidence>().Property(entity => entity.ResourceHash).HasMaxLength(128);
        modelBuilder.Entity<CompanyEvidence>()
            .HasIndex(entity => new { entity.CompanyId, entity.Key, entity.ResourceHash });

        modelBuilder.Entity<CompanyEvidence>()
            .HasOne(entity => entity.Company)
            .WithMany(company => company.Evidence)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
