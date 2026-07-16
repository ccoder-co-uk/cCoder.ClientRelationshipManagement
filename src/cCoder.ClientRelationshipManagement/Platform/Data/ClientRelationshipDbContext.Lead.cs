using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Lead> Leads { get; set; }
    static void ConfigureLead(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>().ToTable("Leads", LeadsSchema);
        ConfigureAuditable<Lead>(modelBuilder);

        modelBuilder.Entity<Lead>().Property(entity => entity.SourceSystem).HasMaxLength(128);
        modelBuilder.Entity<Lead>().Property(entity => entity.SourceRecordId).HasMaxLength(128);
        modelBuilder.Entity<Lead>().Property(entity => entity.SourceFileName).HasMaxLength(256);
        modelBuilder.Entity<Lead>().Property(entity => entity.TenantId).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<Lead>().Property(entity => entity.RawCompanyName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Lead>().Property(entity => entity.RawTradingName).HasMaxLength(512);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawCompanyNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawVatNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawWebsiteUrl).HasMaxLength(512);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawContactEmailAddress).HasMaxLength(256);
        modelBuilder.Entity<Lead>().Property(entity => entity.RawContactPhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<Lead>().Property(entity => entity.RankingRationale).HasMaxLength(2048);
        modelBuilder.Entity<Lead>().HasIndex(entity => new { entity.TenantId, entity.Status });

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.Leads)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Company)
            .WithMany()
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ConvertedLeads)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lead>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Leads)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
