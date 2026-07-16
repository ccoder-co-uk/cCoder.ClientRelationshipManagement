using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.Web.Services.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class AuthorityImportTests
{
    [Fact]
    public void Fingerprint_IsStableForEquivalentAuthorityRecords()
    {
        object[] first = ["Example Ltd", "01234567", new DateTime(2001, 2, 3), 4, DBNull.Value];
        object[] second = [" Example Ltd ", "01234567", new DateTime(2001, 2, 3), 4, null];

        string firstHash = AuthorityRecordFingerprint.ComputeHex(first);
        string secondHash = AuthorityRecordFingerprint.ComputeHex(second);

        firstHash.Should().Be(secondHash);
        firstHash.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Fingerprint_ChangesWhenAuthorityDataChanges()
    {
        string original = AuthorityRecordFingerprint.ComputeHex("Example Ltd", "active", "small");
        string changed = AuthorityRecordFingerprint.ComputeHex("Example Ltd", "dissolved", "small");

        changed.Should().NotBe(original);
    }

    [Fact]
    public void CompanyAuthorityIdentity_IsUniqueAndFiltered()
    {
        DbContextOptions<ClientRelationshipDbContext> options = new DbContextOptionsBuilder<ClientRelationshipDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=authority-model-test;Trusted_Connection=True")
            .Options;
        using ClientRelationshipDbContext context = new(options);

        var company = context.Model.FindEntityType(typeof(Company));
        var index = company.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Company.SourceSystem), nameof(Company.SourceRecordId)]));

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("[SourceSystem] IS NOT NULL AND [SourceRecordId] IS NOT NULL");
    }
}
