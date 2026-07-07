using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Leads;

public interface ILeadIngestionService
{
    ValueTask<PlatformEntities.Lead> CreateLeadAsync(CreateLeadCommand command, CancellationToken cancellationToken = default);
    ValueTask<LeadImportResult> ImportCsvAsync(ImportLeadCsvCommand command, CancellationToken cancellationToken = default);
}

public sealed class CreateLeadCommand
{
    public string TenantId { get; init; }
    public string SourceSystem { get; init; }
    public string SourceRecordId { get; init; }
    public string SourceFileName { get; init; }
    public string RawCompanyName { get; init; }
    public string RawTradingName { get; init; }
    public string RawCompanyNumber { get; init; }
    public string RawVatNumber { get; init; }
    public string RawWebsiteUrl { get; init; }
    public string RawContactEmailAddress { get; init; }
    public string RawContactPhoneNumber { get; init; }
    public string RawAddressText { get; init; }
    public string QualificationNotes { get; init; }
    public string ContactName { get; init; }
    public string ContactPosition { get; init; }
    public string ContactEmailAddress { get; init; }
    public string ContactPhoneNumber { get; init; }
    public string ContactLinkedInUrl { get; init; }
}

public sealed class ImportLeadCsvCommand
{
    public string TenantId { get; init; }
    public string SourceSystem { get; init; }
    public string FileName { get; init; }
    public Stream Content { get; init; }
}

public sealed class LeadImportResult
{
    public int ImportedLeadCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
