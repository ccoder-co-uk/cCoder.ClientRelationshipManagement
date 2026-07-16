using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<ClientAccount> ClientAccounts { get; set; }
    static void ConfigureClientAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientAccount>().ToTable("ClientAccounts", CrmSchema);
        ConfigureAuditable<ClientAccount>(modelBuilder);

        modelBuilder.Entity<ClientAccount>().Property(entity => entity.AccountReference).HasMaxLength(128);

        modelBuilder.Entity<ClientAccount>()
            .HasOne(entity => entity.TenantCompanyRelationship)
            .WithMany(relationship => relationship.ClientAccounts)
            .HasForeignKey(entity => entity.TenantCompanyRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClientAccount>()
            .HasOne(entity => entity.WonOpportunity)
            .WithMany()
            .HasForeignKey(entity => entity.WonOpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
