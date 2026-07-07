using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientContact>(entity =>
        {
            entity.ToTable("ClientContacts", "CRM");
            entity.HasKey(contact => contact.Id);
            entity.HasQueryFilter(contact => contact.Client != null);
            entity.HasIndex(contact => contact.EmailAddress);

            entity.Property(contact => contact.Name).HasMaxLength(256).IsRequired();
            entity.Property(contact => contact.Position).HasMaxLength(256);
            entity.Property(contact => contact.EmailAddress).HasMaxLength(320);
            entity.Property(contact => contact.PhoneNumber).HasMaxLength(64);
            entity.Property(contact => contact.LinkedInUrl).HasMaxLength(512);
            entity.Property(contact => contact.Source).HasMaxLength(256);
            entity.Property(contact => contact.RelationshipRoute).HasMaxLength(512);
            entity.Property(contact => contact.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(contact => contact.Notes).HasMaxLength(2048);
            entity.Property(contact => contact.CreatedBy).HasMaxLength(256);
            entity.Property(contact => contact.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(contact => contact.Client)
                .WithMany(client => client.Contacts)
                .HasForeignKey(contact => contact.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
