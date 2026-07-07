using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientMaterial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientMaterial>(entity =>
        {
            entity.ToTable("ClientMaterials", "CRM");
            entity.HasKey(material => material.Id);
            entity.HasQueryFilter(material => material.Client != null);

            entity.Property(material => material.Name).HasMaxLength(256).IsRequired();
            entity.Property(material => material.FilePath).HasMaxLength(1024);
            entity.Property(material => material.Type).HasConversion<string>().HasMaxLength(64);
            entity.Property(material => material.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(material => material.Notes).HasMaxLength(2048);
            entity.Property(material => material.CreatedBy).HasMaxLength(256);
            entity.Property(material => material.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(material => material.Client)
                .WithMany(client => client.Materials)
                .HasForeignKey(material => material.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(material => material.SentToContact)
                .WithMany()
                .HasForeignKey(material => material.SentToContactId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
