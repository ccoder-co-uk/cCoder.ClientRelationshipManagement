using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<Import> Imports { get; set; }
    static void ConfigureImport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Import>().ToTable("Imports", LeadsSchema);
        ConfigureAuditable<Import>(modelBuilder);

        modelBuilder.Entity<Import>().Property(entity => entity.OriginalFileName).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Import>().Property(entity => entity.ContentType).HasMaxLength(256);
        modelBuilder.Entity<Import>().Property(entity => entity.StoredFilePath).HasMaxLength(1024);
        modelBuilder.Entity<Import>().Property(entity => entity.StoredObjectKey).HasMaxLength(512);
        modelBuilder.Entity<Import>().Property(entity => entity.WarningSummary).HasMaxLength(4096);
        modelBuilder.Entity<Import>().Property(entity => entity.ErrorSummary).HasMaxLength(4096);
        modelBuilder.Entity<Import>().Property(entity => entity.ProcessingCheckpoint).HasMaxLength(128);
        modelBuilder.Entity<Import>().Property(entity => entity.UploadSessionId).HasMaxLength(128);
        modelBuilder.Entity<Import>().HasIndex(entity => entity.JobStatus);
        modelBuilder.Entity<Import>().HasIndex(entity => entity.UploadSessionId);

        modelBuilder.Entity<Import>()
            .HasOne(entity => entity.Source)
            .WithMany(source => source.Imports)
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
