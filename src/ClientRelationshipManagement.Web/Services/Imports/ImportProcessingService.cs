using System.Globalization;
using System.Text;
using System.Text.Json;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

namespace ClientRelationshipManagement.Web.Services.Imports;

public sealed class ImportProcessingService(
    IHostEnvironment environment,
    IImportCoordinationService imports,
    ISalesCoordinationService sales,
    IWorkflowAutomationService workflowAutomationService,
    IOptions<ImportWorkflowOptions> options,
    ILoggingBroker<ImportProcessingService> loggingBroker)
    : IImportProcessingService
{
    public async ValueTask<int> ProcessReadyImportsAsync(CancellationToken cancellationToken = default)
    {
        Import[] readyImports = await imports.RetrieveAllImports()
            .Include(item => item.Source)
            .Where(item =>
                item.JobStatus == ImportJobStatus.Ready
                && item.UploadStatus == ImportUploadStatus.Uploaded)
            .OrderBy(item => item.MarkedReadyOn ?? item.CreatedOn)
            .Take(1)
            .ToArrayAsync(cancellationToken);

        int processedCount = 0;
        foreach (Import import in readyImports)
        {
            await ProcessImportAsync(import, cancellationToken);
            processedCount++;
        }

        return processedCount;
    }

    async ValueTask ProcessImportAsync(Import import, CancellationToken cancellationToken)
    {
        Import trackedImport = await imports.RetrieveAllImports()
            .Include(item => item.Source)
            .FirstAsync(item => item.Id == import.Id, cancellationToken);

        try
        {
            loggingBroker.LogInformation("Import processing started for {ImportId} ({FileName}).", trackedImport.Id, trackedImport.OriginalFileName);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            trackedImport.JobStatus = ImportJobStatus.Processing;
            trackedImport.ProcessingStatus = ImportProcessingStatus.Canonicalizing;
            trackedImport.ProcessingStartedOn = now;
            trackedImport.LastUpdated = now;
            trackedImport.LastUpdatedBy = "hosted-services";
            await imports.SaveAsync(cancellationToken);

            string canonicalFilePath = await CreateCanonicalCompanyFileAsync(trackedImport, cancellationToken);
            trackedImport.ProcessingStatus = ImportProcessingStatus.Merging;
            trackedImport.ProcessingCheckpoint = "canonicalized";
            await imports.SaveAsync(cancellationToken);

            int importedRows = await MergeCanonicalFileAsync(trackedImport.Id, canonicalFilePath, cancellationToken);

            Import completedImport = trackedImport;
            completedImport.ImportedRowCount = importedRows;
            completedImport.TotalRowCount = importedRows;
            completedImport.JobStatus = ImportJobStatus.Completed;
            completedImport.ProcessingStatus = ImportProcessingStatus.Completed;
            completedImport.ProcessingCheckpoint = "completed";
            completedImport.ProcessingCompletedOn = DateTimeOffset.UtcNow;
            completedImport.LastUpdated = DateTimeOffset.UtcNow;
            completedImport.LastUpdatedBy = "hosted-services";
            await imports.SaveAsync(cancellationToken);

            await workflowAutomationService.EnsureCoverageAsync(cancellationToken: cancellationToken);
            ArchiveImportFiles(completedImport.Id);

            loggingBroker.LogInformation("Import processing completed for {ImportId}. Imported {ImportedRowCount} row(s).", completedImport.Id, importedRows);
        }
        catch (Exception exception)
        {
            Import failedImport = await imports.RetrieveAllImports().FirstAsync(item => item.Id == import.Id, cancellationToken);
            failedImport.JobStatus = ImportJobStatus.Failed;
            failedImport.ProcessingStatus = ImportProcessingStatus.Failed;
            failedImport.ErrorCount++;
            failedImport.ErrorSummary = exception.Message;
            failedImport.LastUpdated = DateTimeOffset.UtcNow;
            failedImport.LastUpdatedBy = "hosted-services";
            await imports.SaveAsync(cancellationToken);
            MoveToFailed(import.Id);
            loggingBroker.LogError(exception, "Import processing failed for {ImportId}.", import.Id);
        }
    }

    async ValueTask<string> CreateCanonicalCompanyFileAsync(Import import, CancellationToken cancellationToken)
    {
        Dictionary<string, string> mapping = ReadMapping(import.MappingSnapshotJson);
        string canonicalDirectory = GetImportDirectory("Canonical", import.Id);
        Directory.CreateDirectory(canonicalDirectory);
        string canonicalFilePath = Path.Combine(canonicalDirectory, "companies.csv");

        await using StreamWriter writer = new(canonicalFilePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("SourceRowNumber,CompanyNumber,Name,VatNumber,AddressLine1,AddressLine2,TownOrCity,County,Postcode,Country,Status,Category,SicCodes,Website,ContactName,ContactEmail,ContactPhone");

        using TextFieldParser parser = CreateCsvParser(import.StoredFilePath);
        string[] headers = parser.ReadFields() ?? [];
        Dictionary<string, int> headerLookup = BuildHeaderLookup(headers);
        long rowNumber = 1;

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] row = parser.ReadFields() ?? [];
            rowNumber++;

            string companyName = GetMappedValue(row, headerLookup, mapping, "name");
            if (string.IsNullOrWhiteSpace(companyName))
                continue;

            string[] canonicalRow =
            [
                rowNumber.ToString(CultureInfo.InvariantCulture),
                GetMappedValue(row, headerLookup, mapping, "companyNumber"),
                companyName,
                GetMappedValue(row, headerLookup, mapping, "vatNumber"),
                GetMappedValue(row, headerLookup, mapping, "addressLine1"),
                GetMappedValue(row, headerLookup, mapping, "addressLine2"),
                GetMappedValue(row, headerLookup, mapping, "townOrCity"),
                GetMappedValue(row, headerLookup, mapping, "county"),
                GetMappedValue(row, headerLookup, mapping, "postcode"),
                GetMappedValue(row, headerLookup, mapping, "country"),
                GetMappedValue(row, headerLookup, mapping, "status"),
                GetMappedValue(row, headerLookup, mapping, "category"),
                GetMappedValue(row, headerLookup, mapping, "sicCodes"),
                GetMappedValue(row, headerLookup, mapping, "website"),
                GetMappedValue(row, headerLookup, mapping, "contactName"),
                GetMappedValue(row, headerLookup, mapping, "contactEmail"),
                GetMappedValue(row, headerLookup, mapping, "contactPhone")
            ];

            await writer.WriteLineAsync(string.Join(",", canonicalRow.Select(EscapeCsv)));
        }

        return canonicalFilePath;
    }

    async ValueTask<int> MergeCanonicalFileAsync(Guid importId, string canonicalFilePath, CancellationToken cancellationToken)
    {
        int importedRows = 0;
        using TextFieldParser parser = CreateCsvParser(canonicalFilePath);
        string[] headers = parser.ReadFields() ?? [];
        Dictionary<string, int> headerLookup = BuildHeaderLookup(headers);

        Import import = await imports.RetrieveAllImports().Include(item => item.Source).FirstAsync(item => item.Id == importId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int pendingSaves = 0;

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] row = parser.ReadFields() ?? [];

            string companyNumber = Normalize(GetValue(row, headerLookup, "CompanyNumber"));
            string vatNumber = Normalize(GetValue(row, headerLookup, "VatNumber"));
            string companyName = Normalize(GetValue(row, headerLookup, "Name"));
            if (string.IsNullOrWhiteSpace(companyName))
                continue;

            string sourceRowNumber = Normalize(GetValue(row, headerLookup, "SourceRowNumber"));
            Company company = await FindCompanyAsync(import.Source, companyNumber, vatNumber, cancellationToken);
            if (company is null)
            {
                Address address = HasAddressData(row, headerLookup)
                    ? CreateAddress(import, companyNumber ?? vatNumber, row, headerLookup, now)
                    : null;
                if (address is not null)
                    sales.Add(address);

                company = CreateCompany(import, address?.Id, row, headerLookup, now);
                sales.Add(company);
            }
            else
            {
                UpdateCompany(company, import, row, headerLookup, now);
                Address address = company.RegisteredAddressId.HasValue
                    ? await sales.RetrieveAddresses().FirstOrDefaultAsync(item => item.Id == company.RegisteredAddressId.Value, cancellationToken)
                    : null;

                if (address is null && HasAddressData(row, headerLookup))
                {
                    address = CreateAddress(import, companyNumber ?? vatNumber, row, headerLookup, now);
                    sales.Add(address);
                    company.RegisteredAddressId = address.Id;
                }
                else if (address is not null && (!address.IsVerified || import.Source.IsAuthoritative))
                {
                    UpdateAddress(address, import, row, headerLookup, now);
                }
            }

            Lead lead = await sales.RetrieveLeads().FirstOrDefaultAsync(item =>
                item.SourceId == import.SourceId
                && item.SourceRecordId == (companyNumber ?? vatNumber ?? sourceRowNumber),
                cancellationToken);

            if (lead is null)
            {
                lead = CreateLead(import, company.Id, row, headerLookup, now);
                sales.Add(lead);
            }

            CompanyContact contact = await UpsertContactAsync(import, company.Id, row, headerLookup, now, cancellationToken);
            CreateImportLink(import, company.Id, lead.Id, contact?.Id, sourceRowNumber, now);

            importedRows++;
            pendingSaves++;
            if (pendingSaves >= options.Value.ProcessingBatchSize)
            {
                await sales.SaveAsync(cancellationToken);
                await imports.SaveAsync(cancellationToken);
                pendingSaves = 0;
            }
        }

        if (pendingSaves > 0)
        {
            await sales.SaveAsync(cancellationToken);
            await imports.SaveAsync(cancellationToken);
        }

        return importedRows;
    }

    async ValueTask<Company> FindCompanyAsync(
        Source source,
        string companyNumber,
        string vatNumber,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(companyNumber))
        {
            Company exact = await sales.RetrieveCompanies().FirstOrDefaultAsync(item =>
                item.SourceSystem == source.Name
                && item.SourceRecordId == companyNumber,
                cancellationToken);

            if (exact is not null)
                return exact;
        }

        if (source.IsAuthoritative || !string.IsNullOrWhiteSpace(vatNumber))
        {
            return await sales.RetrieveCompanies().FirstOrDefaultAsync(item =>
                item.VatNumber == vatNumber
                || (!string.IsNullOrWhiteSpace(companyNumber) && item.CompanyNumber == companyNumber),
                cancellationToken);
        }

        return null;
    }

    static Address CreateAddress(Import import, string legacyId, string[] row, IReadOnlyDictionary<string, int> headers, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceSystem = import.Source.Name,
            LegacyId = legacyId,
            IsVerified = import.Source.IsAuthoritative,
            Line1 = Normalize(GetValue(row, headers, "AddressLine1")),
            Line2 = Normalize(GetValue(row, headers, "AddressLine2")),
            TownOrCity = Normalize(GetValue(row, headers, "TownOrCity")),
            StateOrProvince = Normalize(GetValue(row, headers, "County")),
            ZipOrPostalCode = Normalize(GetValue(row, headers, "Postcode")),
            CountryId = Normalize(GetValue(row, headers, "Country")) ?? import.Source.CountryCode,
            VerificationNotes = import.Source.IsAuthoritative ? $"Imported from authoritative source {import.Source.Name}" : null,
            CreatedBy = "hosted-services",
            LastUpdatedBy = "hosted-services",
            CreatedOn = now,
            LastUpdated = now
        };

    static void UpdateAddress(Address address, Import import, string[] row, IReadOnlyDictionary<string, int> headers, DateTimeOffset now)
    {
        AssignIfProvided(value => address.Line1 = value, GetValue(row, headers, "AddressLine1"));
        AssignIfProvided(value => address.Line2 = value, GetValue(row, headers, "AddressLine2"));
        AssignIfProvided(value => address.TownOrCity = value, GetValue(row, headers, "TownOrCity"));
        AssignIfProvided(value => address.StateOrProvince = value, GetValue(row, headers, "County"));
        AssignIfProvided(value => address.ZipOrPostalCode = value, GetValue(row, headers, "Postcode"));
        AssignIfProvided(value => address.CountryId = value, GetValue(row, headers, "Country"));
        address.IsVerified = address.IsVerified || import.Source.IsAuthoritative;
        address.LastUpdatedBy = "hosted-services";
        address.LastUpdated = now;
    }

    static Company CreateCompany(Import import, Guid? addressId, string[] row, IReadOnlyDictionary<string, int> headers, DateTimeOffset now)
    {
        string companyNumber = Normalize(GetValue(row, headers, "CompanyNumber"));
        string vatNumber = Normalize(GetValue(row, headers, "VatNumber"));
        int rankingScore = CalculateRankingScore(row, headers);

        return new Company
        {
            Id = Guid.NewGuid(),
            SourceSystem = import.Source.Name,
            SourceRecordId = companyNumber ?? vatNumber,
            IsVerified = import.Source.IsAuthoritative,
            OfficialName = Normalize(GetValue(row, headers, "Name")) ?? "Imported company",
            LegalEntityName = Normalize(GetValue(row, headers, "Name")),
            TradingName = Normalize(GetValue(row, headers, "Name")),
            CompanyNumber = companyNumber,
            VatNumber = vatNumber,
            CompanyCategory = Normalize(GetValue(row, headers, "Category")),
            CompanyStatus = Normalize(GetValue(row, headers, "Status")),
            PrimarySicCodes = Normalize(GetValue(row, headers, "SicCodes")),
            WebsiteUrl = Normalize(GetValue(row, headers, "Website")),
            ResearchSummary = "Awaiting automated research.",
            VerificationNotes = import.Source.IsAuthoritative ? $"Imported from authoritative source {import.Source.Name}" : null,
            RankingScore = rankingScore,
            RankingRationale = BuildRankingRationale(rankingScore, row, headers),
            RegisteredAddressId = addressId,
            CreatedBy = "hosted-services",
            LastUpdatedBy = "hosted-services",
            CreatedOn = now,
            LastUpdated = now
        };
    }

    static void UpdateCompany(Company company, Import import, string[] row, IReadOnlyDictionary<string, int> headers, DateTimeOffset now)
    {
        bool incomingAuthoritative = import.Source.IsAuthoritative;
        bool preserveExisting = company.IsVerified && !incomingAuthoritative;

        if (!preserveExisting)
        {
            AssignIfProvided(value => company.OfficialName = value, GetValue(row, headers, "Name"));
            AssignIfProvided(value => company.LegalEntityName = value, GetValue(row, headers, "Name"));
            AssignIfProvided(value => company.TradingName = value, GetValue(row, headers, "Name"));
            AssignIfProvided(value => company.CompanyNumber = value, GetValue(row, headers, "CompanyNumber"));
            AssignIfProvided(value => company.VatNumber = value, GetValue(row, headers, "VatNumber"));
            AssignIfProvided(value => company.CompanyStatus = value, GetValue(row, headers, "Status"));
            AssignIfProvided(value => company.CompanyCategory = value, GetValue(row, headers, "Category"));
            AssignIfProvided(value => company.PrimarySicCodes = value, GetValue(row, headers, "SicCodes"));
            AssignIfProvided(value => company.WebsiteUrl = value, GetValue(row, headers, "Website"));
            company.IsVerified = company.IsVerified || incomingAuthoritative;
        }
        else
        {
            company.WebsiteUrl ??= Normalize(GetValue(row, headers, "Website"));
        }

        int rankingScore = CalculateRankingScore(row, headers);
        if (!company.RankingScore.HasValue || rankingScore > company.RankingScore.Value)
        {
            company.RankingScore = rankingScore;
            company.RankingRationale = BuildRankingRationale(rankingScore, row, headers);
        }

        company.LastUpdated = now;
        company.LastUpdatedBy = "hosted-services";
    }

    static Lead CreateLead(Import import, Guid companyId, string[] row, IReadOnlyDictionary<string, int> headers, DateTimeOffset now)
    {
        string companyNumber = Normalize(GetValue(row, headers, "CompanyNumber"));
        string vatNumber = Normalize(GetValue(row, headers, "VatNumber"));
        int rankingScore = CalculateRankingScore(row, headers);

        return new Lead
        {
            Id = Guid.NewGuid(),
            SourceId = import.SourceId,
            SourceSystem = import.Source.Name,
            SourceRecordId = companyNumber ?? vatNumber ?? Normalize(GetValue(row, headers, "SourceRowNumber")),
            SourceFileName = import.OriginalFileName,
            TenantId = "default",
            Status = LeadStatus.Imported,
            RawCompanyName = Normalize(GetValue(row, headers, "Name")) ?? "Imported company",
            RawCompanyNumber = companyNumber,
            RawVatNumber = vatNumber,
            RawWebsiteUrl = Normalize(GetValue(row, headers, "Website")),
            RawContactEmailAddress = Normalize(GetValue(row, headers, "ContactEmail")),
            RawContactPhoneNumber = Normalize(GetValue(row, headers, "ContactPhone")),
            QualificationNotes = import.UserInstructions,
            RankingScore = rankingScore,
            RankingRationale = BuildRankingRationale(rankingScore, row, headers),
            CompanyId = companyId,
            CreatedBy = "hosted-services",
            LastUpdatedBy = "hosted-services",
            CreatedOn = now,
            LastUpdated = now
        };
    }

    async ValueTask<CompanyContact> UpsertContactAsync(
        Import import,
        Guid companyId,
        string[] row,
        IReadOnlyDictionary<string, int> headers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string email = Normalize(GetValue(row, headers, "ContactEmail"));
        string name = Normalize(GetValue(row, headers, "ContactName"));
        string phone = Normalize(GetValue(row, headers, "ContactPhone"));
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            return null;

        CompanyContact contact = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            contact = await sales.RetrieveCompanyContacts().FirstOrDefaultAsync(item =>
                item.CompanyId == companyId && item.EmailAddress == email,
                cancellationToken);
        }

        if (contact is null)
        {
            contact = new CompanyContact
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SourceSystem = import.Source.Name,
                IsVerified = import.Source.IsAuthoritative,
                IsPrimary = false,
                Name = name ?? email ?? "Imported contact",
                EmailAddress = email,
                PhoneNumber = phone,
                CreatedBy = "hosted-services",
                LastUpdatedBy = "hosted-services",
                CreatedOn = now,
                LastUpdated = now
            };

            sales.Add(contact);
        }

        return contact;
    }

    void CreateImportLink(
        Import import,
        Guid companyId,
        Guid leadId,
        Guid? companyContactId,
        string sourceRowNumber,
        DateTimeOffset now)
    {
        imports.Add(new ImportLink
        {
            Id = Guid.NewGuid(),
            ImportId = import.Id,
            SourceId = import.SourceId,
            CompanyId = companyId,
            LeadId = leadId,
            CompanyContactId = companyContactId,
            SourceRowKey = sourceRowNumber,
            SourceRowNumber = long.TryParse(sourceRowNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : null,
            CreatedBy = "hosted-services",
            LastUpdatedBy = "hosted-services",
            CreatedOn = now,
            LastUpdated = now
        });
    }

    void ArchiveImportFiles(Guid importId)
    {
        string incoming = GetImportDirectory("Incoming", importId);
        string archive = GetImportDirectory("Archive", importId);
        if (Directory.Exists(archive))
            Directory.Delete(archive, recursive: true);
        if (Directory.Exists(incoming))
            Directory.Move(incoming, archive);
    }

    void MoveToFailed(Guid importId)
    {
        string incoming = GetImportDirectory("Incoming", importId);
        string failed = GetImportDirectory("Failed", importId);
        if (Directory.Exists(failed))
            Directory.Delete(failed, recursive: true);
        if (Directory.Exists(incoming))
            Directory.Move(incoming, failed);
    }

    string GetImportDirectory(string phase, Guid importId) =>
        Path.Combine(GetWorkspaceRoot(), "Imports", phase, importId.ToString("N", CultureInfo.InvariantCulture));

    string GetWorkspaceRoot()
    {
        string configuredPath = options.Value.AgentWorkspacePath;
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }

    static TextFieldParser CreateCsvParser(string filePath)
    {
        TextFieldParser parser = new(filePath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        return parser;
    }

    static Dictionary<string, int> BuildHeaderLookup(string[] headers) =>
        headers
            .Select((header, index) => new { Header = header, Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

    static Dictionary<string, string> ReadMapping(string mappingJson)
    {
        if (string.IsNullOrWhiteSpace(mappingJson))
            return [];

        using JsonDocument document = JsonDocument.Parse(mappingJson);
        if (!document.RootElement.TryGetProperty("fields", out JsonElement fields))
            return [];

        return fields.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    static string GetMappedValue(
        string[] row,
        IReadOnlyDictionary<string, int> headers,
        IReadOnlyDictionary<string, string> mapping,
        string canonicalName) =>
        mapping.TryGetValue(canonicalName, out string sourceHeader)
            ? GetValue(row, headers, sourceHeader)
            : null;

    static string GetValue(string[] row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        return headers.TryGetValue(header, out int index) && index < row.Length
            ? Normalize(row[index])
            : null;
    }

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        return value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    static bool HasAddressData(string[] row, IReadOnlyDictionary<string, int> headers) =>
        new[]
        {
            "AddressLine1", "AddressLine2", "TownOrCity", "County", "Postcode", "Country"
        }.Any(column => !string.IsNullOrWhiteSpace(GetValue(row, headers, column)));

    static int CalculateRankingScore(string[] row, IReadOnlyDictionary<string, int> headers)
    {
        int score = 20;
        if (!string.IsNullOrWhiteSpace(GetValue(row, headers, "Website")))
            score += 10;
        if (!string.IsNullOrWhiteSpace(GetValue(row, headers, "ContactEmail")))
            score += 20;
        if (!string.IsNullOrWhiteSpace(GetValue(row, headers, "SicCodes")))
            score += 10;
        if (string.Equals(GetValue(row, headers, "Status"), "Active", StringComparison.OrdinalIgnoreCase))
            score += 20;
        if (!string.IsNullOrWhiteSpace(GetValue(row, headers, "VatNumber")))
            score += 10;
        return Math.Min(score, 100);
    }

    static string BuildRankingRationale(int score, string[] row, IReadOnlyDictionary<string, int> headers) =>
        $"Score {score}: status '{GetValue(row, headers, "Status") ?? "unknown"}', " +
        $"contact email '{GetValue(row, headers, "ContactEmail") ?? "missing"}', " +
        $"industry code '{GetValue(row, headers, "SicCodes") ?? "missing"}'.";

    static void AssignIfProvided(Action<string> assign, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            assign(value.Trim());
    }
}
