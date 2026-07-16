using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<AgentMessageEntry> AgentMessageEntries { get; set; }
    static void ConfigureAgentMessageEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentMessageEntry>().ToTable("AgentMessageEntries", CrmSchema);
        ConfigureAuditable<AgentMessageEntry>(modelBuilder);
        modelBuilder.Entity<AgentMessageEntry>().Property(entity => entity.Role).HasMaxLength(32).IsRequired();
        modelBuilder.Entity<AgentMessageEntry>().Property(entity => entity.Body).IsRequired();
        modelBuilder.Entity<AgentMessageEntry>().HasIndex(entity => new { entity.AgentMessageId, entity.CreatedOn });
        modelBuilder.Entity<AgentMessageEntry>()
            .HasOne(entity => entity.AgentMessage)
            .WithMany(message => message.Entries)
            .HasForeignKey(entity => entity.AgentMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
