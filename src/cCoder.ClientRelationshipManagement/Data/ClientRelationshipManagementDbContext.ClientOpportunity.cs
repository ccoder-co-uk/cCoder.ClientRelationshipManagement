using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientOpportunity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientOpportunity>(entity =>
        {
            entity.ToTable("ClientOpportunities", "CRM");
            entity.HasKey(opportunity => opportunity.Id);
            entity.HasQueryFilter(opportunity => opportunity.Client != null);

            entity.Property(opportunity => opportunity.Type).HasConversion<string>().HasMaxLength(64);
            entity.Property(opportunity => opportunity.Stage).HasConversion<string>().HasMaxLength(64);
            entity.Property(opportunity => opportunity.EstimatedAnnualValue).HasPrecision(18, 2);
            entity.Property(opportunity => opportunity.Probability).HasPrecision(5, 2);
            entity.Property(opportunity => opportunity.PainSummary).HasMaxLength(2048);
            entity.Property(opportunity => opportunity.ValueHypothesis).HasMaxLength(2048);
            entity.Property(opportunity => opportunity.DecisionProcess).HasMaxLength(2048);
            entity.Property(opportunity => opportunity.NextAction).HasMaxLength(1024);
            entity.Property(opportunity => opportunity.CreatedBy).HasMaxLength(256);
            entity.Property(opportunity => opportunity.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(opportunity => opportunity.Client)
                .WithMany(client => client.Opportunities)
                .HasForeignKey(opportunity => opportunity.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(opportunity => opportunity.PrimaryContact)
                .WithMany()
                .HasForeignKey(opportunity => opportunity.PrimaryContactId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
