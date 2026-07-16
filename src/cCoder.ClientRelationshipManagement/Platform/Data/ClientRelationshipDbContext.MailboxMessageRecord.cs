using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<MailboxMessageRecord> MailboxMessageRecords { get; set; }
    static void ConfigureMailboxMessageRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailboxMessageRecord>().ToTable("MailboxMessageRecords", CrmSchema);
        ConfigureAuditable<MailboxMessageRecord>(modelBuilder);

        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.ExternalId).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.InternetMessageId).HasMaxLength(512);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.ConversationId).HasMaxLength(512);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.InReplyTo).HasMaxLength(512);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.References);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.FromAddress).HasMaxLength(320);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.ToAddresses).HasMaxLength(2048);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.CcAddresses).HasMaxLength(2048);
        modelBuilder.Entity<MailboxMessageRecord>().Property(entity => entity.Subject).HasMaxLength(1024);

        modelBuilder.Entity<MailboxMessageRecord>().HasIndex(entity => entity.ExternalId).IsUnique();
        modelBuilder.Entity<MailboxMessageRecord>().HasIndex(entity => entity.InternetMessageId);
        modelBuilder.Entity<MailboxMessageRecord>().HasIndex(entity => entity.ConversationId);
        modelBuilder.Entity<MailboxMessageRecord>().HasIndex(entity => new { entity.FromAddress, entity.ReceivedOn });
        modelBuilder.Entity<MailboxMessageRecord>().HasIndex(entity => entity.ReceivedOn);
    }
}
