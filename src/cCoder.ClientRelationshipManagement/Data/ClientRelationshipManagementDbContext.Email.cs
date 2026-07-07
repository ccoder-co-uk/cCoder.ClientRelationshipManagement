using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureEmail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Email>(entity =>
        {
            entity.ToTable("Emails", "CRM");
            entity.HasKey(email => email.Id);
            entity.HasQueryFilter(email => email.Client != null);
            entity.HasIndex(email => email.ClientId);
            entity.HasIndex(email => email.State);
            entity.HasIndex(email => email.ScheduledSendTimeUtc);
            entity.HasIndex(email => email.ClientMaterialId).IsUnique()
                .HasFilter("[ClientMaterialId] IS NOT NULL");

            entity.Property(email => email.SenderUserId).HasMaxLength(256);
            entity.Property(email => email.FromDisplayName).HasMaxLength(256);
            entity.Property(email => email.FromEmailAddress).HasMaxLength(512);
            entity.Property(email => email.ReplyToAddresses).HasMaxLength(2048);
            entity.Property(email => email.ToAddresses).HasMaxLength(2048);
            entity.Property(email => email.CcAddresses).HasMaxLength(2048);
            entity.Property(email => email.BccAddresses).HasMaxLength(2048);
            entity.Property(email => email.Subject).HasMaxLength(256).IsRequired();
            entity.Property(email => email.State).HasConversion<string>().HasMaxLength(64);
            entity.Property(email => email.ApprovedBy).HasMaxLength(256);
            entity.Property(email => email.ExternalMessageId).HasMaxLength(512);
            entity.Property(email => email.LastError).HasMaxLength(2048);
            entity.Property(email => email.CreatedBy).HasMaxLength(256);
            entity.Property(email => email.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(email => email.Client)
                .WithMany(client => client.Emails)
                .HasForeignKey(email => email.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(email => email.ClientMaterial)
                .WithOne(material => material.Email)
                .HasForeignKey<Email>(email => email.ClientMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(email => email.SentToContact)
                .WithMany(contact => contact.Emails)
                .HasForeignKey(email => email.SentToContactId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
