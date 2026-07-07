using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    void ConfigureClient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients", "CRM");
            entity.HasKey(client => client.Id);
            entity.HasQueryFilter(client =>
                authInfo == null || ReadableClientTenantIds.Contains(client.TenantId));
            entity.HasIndex(client => client.TenantId);

            entity.Property(client => client.TenantId).HasMaxLength(128).IsRequired();
            entity.Property(client => client.AccountOwner).HasMaxLength(256);
            entity.Property(client => client.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(client => client.CurrentStage).HasConversion<string>().HasMaxLength(64);
            entity.Property(client => client.Priority).HasConversion<string>().HasMaxLength(64);
            entity.Property(client => client.LeadSource).HasMaxLength(256);
            entity.Property(client => client.InitialRoute).HasMaxLength(512);
            entity.Property(client => client.FitScore).HasPrecision(5, 2);
            entity.Property(client => client.OpportunitySummary).HasMaxLength(2048);
            entity.Property(client => client.PreferredOpeningAngle).HasMaxLength(2048);
            entity.Property(client => client.NextAction).HasMaxLength(1024);
            entity.Property(client => client.CreatedBy).HasMaxLength(256);
            entity.Property(client => client.LastUpdatedBy).HasMaxLength(256);
        });
    }
}
