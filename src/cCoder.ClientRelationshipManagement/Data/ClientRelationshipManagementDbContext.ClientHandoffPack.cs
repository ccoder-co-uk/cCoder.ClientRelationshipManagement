using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Data;

public partial class ClientRelationshipManagementDbContext
{
    static void ConfigureClientHandoffPack(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientHandoffPack>(entity =>
        {
            entity.ToTable("ClientHandoffPacks", "CRM");
            entity.HasKey(handoff => handoff.Id);
            entity.HasQueryFilter(handoff => handoff.Client != null);

            entity.Property(handoff => handoff.SignedContractPath).HasMaxLength(1024);
            entity.Property(handoff => handoff.LegalEntity).HasMaxLength(256);
            entity.Property(handoff => handoff.PrimaryCommercialContact).HasMaxLength(256);
            entity.Property(handoff => handoff.PrimaryOperationalContact).HasMaxLength(256);
            entity.Property(handoff => handoff.PrimaryTechnicalContact).HasMaxLength(256);
            entity.Property(handoff => handoff.AgreedScope).HasMaxLength(4096);
            entity.Property(handoff => handoff.CommercialTermsSummary).HasMaxLength(4096);
            entity.Property(handoff => handoff.PromisedOutcomes).HasMaxLength(4096);
            entity.Property(handoff => handoff.KnownRisks).HasMaxLength(4096);
            entity.Property(handoff => handoff.OnboardingOwner).HasMaxLength(256);
            entity.Property(handoff => handoff.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(handoff => handoff.CreatedBy).HasMaxLength(256);
            entity.Property(handoff => handoff.LastUpdatedBy).HasMaxLength(256);

            entity.HasOne(handoff => handoff.Client)
                .WithMany(client => client.HandoffPacks)
                .HasForeignKey(handoff => handoff.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(handoff => handoff.ClientOpportunity)
                .WithMany(opportunity => opportunity.HandoffPacks)
                .HasForeignKey(handoff => handoff.ClientOpportunityId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
