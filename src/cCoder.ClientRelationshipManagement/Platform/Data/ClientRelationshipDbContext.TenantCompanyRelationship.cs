using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<TenantCompanyRelationship> TenantCompanyRelationships { get; set; }
    static void ConfigureTenantCompanyRelationship(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantCompanyRelationship>().ToTable("TenantCompanyRelationships", CrmSchema);
        ConfigureAuditable<TenantCompanyRelationship>(modelBuilder);

        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.AccountOwnerUserId).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.AccountOwnerDisplayName).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().Property(entity => entity.LeadSource).HasMaxLength(256);
        modelBuilder.Entity<TenantCompanyRelationship>().HasIndex(entity => new { entity.TenantId, entity.Status });

        modelBuilder.Entity<TenantCompanyRelationship>()
            .HasOne(entity => entity.Company)
            .WithMany(company => company.Relationships)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
