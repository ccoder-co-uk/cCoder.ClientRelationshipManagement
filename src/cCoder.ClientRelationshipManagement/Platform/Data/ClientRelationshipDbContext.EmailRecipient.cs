using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<EmailRecipient> EmailRecipients { get; set; }
    static void ConfigureEmailRecipient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailRecipient>().ToTable("EmailRecipients", CrmSchema);
        ConfigureAuditable<EmailRecipient>(modelBuilder);

        modelBuilder.Entity<EmailRecipient>().Property(entity => entity.Address).HasMaxLength(256).IsRequired();

        modelBuilder.Entity<EmailRecipient>()
            .HasOne(entity => entity.Email)
            .WithMany(email => email.Recipients)
            .HasForeignKey(entity => entity.EmailId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmailRecipient>()
            .HasOne(entity => entity.CompanyContact)
            .WithMany(contact => contact.EmailRecipients)
            .HasForeignKey(entity => entity.CompanyContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
