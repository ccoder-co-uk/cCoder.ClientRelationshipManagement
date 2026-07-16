using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Material> Materials { get; set; }
    static void ConfigureMaterial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>().ToTable("Materials", CrmSchema);
        ConfigureAuditable<Material>(modelBuilder);

        modelBuilder.Entity<Material>().Property(entity => entity.LegacyId).HasMaxLength(128);
        modelBuilder.Entity<Material>().Property(entity => entity.Name).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Material>().Property(entity => entity.FilePath).HasMaxLength(1024);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.Materials)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.Opportunity)
            .WithMany(opportunity => opportunity.Materials)
            .HasForeignKey(entity => entity.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.Materials)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Material>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.Materials)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
