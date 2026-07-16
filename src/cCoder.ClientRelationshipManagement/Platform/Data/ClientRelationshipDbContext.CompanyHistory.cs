using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<CompanyHistoryItem> CompanyHistory { get; set; }
    static void ConfigureCompanyHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyHistoryItem>().ToTable("CompanyHistory", MasterdataSchema);
        ConfigureAuditable<CompanyHistoryItem>(modelBuilder);
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.Lane).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.EventType).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.Summary).HasMaxLength(1024).IsRequired();
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.FactKey).HasMaxLength(128);
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.Confidence).HasMaxLength(32);
        modelBuilder.Entity<CompanyHistoryItem>().Property(entity => entity.SourceType).HasMaxLength(128);
        modelBuilder.Entity<CompanyHistoryItem>()
            .HasOne(entity => entity.Company)
            .WithMany(entity => entity.History)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompanyHistoryItem>()
            .HasIndex(entity => new { entity.CompanyId, entity.TenantId, entity.OccurredOn });
        modelBuilder.Entity<CompanyHistoryItem>()
            .HasIndex(entity => new { entity.CompanyId, entity.FactKey, entity.OccurredOn });
        modelBuilder.Entity<CompanyHistoryItem>()
            .HasIndex(entity => entity.ProcessTaskId);
    }
}
