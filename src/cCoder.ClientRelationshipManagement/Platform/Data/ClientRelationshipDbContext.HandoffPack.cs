using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<HandoffPack> HandoffPacks { get; set; }
    static void ConfigureHandoffPack(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HandoffPack>().ToTable("HandoffPacks", CrmSchema);
        ConfigureAuditable<HandoffPack>(modelBuilder);

        modelBuilder.Entity<HandoffPack>().Property(entity => entity.LegacyId).HasMaxLength(128);

        modelBuilder.Entity<HandoffPack>()
            .HasOne(entity => entity.ClientAccount)
            .WithMany(clientAccount => clientAccount.HandoffPacks)
            .HasForeignKey(entity => entity.ClientAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
