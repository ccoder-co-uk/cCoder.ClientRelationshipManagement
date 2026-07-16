using System.Data;
using System.Globalization;
using System.Text.Json;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class AuthorityDataImportService(
    IHostEnvironment environment,
    PlatformConfiguration platformConfiguration,
    IPlatformDbContextFactory dbContextFactory,
    IOptions<AuthorityDataOptions> options,
    IOptions<AgentWorkflowOptions> agentWorkflowOptions,
    ILoggingBroker<AuthorityDataImportService> loggingBroker)
    : IAuthorityDataImportService
{
    const string CompaniesHouseSource = "CompaniesHouse";
    const string ImportedFromAuthorityPrefix = "Imported from authority";
    static readonly CultureInfo UkCulture = CultureInfo.GetCultureInfo("en-GB");

    public async ValueTask<int> RunPendingImportsAsync(CancellationToken cancellationToken = default)
    {
        AuthorityDataOptions authorityDataOptions = options.Value;
        if (!authorityDataOptions.Enabled)
        {
            loggingBroker.LogInformation("Authority data import skipped because the feature is disabled.");
            return 0;
        }

        string dropPath = ResolvePath(authorityDataOptions.DropPath);
        string archivePath = ResolvePath(authorityDataOptions.ArchivePath);
        string failedPath = ResolvePath(authorityDataOptions.FailedPath);

        Directory.CreateDirectory(dropPath);
        Directory.CreateDirectory(archivePath);
        Directory.CreateDirectory(failedPath);
        await EnsureStagingTableAsync(cancellationToken);

        FileInfo[] files = new DirectoryInfo(dropPath)
            .GetFiles("*.csv", System.IO.SearchOption.TopDirectoryOnly)
            .OrderBy(file => file.CreationTimeUtc)
            .ToArray();

        int importedFileCount = await ResumePendingStagedBatchesAsync(
            authorityDataOptions,
            dropPath,
            archivePath,
            failedPath,
            cancellationToken);

        if (files.Length == 0)
        {
            loggingBroker.LogInformation("Authority data import found no pending file(s) in {DropPath}.", dropPath);
            return importedFileCount;
        }

        foreach (FileInfo file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                loggingBroker.LogInformation(
                    "Authority data import started for {FileName} ({FileSizeMb:N2} MB).",
                    file.Name,
                    file.Length / 1024d / 1024d);

                await ImportCompaniesHouseCsvAsync(file.FullName, authorityDataOptions, cancellationToken);
                MoveFile(file.FullName, archivePath);
                importedFileCount++;

                loggingBroker.LogInformation(
                    "Authority data import completed for {FileName}.",
                    file.Name);
            }
            catch (Exception exception)
            {
                loggingBroker.LogError(exception, "Authority data import failed for {FileName}.", file.Name);
                MoveFile(file.FullName, failedPath);
            }
        }

        return importedFileCount;
    }

    async ValueTask ImportCompaniesHouseCsvAsync(
        string filePath,
        AuthorityDataOptions authorityDataOptions,
        CancellationToken cancellationToken)
    {
        await EnsureStagingTableAsync(cancellationToken);

        string sourceFileName = Path.GetFileName(filePath);
        StagedBatchInfo existingBatch = await GetStagedBatchBySourceFileNameAsync(sourceFileName, cancellationToken);
        if (existingBatch is not null)
        {
            loggingBroker.LogInformation(
                "Authority data import detected a resumable staged batch {BatchId} for {FileName} with {PendingRowCount} pending row(s).",
                existingBatch.BatchId,
                existingBatch.SourceFileName,
                existingBatch.PendingRowCount);

            await ResumeStagedBatchAsync(existingBatch, authorityDataOptions, cancellationToken);
            return;
        }

        Guid batchId = Guid.NewGuid();
        await BulkStageCompaniesHouseFileAsync(filePath, batchId, authorityDataOptions.BatchSize, cancellationToken);

        await ResumeStagedBatchAsync(
            new StagedBatchInfo
            {
                BatchId = batchId,
                SourceFileName = sourceFileName,
                PendingRowCount = 0
            },
            authorityDataOptions,
            cancellationToken);
    }

    async ValueTask<int> ResumePendingStagedBatchesAsync(
        AuthorityDataOptions authorityDataOptions,
        string dropPath,
        string archivePath,
        string failedPath,
        CancellationToken cancellationToken)
    {
        StagedBatchInfo[] stagedBatches = await GetPendingStagedBatchesAsync(cancellationToken);
        if (stagedBatches.Length == 0)
            return 0;

        int resumedBatchCount = 0;

        foreach (StagedBatchInfo stagedBatch in stagedBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            loggingBroker.LogInformation(
                "Authority data import resuming staged batch {BatchId} for {FileName} with {PendingRowCount} pending row(s).",
                stagedBatch.BatchId,
                stagedBatch.SourceFileName,
                stagedBatch.PendingRowCount);

            await ResumeStagedBatchAsync(stagedBatch, authorityDataOptions, cancellationToken);
            MoveSourceFileIfPresent(stagedBatch.SourceFileName, archivePath, dropPath, failedPath);
            resumedBatchCount++;
        }

        return resumedBatchCount;
    }

    async ValueTask ResumeStagedBatchAsync(
        StagedBatchInfo stagedBatch,
        AuthorityDataOptions authorityDataOptions,
        CancellationToken cancellationToken)
    {
        string executionUserId = ResolveExecutionUserId();
        LegacyImportProvenance provenance = await EnsureLegacyImportProvenanceAsync(
            stagedBatch,
            authorityDataOptions,
            executionUserId,
            cancellationToken);

        bool completed = await MergeStagedBatchAsync(
            stagedBatch.BatchId,
            stagedBatch.SourceFileName,
            authorityDataOptions,
            provenance,
            executionUserId,
            cancellationToken);

        if (completed)
        {
            await DeleteStagedBatchAsync(stagedBatch.BatchId, cancellationToken);
            await MarkLegacyImportCompletedAsync(provenance.ImportId, executionUserId, cancellationToken);
        }
    }

    async ValueTask<LegacyImportProvenance> EnsureLegacyImportProvenanceAsync(
        StagedBatchInfo stagedBatch,
        AuthorityDataOptions authorityDataOptions,
        string executionUserId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        Source source = await context.Sources.FirstOrDefaultAsync(
            item => item.Name == authorityDataOptions.SourceSystem
                && item.CountryCode == authorityDataOptions.SourceCountryCode,
            cancellationToken);

        if (source is null)
        {
            source = new Source
            {
                Id = Guid.NewGuid(),
                Name = authorityDataOptions.SourceSystem,
                SourceType = SourceType.Authority,
                CountryCode = authorityDataOptions.SourceCountryCode,
                IsAuthoritative = true,
                Notes = authorityDataOptions.SourceNotes,
                CreatedBy = executionUserId,
                LastUpdatedBy = executionUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            context.Sources.Add(source);
        }

        string storedObjectKey = $"legacy-staging:{stagedBatch.BatchId}";
        Import import = await context.Imports.FirstOrDefaultAsync(
            item => item.StoredObjectKey == storedObjectKey,
            cancellationToken);

        if (import is null)
        {
            import = new Import
            {
                Id = stagedBatch.BatchId,
                SourceId = source.Id,
                OriginalFileName = stagedBatch.SourceFileName,
                ContentType = "text/csv",
                SizeBytes = 0,
                StoredFilePath = $"crm.CompaniesHouseImportStaging/{stagedBatch.BatchId}",
                StoredObjectKey = storedObjectKey,
                JobStatus = ImportJobStatus.Processing,
                UploadStatus = ImportUploadStatus.Uploaded,
                ProcessingStatus = ImportProcessingStatus.Merging,
                UploadedBytes = 0,
                TotalRowCount = stagedBatch.PendingRowCount > int.MaxValue ? int.MaxValue : (int)stagedBatch.PendingRowCount,
                ImportedRowCount = 0,
                MappingSnapshotJson = "{\"legacy\":\"CompaniesHouseImportStaging\"}",
                UserInstructions = "Legacy Companies House staging import resumed under import-management provenance.",
                ProcessingCheckpoint = "legacy-staging-resume",
                UploadedOn = now,
                MarkedReadyOn = now,
                ProcessingStartedOn = now,
                CreatedBy = executionUserId,
                LastUpdatedBy = executionUserId,
                CreatedOn = now,
                LastUpdated = now
            };

            context.Imports.Add(import);
        }
        else
        {
            import.SourceId = source.Id;
            import.JobStatus = ImportJobStatus.Processing;
            import.UploadStatus = ImportUploadStatus.Uploaded;
            import.ProcessingStatus = ImportProcessingStatus.Merging;
            import.ProcessingCheckpoint = "legacy-staging-resume";
            import.ProcessingStartedOn ??= now;
            import.LastUpdatedBy = executionUserId;
            import.LastUpdated = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await BackfillLegacyImportProvenanceAsync(
            source.Id,
            import.Id,
            stagedBatch.SourceFileName,
            authorityDataOptions,
            executionUserId,
            cancellationToken);

        return new LegacyImportProvenance
        {
            SourceId = source.Id,
            ImportId = import.Id
        };
    }

    async ValueTask<StagedBatchInfo[]> GetPendingStagedBatchesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                stage.BatchId,
                stage.SourceFileName,
                COUNT_BIG(*) AS PendingRowCount
            FROM crm.CompaniesHouseImportStaging stage
            GROUP BY stage.BatchId, stage.SourceFileName
            ORDER BY stage.BatchId;
            """;

        List<StagedBatchInfo> stagedBatches = [];

        using SqlConnection connection = new(GetAdminConnectionString());
        await connection.OpenAsync(cancellationToken);

        using SqlCommand command = new(sql, connection)
        {
            CommandTimeout = 0
        };

        using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            stagedBatches.Add(new StagedBatchInfo
            {
                BatchId = reader.GetGuid(0),
                SourceFileName = reader.GetString(1),
                PendingRowCount = reader.GetInt64(2)
            });
        }

        return [.. stagedBatches];
    }

    async ValueTask<StagedBatchInfo> GetStagedBatchBySourceFileNameAsync(
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                stage.BatchId,
                stage.SourceFileName,
                COUNT_BIG(*) AS PendingRowCount
            FROM crm.CompaniesHouseImportStaging stage
            WHERE stage.SourceFileName = @SourceFileName
            GROUP BY stage.BatchId, stage.SourceFileName
            ORDER BY stage.BatchId;
            """;

        using SqlConnection connection = new(GetAdminConnectionString());
        await connection.OpenAsync(cancellationToken);

        using SqlCommand command = new(sql, connection)
        {
            CommandTimeout = 0
        };

        command.Parameters.Add(new SqlParameter("@SourceFileName", SqlDbType.NVarChar, 256) { Value = sourceFileName });

        using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new StagedBatchInfo
        {
            BatchId = reader.GetGuid(0),
            SourceFileName = reader.GetString(1),
            PendingRowCount = reader.GetInt64(2)
        };
    }

    async ValueTask EnsureStagingTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'crm.CompaniesHouseImportStaging', N'U') IS NULL
            BEGIN
                CREATE TABLE crm.CompaniesHouseImportStaging
                (
                    StagingRowId BIGINT IDENTITY(1,1) NOT NULL,
                    BatchId UNIQUEIDENTIFIER NOT NULL,
                    SourceFileName NVARCHAR(256) NOT NULL,
                    CompanyName NVARCHAR(512) NULL,
                    CompanyNumber NVARCHAR(64) NULL,
                    RegCareOf NVARCHAR(256) NULL,
                    RegPoBox NVARCHAR(64) NULL,
                    RegAddressLine1 NVARCHAR(256) NULL,
                    RegAddressLine2 NVARCHAR(256) NULL,
                    RegPostTown NVARCHAR(256) NULL,
                    RegCounty NVARCHAR(256) NULL,
                    RegCountry NVARCHAR(256) NULL,
                    RegPostCode NVARCHAR(64) NULL,
                    CompanyCategory NVARCHAR(256) NULL,
                    CompanyStatus NVARCHAR(256) NULL,
                    CountryOfOrigin NVARCHAR(256) NULL,
                    DissolutionDate DATE NULL,
                    IncorporationDate DATE NULL,
                    AccountsCategory NVARCHAR(128) NULL,
                    AccountsLastMadeUpDate DATE NULL,
                    NumMortOutstanding INT NULL,
                    SicCodes NVARCHAR(2048) NULL,
                    RegistryUri NVARCHAR(512) NULL,
                    PreviousNamesJson NVARCHAR(MAX) NULL,
                    AuthorityRecordHash CHAR(64) NULL
                );

                CREATE INDEX IX_CompaniesHouseImportStaging_BatchId
                    ON crm.CompaniesHouseImportStaging(BatchId);
            END;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'StagingRowId') IS NULL
            BEGIN
                ALTER TABLE crm.CompaniesHouseImportStaging
                    ADD StagingRowId BIGINT IDENTITY(1,1) NOT NULL;
            END;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'AccountsCategory') IS NULL
                ALTER TABLE crm.CompaniesHouseImportStaging ADD AccountsCategory NVARCHAR(128) NULL;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'AccountsLastMadeUpDate') IS NULL
                ALTER TABLE crm.CompaniesHouseImportStaging ADD AccountsLastMadeUpDate DATE NULL;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'NumMortOutstanding') IS NULL
                ALTER TABLE crm.CompaniesHouseImportStaging ADD NumMortOutstanding INT NULL;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'AuthorityRecordHash') IS NULL
                ALTER TABLE crm.CompaniesHouseImportStaging ADD AuthorityRecordHash CHAR(64) NULL;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'crm.CompaniesHouseImportStaging')
                    AND name = N'IX_CompaniesHouseImportStaging_Batch_StagingRowId'
            )
            BEGIN
                CREATE INDEX IX_CompaniesHouseImportStaging_Batch_StagingRowId
                    ON crm.CompaniesHouseImportStaging(BatchId, StagingRowId)
                    INCLUDE (SourceFileName, CompanyNumber, CompanyName);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'leads.Leads')
                    AND name = N'IX_Leads_SourceLookup'
            )
            BEGIN
                CREATE INDEX IX_Leads_SourceLookup
                    ON leads.Leads(TenantId, SourceSystem, SourceRecordId)
                    INCLUDE (SourceFileName, SourceId, CompanyId);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'leads.Leads')
                    AND name = N'IX_Leads_AgentSelection'
            )
            BEGIN
                CREATE INDEX IX_Leads_AgentSelection
                    ON leads.Leads(SourceSystem, RankingScore DESC, Status)
                    INCLUDE (Id, CompanyId, CreatedOn);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'leads.Leads')
                    AND name = N'IX_Leads_AgentCohort'
            )
            BEGIN
                CREATE INDEX IX_Leads_AgentCohort
                    ON leads.Leads(SourceSystem, RankingScore DESC, Id);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'process.ProcessTasks')
                    AND name = N'IX_ProcessTasks_AgentQueue'
            )
            BEGIN
                CREATE INDEX IX_ProcessTasks_AgentQueue
                    ON process.ProcessTasks(State, LeadId, DueOn)
                    INCLUDE
                    (
                        ActionType,
                        OpportunityId,
                        ClientAccountId,
                        TenantCompanyRelationshipId,
                        EmailId,
                        ProcessStepId,
                        RenderedTitle
                    );
            END;

            UPDATE lead
            SET
                lead.RankingScore =
                    CASE WHEN LOWER(ISNULL(company.CompanyStatus, '')) = 'active' THEN 40 ELSE 0 END
                    + CASE
                        WHEN company.IncorporatedOn <= DATEADD(YEAR, -10, SYSUTCDATETIME()) THEN 15
                        WHEN company.IncorporatedOn <= DATEADD(YEAR, -5, SYSUTCDATETIME()) THEN 10
                        WHEN company.IncorporatedOn <= DATEADD(YEAR, -2, SYSUTCDATETIME()) THEN 5
                        ELSE 0
                      END
                    + CASE
                        WHEN ISNULL(company.PrimarySicCodes, '') LIKE '%99999%'
                            OR LOWER(ISNULL(company.PrimarySicCodes, '')) LIKE '%dormant%'
                            OR LOWER(ISNULL(company.PrimarySicCodes, '')) LIKE '%non-trading%'
                        THEN -100 ELSE 0
                      END,
                lead.RankingRationale = 'Companies House fast gate: active status, trading SIC and company age; detailed accounts signals will replace this score on the next authority import.',
                lead.LastUpdatedBy = @ExecutionUserId,
                lead.LastUpdated = SYSUTCDATETIME()
            FROM leads.Leads lead
            INNER JOIN masterdata.Companies company ON company.Id = lead.CompanyId
            WHERE lead.SourceSystem = @SourceSystem
                AND lead.RankingScore IS NULL;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'masterdata.Addresses')
                    AND name = N'IX_Addresses_Source_Legacy'
            )
            BEGIN
                CREATE INDEX IX_Addresses_Source_Legacy
                    ON masterdata.Addresses(SourceSystem, LegacyId);
            END;
            """;

        await ExecuteNonQueryAsync(
            sql,
            [
                new SqlParameter("@SourceSystem", SqlDbType.NVarChar, 128)
                {
                    Value = options.Value.SourceSystem
                },
                new SqlParameter("@ExecutionUserId", SqlDbType.NVarChar, 256)
                {
                    Value = ResolveExecutionUserId()
                }
            ],
            cancellationToken);
    }

    async ValueTask BulkStageCompaniesHouseFileAsync(
        string filePath,
        Guid batchId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        using SqlConnection connection = new(GetAdminConnectionString());
        await connection.OpenAsync(cancellationToken);

        using SqlBulkCopy bulkCopy = new(connection)
        {
            DestinationTableName = "crm.CompaniesHouseImportStaging",
            BatchSize = Math.Max(1, batchSize),
            BulkCopyTimeout = 0
        };

        foreach (DataColumn column in CreateStageTable().Columns)
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);

        using TextFieldParser parser = new(filePath);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;

        string[] headers = parser.ReadFields() ?? [];
        Dictionary<string, int> headerLookup = headers
            .Select((value, index) => new { Header = NormalizeHeader(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        DataTable dataTable = CreateStageTable();
        int stagedRowCount = 0;

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] row = parser.ReadFields() ?? [];
            if (row.Length == 0)
                continue;

            string companyNumber = GetValue(row, headerLookup, "companynumber");
            string companyName = GetValue(row, headerLookup, "companyname");

            if (string.IsNullOrWhiteSpace(companyNumber) || string.IsNullOrWhiteSpace(companyName))
                continue;

            DataRow dataRow = dataTable.NewRow();
            dataRow["BatchId"] = batchId;
            dataRow["SourceFileName"] = Path.GetFileName(filePath);
            dataRow["CompanyName"] = companyName;
            dataRow["CompanyNumber"] = companyNumber;
            dataRow["RegCareOf"] = ToDbValue(GetValue(row, headerLookup, "regaddress.careof"));
            dataRow["RegPoBox"] = ToDbValue(GetValue(row, headerLookup, "regaddress.pobox"));
            dataRow["RegAddressLine1"] = ToDbValue(GetValue(row, headerLookup, "regaddress.addressline1"));
            dataRow["RegAddressLine2"] = ToDbValue(GetValue(row, headerLookup, "regaddress.addressline2"));
            dataRow["RegPostTown"] = ToDbValue(GetValue(row, headerLookup, "regaddress.posttown"));
            dataRow["RegCounty"] = ToDbValue(GetValue(row, headerLookup, "regaddress.county"));
            dataRow["RegCountry"] = ToDbValue(GetValue(row, headerLookup, "regaddress.country"));
            dataRow["RegPostCode"] = ToDbValue(GetValue(row, headerLookup, "regaddress.postcode"));
            dataRow["CompanyCategory"] = ToDbValue(GetValue(row, headerLookup, "companycategory"));
            dataRow["CompanyStatus"] = ToDbValue(GetValue(row, headerLookup, "companystatus"));
            dataRow["CountryOfOrigin"] = ToDbValue(GetValue(row, headerLookup, "countryoforigin"));
            dataRow["DissolutionDate"] = ToDbValue(ParseNullableDate(GetValue(row, headerLookup, "dissolutiondate")));
            dataRow["IncorporationDate"] = ToDbValue(ParseNullableDate(GetValue(row, headerLookup, "incorporationdate")));
            dataRow["AccountsCategory"] = ToDbValue(GetValue(row, headerLookup, "accounts.accountscategory"));
            dataRow["AccountsLastMadeUpDate"] = ToDbValue(ParseNullableDate(GetValue(row, headerLookup, "accounts.lastmadeupdate")));
            dataRow["NumMortOutstanding"] = ToDbValue(ParseNullableInt(GetValue(row, headerLookup, "mortgages.nummortoutstanding")));
            dataRow["SicCodes"] = ToDbValue(string.Join(" | ", new[]
            {
                GetValue(row, headerLookup, "siccode.sictext1"),
                GetValue(row, headerLookup, "siccode.sictext2"),
                GetValue(row, headerLookup, "siccode.sictext3"),
                GetValue(row, headerLookup, "siccode.sictext4")
            }.Where(value => !string.IsNullOrWhiteSpace(value))));
            dataRow["RegistryUri"] = ToDbValue(GetValue(row, headerLookup, "uri"));
            dataRow["PreviousNamesJson"] = ToDbValue(BuildPreviousNamesJson(row, headerLookup));
            dataRow["AuthorityRecordHash"] = AuthorityRecordFingerprint.ComputeHex(
                dataRow["CompanyName"],
                dataRow["CompanyNumber"],
                dataRow["RegCareOf"],
                dataRow["RegPoBox"],
                dataRow["RegAddressLine1"],
                dataRow["RegAddressLine2"],
                dataRow["RegPostTown"],
                dataRow["RegCounty"],
                dataRow["RegCountry"],
                dataRow["RegPostCode"],
                dataRow["CompanyCategory"],
                dataRow["CompanyStatus"],
                dataRow["CountryOfOrigin"],
                dataRow["DissolutionDate"],
                dataRow["IncorporationDate"],
                dataRow["AccountsCategory"],
                dataRow["AccountsLastMadeUpDate"],
                dataRow["NumMortOutstanding"],
                dataRow["SicCodes"],
                dataRow["RegistryUri"],
                dataRow["PreviousNamesJson"]);
            dataTable.Rows.Add(dataRow);
            stagedRowCount++;

            if (dataTable.Rows.Count >= Math.Max(1, batchSize))
            {
                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                loggingBroker.LogInformation(
                    "Authority data import staged {StagedRowCount} company row(s) from {FileName}.",
                    stagedRowCount,
                    Path.GetFileName(filePath));
                dataTable.Clear();
            }
        }

        if (dataTable.Rows.Count > 0)
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

        loggingBroker.LogInformation(
            "Authority data import finished staging {StagedRowCount} company row(s) from {FileName}.",
            stagedRowCount,
            Path.GetFileName(filePath));
    }

    async ValueTask<bool> MergeStagedBatchAsync(
        Guid batchId,
        string sourceFileName,
        AuthorityDataOptions authorityDataOptions,
        LegacyImportProvenance provenance,
        string executionUserId,
        CancellationToken cancellationToken)
    {
        int mergeBatchSize = Math.Max(1, authorityDataOptions.MergeBatchSize);
        int maxMergeChunksPerRun = Math.Max(0, authorityDataOptions.MaxMergeChunksPerRun);
        DateTimeOffset runDeadline = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, authorityDataOptions.MaxRunMinutes));
        int mergedRowCount = 0;
        int mergedChunkCount = 0;

        while (true)
        {
            int pendingRowCount = await GetPendingMergeRowCountAsync(batchId, mergeBatchSize, cancellationToken);
            if (pendingRowCount == 0)
                return true;

            if (mergedChunkCount > 0
                && (DateTimeOffset.UtcNow >= runDeadline
                    || (maxMergeChunksPerRun > 0 && mergedChunkCount >= maxMergeChunksPerRun)))
            {
                long remainingRows = await GetPendingStagedRowCountAsync(batchId, cancellationToken);
                loggingBroker.LogInformation(
                    "Authority data import yielding staged batch {BatchId} for {FileName} after {MergedChunkCount} chunk(s). {RemainingRows} staged row(s) remain for a future tick.",
                    batchId,
                    sourceFileName,
                    mergedChunkCount,
                    remainingRows);

                return false;
            }

            await MergeNextStagedChunkAsync(
                batchId,
                sourceFileName,
                authorityDataOptions,
                provenance,
                executionUserId,
                mergeBatchSize,
                cancellationToken);

            mergedRowCount += pendingRowCount;
            mergedChunkCount++;

            loggingBroker.LogInformation(
                "Authority data import merged {MergedRowCount} staged company row(s) for {FileName}.",
                mergedRowCount,
                sourceFileName);
        }
    }

    async ValueTask<long> GetPendingStagedRowCountAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT_BIG(*)
            FROM crm.CompaniesHouseImportStaging stage
            WHERE stage.BatchId = @BatchId;
            """;

        object value = await ExecuteScalarAsync(
            sql,
            [new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId }],
            cancellationToken);

        return value is null || value == DBNull.Value
            ? 0
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    async ValueTask<int> GetPendingMergeRowCountAsync(
        Guid batchId,
        int mergeBatchSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM
            (
                SELECT TOP (@MergeBatchSize) 1 AS PendingMarker
                FROM crm.CompaniesHouseImportStaging stage
                WHERE stage.BatchId = @BatchId
                ORDER BY stage.StagingRowId
            ) pending;
            """;

        object value = await ExecuteScalarAsync(
            sql,
            [
                new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId },
                new SqlParameter("@MergeBatchSize", SqlDbType.Int) { Value = mergeBatchSize }
            ],
            cancellationToken);

        return value is null || value == DBNull.Value
            ? 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    async ValueTask MergeNextStagedChunkAsync(
        Guid batchId,
        string sourceFileName,
        AuthorityDataOptions authorityDataOptions,
        LegacyImportProvenance provenance,
        string executionUserId,
        int mergeBatchSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();
            DECLARE @ProcessedRowCount INT;

            SELECT TOP (@MergeBatchSize)
                stage.StagingRowId,
                stage.CompanyName,
                stage.CompanyNumber,
                stage.RegCareOf,
                stage.RegPoBox,
                stage.RegAddressLine1,
                stage.RegAddressLine2,
                stage.RegPostTown,
                stage.RegCounty,
                stage.RegCountry,
                stage.RegPostCode,
                stage.CompanyCategory,
                stage.CompanyStatus,
                stage.CountryOfOrigin,
                stage.DissolutionDate,
                stage.IncorporationDate,
                stage.AccountsCategory,
                stage.AccountsLastMadeUpDate,
                stage.NumMortOutstanding,
                stage.SicCodes,
                stage.RegistryUri,
                stage.PreviousNamesJson,
                stage.AuthorityRecordHash
            INTO #BatchSlice
            FROM crm.CompaniesHouseImportStaging stage
            WHERE stage.BatchId = @BatchId
            ORDER BY stage.StagingRowId;

            SET @ProcessedRowCount = @@ROWCOUNT;

            CREATE UNIQUE CLUSTERED INDEX IX_BatchSlice_StagingRowId
                ON #BatchSlice(StagingRowId);

            ;WITH NormalizedRecords AS
            (
                SELECT
                    stage.*,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY NULLIF(LTRIM(RTRIM(stage.CompanyNumber)), '')
                        ORDER BY stage.StagingRowId DESC
                    ) AS AuthorityRecordSequence
                FROM #BatchSlice stage
                WHERE NULLIF(LTRIM(RTRIM(stage.CompanyNumber)), '') IS NOT NULL
            )
            SELECT
                NULLIF(LTRIM(RTRIM(record.CompanyNumber)), '') AS SourceRecordId,
                record.CompanyName,
                record.CompanyCategory,
                record.CompanyStatus,
                record.CountryOfOrigin,
                record.DissolutionDate,
                record.IncorporationDate,
                record.SicCodes,
                record.RegistryUri,
                record.PreviousNamesJson,
                NULLIF(LTRIM(RTRIM(record.RegPoBox)), '') AS RegPoBox,
                NULLIF(LTRIM(RTRIM(record.RegAddressLine1)), '') AS RegAddressLine1,
                NULLIF(LTRIM(RTRIM(CONCAT_WS(', ',
                    NULLIF(LTRIM(RTRIM(record.RegCareOf)), ''),
                    NULLIF(LTRIM(RTRIM(record.RegAddressLine2)), '')))), '') AS RegAddressLine2,
                NULLIF(LTRIM(RTRIM(record.RegPostTown)), '') AS RegPostTown,
                NULLIF(LTRIM(RTRIM(record.RegCounty)), '') AS RegCounty,
                NULLIF(LTRIM(RTRIM(record.RegPostCode)), '') AS RegPostCode,
                NULLIF(LTRIM(RTRIM(record.RegCountry)), '') AS RegCountry,
                COALESCE(record.AuthorityRecordHash, CONVERT(CHAR(64), HASHBYTES('SHA2_256', CONCAT(
                    ISNULL(record.CompanyName, ''), NCHAR(31),
                    ISNULL(record.CompanyNumber, ''), NCHAR(31),
                    ISNULL(record.RegCareOf, ''), NCHAR(31),
                    ISNULL(record.RegPoBox, ''), NCHAR(31),
                    ISNULL(record.RegAddressLine1, ''), NCHAR(31),
                    ISNULL(record.RegAddressLine2, ''), NCHAR(31),
                    ISNULL(record.RegPostTown, ''), NCHAR(31),
                    ISNULL(record.RegCounty, ''), NCHAR(31),
                    ISNULL(record.RegCountry, ''), NCHAR(31),
                    ISNULL(record.RegPostCode, ''), NCHAR(31),
                    ISNULL(record.CompanyCategory, ''), NCHAR(31),
                    ISNULL(record.CompanyStatus, ''), NCHAR(31),
                    ISNULL(record.CountryOfOrigin, ''), NCHAR(31),
                    ISNULL(CONVERT(NVARCHAR(10), record.DissolutionDate, 23), ''), NCHAR(31),
                    ISNULL(CONVERT(NVARCHAR(10), record.IncorporationDate, 23), ''), NCHAR(31),
                    ISNULL(record.AccountsCategory, ''), NCHAR(31),
                    ISNULL(CONVERT(NVARCHAR(10), record.AccountsLastMadeUpDate, 23), ''), NCHAR(31),
                    ISNULL(CONVERT(NVARCHAR(16), record.NumMortOutstanding), ''), NCHAR(31),
                    ISNULL(record.SicCodes, ''), NCHAR(31),
                    ISNULL(record.RegistryUri, ''), NCHAR(31),
                    ISNULL(record.PreviousNamesJson, '')
                )), 2)) AS AuthorityRecordHash,
                CASE WHEN LOWER(ISNULL(record.CompanyStatus, '')) = 'active' THEN 40 ELSE 0 END
                    + CASE
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%group%' THEN 35
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%full%' THEN 30
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%medium%' THEN 25
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%small%' THEN 15
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%micro%' THEN 5
                        WHEN LOWER(ISNULL(record.AccountsCategory, '')) LIKE '%dormant%' THEN -100
                        ELSE 0
                      END
                    + CASE
                        WHEN record.IncorporationDate <= DATEADD(YEAR, -10, @Now) THEN 15
                        WHEN record.IncorporationDate <= DATEADD(YEAR, -5, @Now) THEN 10
                        WHEN record.IncorporationDate <= DATEADD(YEAR, -2, @Now) THEN 5
                        ELSE 0
                      END
                    + CASE WHEN record.AccountsLastMadeUpDate >= DATEADD(MONTH, -18, @Now) THEN 10 ELSE 0 END
                    + CASE
                        WHEN record.NumMortOutstanding >= 5 THEN 10
                        WHEN record.NumMortOutstanding > 0 THEN record.NumMortOutstanding * 2
                        ELSE 0
                      END
                    + CASE
                        WHEN ISNULL(record.SicCodes, '') LIKE '%99999%'
                            OR LOWER(ISNULL(record.SicCodes, '')) LIKE '%dormant%'
                            OR LOWER(ISNULL(record.SicCodes, '')) LIKE '%non-trading%'
                        THEN -100 ELSE 0
                      END AS RankingScore,
                CONCAT(
                    'Authority fast gate. Status: ', ISNULL(record.CompanyStatus, 'unknown'),
                    '; accounts category: ', ISNULL(record.AccountsCategory, 'unknown'),
                    '; last accounts: ', ISNULL(CONVERT(NVARCHAR(10), record.AccountsLastMadeUpDate, 23), 'unknown'),
                    '; outstanding mortgages: ', ISNULL(CONVERT(NVARCHAR(16), record.NumMortOutstanding), 'unknown'),
                    '; incorporated: ', ISNULL(CONVERT(NVARCHAR(10), record.IncorporationDate, 23), 'unknown'),
                    '.') AS RankingRationale
            INTO #AuthorityRecords
            FROM NormalizedRecords record
            WHERE record.AuthorityRecordSequence = 1;

            CREATE UNIQUE CLUSTERED INDEX IX_AuthorityRecords_SourceRecordId
                ON #AuthorityRecords(SourceRecordId);

            UPDATE address
            SET
                address.IsVerified = 1,
                address.PoBox = record.RegPoBox,
                address.Line1 = record.RegAddressLine1,
                address.Line2 = record.RegAddressLine2,
                address.TownOrCity = record.RegPostTown,
                address.StateOrProvince = record.RegCounty,
                address.ZipOrPostalCode = record.RegPostCode,
                address.CountryId = LEFT(record.RegCountry, 64),
                address.VerificationNotes = CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                address.LastUpdatedBy = @ExecutionUserId,
                address.LastUpdated = @Now
            FROM masterdata.Addresses address
            INNER JOIN #AuthorityRecords record
                ON address.SourceSystem = @SourceSystem
                AND address.LegacyId = record.SourceRecordId
            LEFT JOIN masterdata.Companies company
                ON company.SourceSystem = @SourceSystem
                AND company.SourceRecordId = record.SourceRecordId
            WHERE company.Id IS NULL
                OR company.AuthorityRecordHash IS NULL
                OR company.AuthorityRecordHash <> record.AuthorityRecordHash;

            INSERT INTO masterdata.Addresses
            (
                Id, LegacyId, SourceSystem, IsVerified, PoBox, Line1, Line2,
                TownOrCity, StateOrProvince, ZipOrPostalCode, CountryId,
                VerificationNotes, CreatedBy, LastUpdatedBy, CreatedOn, LastUpdated
            )
            SELECT
                NEWID(), record.SourceRecordId, @SourceSystem, 1, record.RegPoBox,
                record.RegAddressLine1, record.RegAddressLine2, record.RegPostTown,
                record.RegCounty, record.RegPostCode, LEFT(record.RegCountry, 64),
                CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                @ExecutionUserId, @ExecutionUserId, @Now, @Now
            FROM #AuthorityRecords record
            WHERE COALESCE(
                    record.RegPoBox,
                    record.RegAddressLine1,
                    record.RegAddressLine2,
                    record.RegPostTown,
                    record.RegCounty,
                    record.RegPostCode,
                    record.RegCountry) IS NOT NULL
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM masterdata.Addresses address
                    WHERE address.SourceSystem = @SourceSystem
                        AND address.LegacyId = record.SourceRecordId
                );

            UPDATE company
            SET
                company.SourceSystem = @SourceSystem,
                company.SourceRecordId = record.SourceRecordId,
                company.LastUpdatedBy = @ExecutionUserId,
                company.LastUpdated = @Now
            FROM masterdata.Companies company
            INNER JOIN #AuthorityRecords record
                ON record.SourceRecordId = company.CompanyNumber
            WHERE company.SourceSystem IS NULL
                AND company.SourceRecordId IS NULL
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM masterdata.Companies keyed
                    WHERE keyed.SourceSystem = @SourceSystem
                        AND keyed.SourceRecordId = record.SourceRecordId
                );

            UPDATE company
            SET
                company.IsVerified = 1,
                company.OfficialName = COALESCE(NULLIF(record.CompanyName, ''), company.OfficialName),
                company.LegalEntityName = COALESCE(NULLIF(record.CompanyName, ''), company.LegalEntityName),
                company.CompanyNumber = record.SourceRecordId,
                company.CompanyCategory = record.CompanyCategory,
                company.CompanyStatus = record.CompanyStatus,
                company.CountryOfOrigin = record.CountryOfOrigin,
                company.IncorporatedOn = record.IncorporationDate,
                company.DissolvedOn = record.DissolutionDate,
                company.PrimarySicCodes = record.SicCodes,
                company.RegistryUri = record.RegistryUri,
                company.PreviousNamesJson = record.PreviousNamesJson,
                company.RegisteredAddressId = COALESCE(address.Id, company.RegisteredAddressId),
                company.VerificationNotes = CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                company.RankingScore = record.RankingScore,
                company.RankingRationale = CONCAT(record.RankingRationale, ' Score: ', record.RankingScore, '.'),
                company.AuthorityRecordHash = record.AuthorityRecordHash,
                company.LastUpdatedBy = @ExecutionUserId,
                company.LastUpdated = @Now
            FROM masterdata.Companies company
            INNER JOIN #AuthorityRecords record
                ON company.SourceSystem = @SourceSystem
                AND company.SourceRecordId = record.SourceRecordId
            LEFT JOIN masterdata.Addresses address
                ON address.SourceSystem = @SourceSystem
                AND address.LegacyId = record.SourceRecordId
            WHERE company.AuthorityRecordHash IS NULL
                OR company.AuthorityRecordHash <> record.AuthorityRecordHash;

            INSERT INTO masterdata.Companies
            (
                Id,
                LegacyId,
                SourceSystem,
                SourceRecordId,
                AuthorityRecordHash,
                IsVerified,
                OfficialName,
                LegalEntityName,
                TradingName,
                CompanyNumber,
                VatNumber,
                CompanyCategory,
                CompanyStatus,
                CountryOfOrigin,
                IncorporatedOn,
                DissolvedOn,
                PrimarySicCodes,
                RegistryUri,
                PreviousNamesJson,
                WebsiteUrl,
                ContactEmailAddress,
                ContactPhoneNumber,
                ResearchSummary,
                VerificationNotes,
                AnnualRevenue,
                RevenueCurrency,
                EmployeeCount,
                RankingScore,
                RankingRationale,
                IsProspectingSuppressed,
                ProspectingSuppressedReason,
                ProspectingSuppressedOn,
                RegisteredAddressId,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                NULL,
                @SourceSystem,
                record.SourceRecordId,
                record.AuthorityRecordHash,
                1,
                COALESCE(NULLIF(record.CompanyName, ''), 'Imported company'),
                record.CompanyName,
                NULL,
                record.SourceRecordId,
                NULL,
                record.CompanyCategory,
                record.CompanyStatus,
                record.CountryOfOrigin,
                record.IncorporationDate,
                record.DissolutionDate,
                record.SicCodes,
                record.RegistryUri,
                record.PreviousNamesJson,
                NULL,
                NULL,
                NULL,
                NULL,
                CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                NULL,
                NULL,
                NULL,
                record.RankingScore,
                CONCAT(record.RankingRationale, ' Score: ', record.RankingScore, '.'),
                0,
                NULL,
                NULL,
                address.Id,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM #AuthorityRecords record
            LEFT JOIN masterdata.Addresses address
                ON address.SourceSystem = @SourceSystem
                AND address.LegacyId = record.SourceRecordId
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM masterdata.Companies company
                WHERE company.SourceSystem = @SourceSystem
                    AND company.SourceRecordId = record.SourceRecordId
            );

            UPDATE imported
            SET
                imported.ImportedRowCount = CASE
                    WHEN CONVERT(BIGINT, imported.ImportedRowCount) + @ProcessedRowCount > 2147483647
                    THEN 2147483647
                    ELSE imported.ImportedRowCount + @ProcessedRowCount
                END,
                imported.TotalRowCount = CASE
                    WHEN imported.TotalRowCount < imported.ImportedRowCount + @ProcessedRowCount
                    THEN imported.ImportedRowCount + @ProcessedRowCount
                    ELSE imported.TotalRowCount
                END,
                imported.ProcessingCheckpoint = CONCAT('authority-merge:', imported.ImportedRowCount + @ProcessedRowCount),
                imported.LastUpdatedBy = @ExecutionUserId,
                imported.LastUpdated = @Now
            FROM leads.Imports imported
            WHERE imported.Id = @ImportId;

            DELETE stage
            FROM crm.CompaniesHouseImportStaging stage
            INNER JOIN #BatchSlice batchSlice
                ON batchSlice.StagingRowId = stage.StagingRowId;

            COMMIT TRANSACTION;
            """;

        await ExecuteNonQueryAsync(
            sql,
            [
                new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId },
                new SqlParameter("@ImportId", SqlDbType.UniqueIdentifier) { Value = provenance.ImportId },
                new SqlParameter("@MergeBatchSize", SqlDbType.Int) { Value = mergeBatchSize },
                new SqlParameter("@SourceFileName", SqlDbType.NVarChar, 256) { Value = sourceFileName },
                new SqlParameter("@SourceSystem", SqlDbType.NVarChar, 128) { Value = authorityDataOptions.SourceSystem },
                new SqlParameter("@ImportPrefix", SqlDbType.NVarChar, 128) { Value = ImportedFromAuthorityPrefix },
                new SqlParameter("@ExecutionUserId", SqlDbType.NVarChar, 256) { Value = executionUserId }
            ],
            cancellationToken);
    }

    async ValueTask BackfillLegacyImportProvenanceAsync(
        Guid sourceId,
        Guid importId,
        string sourceFileName,
        AuthorityDataOptions authorityDataOptions,
        string executionUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET XACT_ABORT ON;

            DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

            UPDATE lead
            SET
                lead.SourceId = @SourceId,
                lead.LastUpdatedBy = @ExecutionUserId,
                lead.LastUpdated = @Now
            FROM leads.Leads lead
            WHERE lead.SourceSystem = @SourceSystem
                AND lead.SourceFileName = @SourceFileName
                AND lead.SourceId IS NULL;

            INSERT INTO leads.ImportLinks
            (
                Id,
                ImportId,
                SourceId,
                CompanyId,
                LeadId,
                CompanyContactId,
                SourceRowKey,
                SourceRowNumber,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                @ImportId,
                @SourceId,
                company.Id,
                lead.Id,
                NULL,
                lead.SourceRecordId,
                NULL,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM leads.Leads lead
            INNER JOIN masterdata.Companies company
                ON company.CompanyNumber = lead.SourceRecordId
            WHERE lead.SourceSystem = @SourceSystem
                AND lead.SourceFileName = @SourceFileName
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM leads.ImportLinks existing
                    WHERE existing.ImportId = @ImportId
                        AND existing.SourceId = @SourceId
                        AND existing.SourceRowKey = lead.SourceRecordId
                );

            UPDATE imported
            SET
                imported.ImportedRowCount = CASE
                    WHEN imported.ImportedRowCount > provenance.ImportedRowCount
                    THEN imported.ImportedRowCount
                    ELSE provenance.ImportedRowCount
                END,
                imported.TotalRowCount = CASE
                    WHEN provenance.ImportedRowCount > imported.TotalRowCount THEN provenance.ImportedRowCount
                    ELSE imported.TotalRowCount
                END,
                imported.LastUpdatedBy = @ExecutionUserId,
                imported.LastUpdated = @Now
            FROM leads.Imports imported
            CROSS APPLY
            (
                SELECT COUNT_BIG(*) AS ImportedRowCount
                FROM leads.ImportLinks link
                WHERE link.ImportId = imported.Id
            ) provenance
            WHERE imported.Id = @ImportId;
            """;

        await ExecuteNonQueryAsync(
            sql,
            [
                new SqlParameter("@SourceId", SqlDbType.UniqueIdentifier) { Value = sourceId },
                new SqlParameter("@ImportId", SqlDbType.UniqueIdentifier) { Value = importId },
                new SqlParameter("@SourceSystem", SqlDbType.NVarChar, 128) { Value = authorityDataOptions.SourceSystem },
                new SqlParameter("@SourceFileName", SqlDbType.NVarChar, 256) { Value = sourceFileName },
                new SqlParameter("@ExecutionUserId", SqlDbType.NVarChar, 256) { Value = executionUserId }
            ],
            cancellationToken);
    }

    async ValueTask MarkLegacyImportCompletedAsync(
        Guid importId,
        string executionUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

            UPDATE leads.Imports
            SET
                TotalRowCount = ImportedRowCount,
                JobStatus = @CompletedJobStatus,
                ProcessingStatus = @CompletedProcessingStatus,
                ProcessingCheckpoint = 'completed',
                ProcessingCompletedOn = @Now,
                LastUpdatedBy = @ExecutionUserId,
                LastUpdated = @Now
            WHERE Id = @ImportId;
            """;

        await ExecuteNonQueryAsync(
            sql,
            [
                new SqlParameter("@ImportId", SqlDbType.UniqueIdentifier) { Value = importId },
                new SqlParameter("@CompletedJobStatus", SqlDbType.Int) { Value = (int)ImportJobStatus.Completed },
                new SqlParameter("@CompletedProcessingStatus", SqlDbType.Int) { Value = (int)ImportProcessingStatus.Completed },
                new SqlParameter("@ExecutionUserId", SqlDbType.NVarChar, 256) { Value = executionUserId }
            ],
            cancellationToken);
    }

    async ValueTask DeleteStagedBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM crm.CompaniesHouseImportStaging WHERE BatchId = @BatchId;";
        await ExecuteNonQueryAsync(
            sql,
            [new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId }],
            cancellationToken);
    }

    async ValueTask ExecuteNonQueryAsync(
        string sql,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        using SqlConnection connection = new(GetAdminConnectionString());
        await connection.OpenAsync(cancellationToken);

        using SqlCommand command = new(sql, connection)
        {
            CommandTimeout = 0
        };

        foreach (SqlParameter parameter in parameters)
            command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async ValueTask<object> ExecuteScalarAsync(
        string sql,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        using SqlConnection connection = new(GetAdminConnectionString());
        await connection.OpenAsync(cancellationToken);

        using SqlCommand command = new(sql, connection)
        {
            CommandTimeout = 0
        };

        foreach (SqlParameter parameter in parameters)
            command.Parameters.Add(parameter);

        return await command.ExecuteScalarAsync(cancellationToken);
    }

    DataTable CreateStageTable()
    {
        DataTable table = new("CompaniesHouseImportStaging");
        table.Columns.Add("BatchId", typeof(Guid));
        table.Columns.Add("SourceFileName", typeof(string));
        table.Columns.Add("CompanyName", typeof(string));
        table.Columns.Add("CompanyNumber", typeof(string));
        table.Columns.Add("RegCareOf", typeof(string));
        table.Columns.Add("RegPoBox", typeof(string));
        table.Columns.Add("RegAddressLine1", typeof(string));
        table.Columns.Add("RegAddressLine2", typeof(string));
        table.Columns.Add("RegPostTown", typeof(string));
        table.Columns.Add("RegCounty", typeof(string));
        table.Columns.Add("RegCountry", typeof(string));
        table.Columns.Add("RegPostCode", typeof(string));
        table.Columns.Add("CompanyCategory", typeof(string));
        table.Columns.Add("CompanyStatus", typeof(string));
        table.Columns.Add("CountryOfOrigin", typeof(string));
        table.Columns.Add("DissolutionDate", typeof(DateTime));
        table.Columns.Add("IncorporationDate", typeof(DateTime));
        table.Columns.Add("AccountsCategory", typeof(string));
        table.Columns.Add("AccountsLastMadeUpDate", typeof(DateTime));
        table.Columns.Add("NumMortOutstanding", typeof(int));
        table.Columns.Add("SicCodes", typeof(string));
        table.Columns.Add("RegistryUri", typeof(string));
        table.Columns.Add("PreviousNamesJson", typeof(string));
        table.Columns.Add("AuthorityRecordHash", typeof(string));
        return table;
    }

    string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "..",
            "..",
            configuredPath));
    }

    string GetAdminConnectionString() =>
        !string.IsNullOrWhiteSpace(platformConfiguration.AdminConnectionString)
            ? platformConfiguration.AdminConnectionString
            : platformConfiguration.ConnectionString;

    string ResolveExecutionUserId() =>
        string.IsNullOrWhiteSpace(agentWorkflowOptions.Value.ExecutionUserId)
            ? "system"
            : agentWorkflowOptions.Value.ExecutionUserId.Trim();

    static string NormalizeHeader(string header) =>
        string.IsNullOrWhiteSpace(header)
            ? string.Empty
            : header.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
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

    static DateTime? ParseNullableDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, UkCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed))
            return parsed.Date;

        return null;
    }

    static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    static string BuildPreviousNamesJson(string[] row, IReadOnlyDictionary<string, int> headers)
    {
        List<object> previousNames = [];

        for (int index = 1; index <= 10; index++)
        {
            string changedOn = GetValue(row, headers, $"previousname{index}.condate");
            string name = GetValue(row, headers, $"previousname{index}.companyname");

            if (string.IsNullOrWhiteSpace(name))
                continue;

            previousNames.Add(new
            {
                ChangedOn = changedOn,
                Name = name
            });
        }

        return previousNames.Count == 0
            ? null
            : JsonSerializer.Serialize(previousNames);
    }

    static object ToDbValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    static object ToDbValue(DateTime? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    static object ToDbValue(int? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    static object ToSqlString(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    static void MoveFile(string sourcePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        string destinationPath = Path.Combine(
            destinationDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}__{Path.GetFileName(sourcePath)}");

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        File.Move(sourcePath, destinationPath);
    }

    static void MoveSourceFileIfPresent(
        string sourceFileName,
        string destinationDirectory,
        params string[] sourceDirectories)
    {
        foreach (string sourceDirectory in sourceDirectories)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                continue;

            string directPath = Path.Combine(sourceDirectory, sourceFileName);
            if (File.Exists(directPath))
            {
                MoveFile(directPath, destinationDirectory);
                return;
            }

            FileInfo file = new DirectoryInfo(sourceDirectory)
                .GetFiles($"*__{sourceFileName}", System.IO.SearchOption.TopDirectoryOnly)
                .OrderByDescending(item => item.LastWriteTimeUtc)
                .FirstOrDefault();

            if (file is not null)
            {
                MoveFile(file.FullName, destinationDirectory);
                return;
            }
        }
    }

    sealed class StagedBatchInfo
    {
        public Guid BatchId { get; init; }
        public string SourceFileName { get; init; } = string.Empty;
        public long PendingRowCount { get; init; }
    }

    sealed class LegacyImportProvenance
    {
        public Guid SourceId { get; init; }
        public Guid ImportId { get; init; }
    }
}
