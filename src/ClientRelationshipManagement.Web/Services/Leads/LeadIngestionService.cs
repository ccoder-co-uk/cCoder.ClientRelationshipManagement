using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.Web.Services.Processes;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class LeadIngestionService(
    IPlatformDbContextFactory dbContextFactory,
    IWorkflowAutomationService workflowAutomationService,
    ISSOAuthInfo authInfo)
    : ILeadIngestionService
{
    public async ValueTask<PlatformEntities.Lead> CreateLeadAsync(
        CreateLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RawCompanyName))
            throw new InvalidOperationException("A lead requires at least a raw company name.");

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.Lead lead = new()
        {
            Id = Guid.NewGuid(),
            TenantId = ResolveTenantId(command.TenantId),
            SourceSystem = FirstNonEmpty(command.SourceSystem, "manual"),
            SourceRecordId = command.SourceRecordId,
            SourceFileName = command.SourceFileName,
            Status = LeadStatus.Imported,
            RawCompanyName = command.RawCompanyName.Trim(),
            RawTradingName = Normalize(command.RawTradingName),
            RawCompanyNumber = Normalize(command.RawCompanyNumber),
            RawVatNumber = Normalize(command.RawVatNumber),
            RawWebsiteUrl = Normalize(command.RawWebsiteUrl),
            RawContactEmailAddress = Normalize(command.RawContactEmailAddress),
            RawContactPhoneNumber = Normalize(command.RawContactPhoneNumber),
            RawAddressText = Normalize(command.RawAddressText),
            QualificationNotes = Normalize(command.QualificationNotes),
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        context.Leads.Add(lead);

        if (!string.IsNullOrWhiteSpace(command.ContactName)
            || !string.IsNullOrWhiteSpace(command.ContactEmailAddress)
            || !string.IsNullOrWhiteSpace(command.ContactPhoneNumber))
        {
            context.LeadContacts.Add(new PlatformEntities.LeadContact
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                IsPrimary = true,
                Name = FirstNonEmpty(command.ContactName, "Imported contact"),
                Position = Normalize(command.ContactPosition),
                EmailAddress = Normalize(command.ContactEmailAddress),
                PhoneNumber = Normalize(command.ContactPhoneNumber),
                LinkedInUrl = Normalize(command.ContactLinkedInUrl),
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = now,
                LastUpdated = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await workflowAutomationService.EnsureCoverageAsync(leadId: lead.Id, forceCreate: true, cancellationToken: cancellationToken);
        return lead;
    }

    public async ValueTask<LeadImportResult> ImportCsvAsync(
        ImportLeadCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Content);

        using StreamReader reader = new(command.Content, leaveOpen: true);
        string csv = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(csv))
            return new LeadImportResult();

        List<string[]> rows = csv
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseCsvLine)
            .Where(columns => columns.Length > 0)
            .ToList();

        if (rows.Count <= 1)
            return new LeadImportResult();

        Dictionary<string, int> headerLookup = rows[0]
            .Select((value, index) => new { Name = NormalizeHeader(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        List<string> warnings = [];
        int importedCount = 0;

        for (int index = 1; index < rows.Count; index++)
        {
            string[] row = rows[index];
            string companyName = GetValue(row, headerLookup, "companyname", "name", "rawcompanyname");

            if (string.IsNullOrWhiteSpace(companyName))
            {
                warnings.Add($"Row {index + 1} was skipped because no company name was supplied.");
                continue;
            }

            await CreateLeadAsync(new CreateLeadCommand
            {
                TenantId = command.TenantId,
                SourceSystem = FirstNonEmpty(command.SourceSystem, "csv-import"),
                SourceFileName = command.FileName,
                SourceRecordId = $"csv:{index}",
                RawCompanyName = companyName,
                RawTradingName = GetValue(row, headerLookup, "tradingname"),
                RawCompanyNumber = GetValue(row, headerLookup, "companynumber"),
                RawVatNumber = GetValue(row, headerLookup, "vatnumber", "vat"),
                RawWebsiteUrl = GetValue(row, headerLookup, "website", "websiteurl"),
                RawContactEmailAddress = GetValue(row, headerLookup, "contactemail", "email"),
                RawContactPhoneNumber = GetValue(row, headerLookup, "contactphone", "phone"),
                RawAddressText = GetValue(row, headerLookup, "address", "registeredoffice"),
                QualificationNotes = GetValue(row, headerLookup, "notes", "qualificationnotes"),
                ContactName = GetValue(row, headerLookup, "contactname", "namecontact"),
                ContactPosition = GetValue(row, headerLookup, "contactposition", "position"),
                ContactEmailAddress = GetValue(row, headerLookup, "contactemail", "email"),
                ContactPhoneNumber = GetValue(row, headerLookup, "contactphone", "phone"),
                ContactLinkedInUrl = GetValue(row, headerLookup, "linkedin", "linkedinurl")
            }, cancellationToken);

            importedCount++;
        }

        return new LeadImportResult
        {
            ImportedLeadCount = importedCount,
            Warnings = warnings
        };
    }

    static string[] ParseCsvLine(string line)
    {
        List<string> values = [];
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int index = 0; index < line.Length; index++)
        {
            char currentChar = line[index];

            if (currentChar == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (currentChar == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        values.Add(current.ToString().Trim());
        return [.. values];
    }

    static string NormalizeHeader(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();

    static string GetValue(string[] row, IReadOnlyDictionary<string, int> headers, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (headers.TryGetValue(key, out int index) && index < row.Length)
                return Normalize(row[index]);
        }

        return null;
    }

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    static string ResolveTenantId(string tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? "default"
            : tenantId.Trim();

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId) || string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase)
            ? "system"
            : authInfo.SSOUserId;
}
