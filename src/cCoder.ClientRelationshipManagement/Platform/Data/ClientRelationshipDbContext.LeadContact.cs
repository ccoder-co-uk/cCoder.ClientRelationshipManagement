using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Platform.Data;

public partial class ClientRelationshipDbContext
{
    public DbSet<LeadContact> LeadContacts { get; set; }
    static void ConfigureLeadContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadContact>().ToTable("LeadContacts", LeadsSchema);
        ConfigureAuditable<LeadContact>(modelBuilder);

        modelBuilder.Entity<LeadContact>().Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<LeadContact>().Property(entity => entity.Position).HasMaxLength(256);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.EmailAddress).HasMaxLength(256);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.PhoneNumber).HasMaxLength(64);
        modelBuilder.Entity<LeadContact>().Property(entity => entity.LinkedInUrl).HasMaxLength(512);

        modelBuilder.Entity<LeadContact>()
            .HasOne(entity => entity.Lead)
            .WithMany(lead => lead.Contacts)
            .HasForeignKey(entity => entity.LeadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
