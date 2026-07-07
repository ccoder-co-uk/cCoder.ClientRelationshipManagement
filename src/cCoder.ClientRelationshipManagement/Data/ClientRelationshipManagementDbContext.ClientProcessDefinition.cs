using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    void ConfigureClientProcessDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProcessDefinition>(entity =>
        {
            entity.ToTable("ClientProcessDefinitions", "CRM");
            entity.HasKey(definition => definition.Id);

            entity.HasIndex(definition => definition.TenantId);
            entity.HasIndex(definition => new { definition.TenantId, definition.IsDefault });

            entity.Property(definition => definition.TenantId).HasMaxLength(128).IsRequired();
            entity.Property(definition => definition.Name).HasMaxLength(256).IsRequired();
            entity.Property(definition => definition.Description).HasMaxLength(2048);
            entity.Property(definition => definition.CreatedBy).HasMaxLength(256);
            entity.Property(definition => definition.LastUpdatedBy).HasMaxLength(256);
        });
    }
}
