using System.Data;
using System.Globalization;
using System.Text.Json;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

namespace ClientRelationshipManagement.Web.Services.Leads;

public sealed class AuthorityDataImportService(
    IHostEnvironment environment,
    PlatformConfiguration platformConfiguration,
    IPlatformDbContextFactory dbContextFactory,
    IWorkflowAutomationService workflowAutomationService,
    IOptions<AuthorityDataOptions> options,
    IOptions<AgentWorkflowOptions> agentWorkflowOptions,
    ILoggingBroker<AuthorityDataImportService> loggingBroker)
    : IAuthorityDataImportService
{
    const string CompaniesHouseSource = "CompaniesHouse";
    const string ImportedFromAuthorityPrefix = "Imported from Companies House";
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

    async ValueTask<LeadProcessSeedData> GetLeadProcessSeedDataAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        ProcessDefinition definition = await context.ProcessDefinitions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.ScopeType == ProcessScopeType.Lead
                && (item.LifecycleState == ProcessDefinitionLifecycleState.Active || item.IsActive)
                && item.IsDefault)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (definition is null)
            return null;

        ProcessStep entryStep = await context.ProcessSteps
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionId == definition.Id && item.IsEntryPoint && item.IsActive)
            .OrderBy(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (entryStep is null)
            return null;

        return new LeadProcessSeedData
        {
            ProcessDefinitionId = definition.Id,
            EntryStepId = entryStep.Id,
            ActionType = entryStep.ActionType,
            DueAfterDays = entryStep.DueAfterDays,
            DueAfterHours = entryStep.DueAfterHours,
            TitleTemplate = entryStep.TaskTitleTemplate ?? string.Empty,
            InstructionsTemplate = entryStep.TaskInstructionsTemplate ?? string.Empty,
            EmailSubjectTemplate = entryStep.EmailSubjectTemplate ?? string.Empty,
            EmailBodyTemplate = entryStep.EmailBodyTemplate ?? string.Empty,
            CallScriptTemplate = entryStep.CallScriptTemplate ?? string.Empty,
            QuestionSetTemplate = entryStep.QuestionSetTemplate ?? string.Empty
        };
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
        await workflowAutomationService.EnsureSeedProcessesAsync(cancellationToken);

        LeadProcessSeedData leadProcessSeedData = await GetLeadProcessSeedDataAsync(
            authorityDataOptions.DefaultTenantId,
            cancellationToken);

        if (leadProcessSeedData is null)
            throw new InvalidOperationException("The default lead process definition could not be resolved.");

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
            leadProcessSeedData,
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
            item => item.Name == authorityDataOptions.SourceSystem && item.CountryCode == "GB",
            cancellationToken);

        if (source is null)
        {
            source = new Source
            {
                Id = Guid.NewGuid(),
                Name = authorityDataOptions.SourceSystem,
                SourceType = SourceType.Authority,
                CountryCode = "GB",
                IsAuthoritative = true,
                Notes = "Companies House authoritative company register import.",
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
                    SicCodes NVARCHAR(2048) NULL,
                    RegistryUri NVARCHAR(512) NULL,
                    PreviousNamesJson NVARCHAR(MAX) NULL
                );

                CREATE INDEX IX_CompaniesHouseImportStaging_BatchId
                    ON crm.CompaniesHouseImportStaging(BatchId);
            END;

            IF COL_LENGTH(N'crm.CompaniesHouseImportStaging', N'StagingRowId') IS NULL
            BEGIN
                ALTER TABLE crm.CompaniesHouseImportStaging
                    ADD StagingRowId BIGINT IDENTITY(1,1) NOT NULL;
            END;

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
                WHERE object_id = OBJECT_ID(N'masterdata.Addresses')
                    AND name = N'IX_Addresses_Source_Legacy'
            )
            BEGIN
                CREATE INDEX IX_Addresses_Source_Legacy
                    ON masterdata.Addresses(SourceSystem, LegacyId);
            END;
            """;

        await ExecuteNonQueryAsync(sql, [], cancellationToken);
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
            dataRow["SicCodes"] = ToDbValue(string.Join(" | ", new[]
            {
                GetValue(row, headerLookup, "siccode.sictext1"),
                GetValue(row, headerLookup, "siccode.sictext2"),
                GetValue(row, headerLookup, "siccode.sictext3"),
                GetValue(row, headerLookup, "siccode.sictext4")
            }.Where(value => !string.IsNullOrWhiteSpace(value))));
            dataRow["RegistryUri"] = ToDbValue(GetValue(row, headerLookup, "uri"));
            dataRow["PreviousNamesJson"] = ToDbValue(BuildPreviousNamesJson(row, headerLookup));
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
        LeadProcessSeedData leadProcessSeedData,
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
                leadProcessSeedData,
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
        LeadProcessSeedData leadProcessSeedData,
        LegacyImportProvenance provenance,
        string executionUserId,
        int mergeBatchSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

            SELECT TOP (@MergeBatchSize)
                stage.StagingRowId,
                stage.BatchId,
                stage.SourceFileName,
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
                stage.SicCodes,
                stage.RegistryUri,
                stage.PreviousNamesJson
            INTO #BatchSlice
            FROM crm.CompaniesHouseImportStaging stage
            WHERE stage.BatchId = @BatchId
            ORDER BY stage.StagingRowId;

            CREATE CLUSTERED INDEX IX_BatchSlice_StagingRowId
                ON #BatchSlice(StagingRowId);

            CREATE INDEX IX_BatchSlice_CompanyNumber
                ON #BatchSlice(CompanyNumber);

            ;WITH StagedAddresses AS
            (
                SELECT DISTINCT
                    NULLIF(LTRIM(RTRIM(CompanyNumber)), '') AS CompanyNumber,
                    NULLIF(LTRIM(RTRIM(RegPoBox)), '') AS RegPoBox,
                    NULLIF(LTRIM(RTRIM(RegAddressLine1)), '') AS RegAddressLine1,
                    NULLIF(LTRIM(RTRIM(CONCAT_WS(', ', NULLIF(LTRIM(RTRIM(RegCareOf)), ''), NULLIF(LTRIM(RTRIM(RegAddressLine2)), '')))), '') AS RegAddressLine2,
                    NULLIF(LTRIM(RTRIM(RegPostTown)), '') AS RegPostTown,
                    NULLIF(LTRIM(RTRIM(RegCounty)), '') AS RegCounty,
                    NULLIF(LTRIM(RTRIM(RegPostCode)), '') AS RegPostCode,
                    NULLIF(LTRIM(RTRIM(RegCountry)), '') AS RegCountry
                FROM #BatchSlice
                WHERE NULLIF(LTRIM(RTRIM(CompanyNumber)), '') IS NOT NULL
            )
            INSERT INTO masterdata.Addresses
            (
                Id,
                LegacyId,
                SourceSystem,
                IsVerified,
                PoBox,
                Line1,
                Line2,
                TownOrCity,
                StateOrProvince,
                ZipOrPostalCode,
                CountryId,
                VerificationNotes,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                address.CompanyNumber,
                @SourceSystem,
                1,
                address.RegPoBox,
                address.RegAddressLine1,
                address.RegAddressLine2,
                address.RegPostTown,
                address.RegCounty,
                address.RegPostCode,
                address.RegCountry,
                CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM StagedAddresses address
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM masterdata.Addresses existing
                WHERE existing.SourceSystem = @SourceSystem
                    AND existing.LegacyId = address.CompanyNumber
            );

            ;WITH StagedCompanies AS
            (
                SELECT
                    stage.CompanyName,
                    stage.CompanyNumber,
                    stage.CompanyCategory,
                    stage.CompanyStatus,
                    stage.CountryOfOrigin,
                    stage.DissolutionDate,
                    stage.IncorporationDate,
                    stage.SicCodes,
                    stage.RegistryUri,
                    stage.PreviousNamesJson,
                    NULLIF(LTRIM(RTRIM(CONCAT_WS(', ',
                        NULLIF(LTRIM(RTRIM(stage.RegAddressLine1)), ''),
                        NULLIF(LTRIM(RTRIM(stage.RegAddressLine2)), ''),
                        NULLIF(LTRIM(RTRIM(stage.RegPostTown)), ''),
                        NULLIF(LTRIM(RTRIM(stage.RegCounty)), ''),
                        NULLIF(LTRIM(RTRIM(stage.RegPostCode)), ''),
                        NULLIF(LTRIM(RTRIM(stage.RegCountry)), '')))), '') AS RegisteredOfficeText,
                    address.Id AS RegisteredAddressId
                FROM #BatchSlice stage
                OUTER APPLY
                (
                    SELECT TOP 1 existing.Id
                    FROM masterdata.Addresses existing
                    WHERE existing.SourceSystem = @SourceSystem
                        AND existing.LegacyId = NULLIF(LTRIM(RTRIM(stage.CompanyNumber)), '')
                ) address
                WHERE NULLIF(LTRIM(RTRIM(stage.CompanyNumber)), '') IS NOT NULL
            )
            MERGE masterdata.Companies AS target
            USING StagedCompanies AS source
            ON target.CompanyNumber = source.CompanyNumber
            WHEN MATCHED THEN UPDATE SET
                target.SourceSystem = @SourceSystem,
                target.SourceRecordId = source.CompanyNumber,
                target.IsVerified = 1,
                target.OfficialName = COALESCE(NULLIF(source.CompanyName, ''), target.OfficialName),
                target.LegalEntityName = COALESCE(NULLIF(source.CompanyName, ''), target.LegalEntityName),
                target.CompanyCategory = source.CompanyCategory,
                target.CompanyStatus = source.CompanyStatus,
                target.CountryOfOrigin = source.CountryOfOrigin,
                target.IncorporatedOn = source.IncorporationDate,
                target.DissolvedOn = source.DissolutionDate,
                target.PrimarySicCodes = source.SicCodes,
                target.RegistryUri = source.RegistryUri,
                target.PreviousNamesJson = source.PreviousNamesJson,
                target.RegisteredOfficeText = source.RegisteredOfficeText,
                target.RegisteredAddressId = COALESCE(source.RegisteredAddressId, target.RegisteredAddressId),
                target.VerificationNotes = CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                target.LastUpdatedBy = @ExecutionUserId,
                target.LastUpdated = @Now
            WHEN NOT MATCHED THEN INSERT
            (
                Id,
                LegacyId,
                SourceSystem,
                SourceRecordId,
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
                RegisteredOfficeText,
                ResearchSummary,
                VerificationNotes,
                RegisteredAddressId,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            VALUES
            (
                NEWID(),
                NULL,
                @SourceSystem,
                source.CompanyNumber,
                1,
                COALESCE(NULLIF(source.CompanyName, ''), 'Imported company'),
                source.CompanyName,
                NULL,
                source.CompanyNumber,
                NULL,
                source.CompanyCategory,
                source.CompanyStatus,
                source.CountryOfOrigin,
                source.IncorporationDate,
                source.DissolutionDate,
                source.SicCodes,
                source.RegistryUri,
                source.PreviousNamesJson,
                NULL,
                NULL,
                NULL,
                source.RegisteredOfficeText,
                NULL,
                CONCAT(@ImportPrefix, ' file ', @SourceFileName),
                source.RegisteredAddressId,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            );

            INSERT INTO leads.Leads
            (
                Id,
                SourceSystem,
                SourceId,
                SourceRecordId,
                SourceFileName,
                TenantId,
                Status,
                RawCompanyName,
                RawTradingName,
                RawCompanyNumber,
                RawVatNumber,
                RawWebsiteUrl,
                RawContactEmailAddress,
                RawContactPhoneNumber,
                RawAddressText,
                QualificationNotes,
                CompanyId,
                TenantCompanyRelationshipId,
                OpportunityId,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                @SourceSystem,
                @SourceId,
                company.CompanyNumber,
                @SourceFileName,
                @DefaultTenantId,
                @LeadStatusImported,
                company.OfficialName,
                company.TradingName,
                company.CompanyNumber,
                company.VatNumber,
                company.WebsiteUrl,
                company.ContactEmailAddress,
                company.ContactPhoneNumber,
                company.RegisteredOfficeText,
                CONCAT_WS(CHAR(10),
                    CONCAT('Authority source status: ', company.CompanyStatus),
                    CONCAT('Category: ', company.CompanyCategory),
                    CONCAT('Origin: ', company.CountryOfOrigin),
                    CONCAT('SIC: ', company.PrimarySicCodes),
                    company.RegistryUri),
                company.Id,
                NULL,
                NULL,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM masterdata.Companies company
            INNER JOIN #BatchSlice stage
                ON stage.CompanyNumber = company.CompanyNumber
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM leads.Leads lead
                WHERE lead.TenantId = @DefaultTenantId
                    AND lead.SourceSystem = @SourceSystem
                    AND lead.SourceRecordId = company.CompanyNumber
            );

            INSERT INTO process.ProcessInstances
            (
                Id,
                ProcessDefinitionId,
                LeadId,
                TenantCompanyRelationshipId,
                OpportunityId,
                ClientAccountId,
                CurrentProcessStepId,
                CurrentProcessTaskId,
                State,
                CompletionOutcomeKey,
                StartedOn,
                CompletedOn,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                @LeadProcessDefinitionId,
                lead.Id,
                NULL,
                NULL,
                NULL,
                @LeadEntryStepId,
                NULL,
                @ProcessInstanceStateActive,
                NULL,
                @Now,
                NULL,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM leads.Leads lead
            INNER JOIN #BatchSlice stage
                ON stage.CompanyNumber = lead.SourceRecordId
            WHERE lead.TenantId = @DefaultTenantId
                AND lead.SourceSystem = @SourceSystem
                AND lead.SourceFileName = @SourceFileName
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM process.ProcessInstances processInstance
                    WHERE processInstance.LeadId = lead.Id
                        AND processInstance.State = @ProcessInstanceStateActive
                );

            INSERT INTO process.ProcessTasks
            (
                Id,
                ProcessInstanceId,
                ProcessStepId,
                LeadId,
                TenantCompanyRelationshipId,
                OpportunityId,
                ClientAccountId,
                EmailId,
                ActionType,
                State,
                DueOn,
                RenderedTitle,
                RenderedInstructions,
                RenderedEmailSubject,
                RenderedEmailBody,
                RenderedCallScript,
                RenderedQuestionSet,
                CompletionOutcomeKey,
                CompletionNotes,
                CompletedOn,
                CompletedBy,
                CreatedBy,
                LastUpdatedBy,
                CreatedOn,
                LastUpdated
            )
            SELECT
                NEWID(),
                processInstance.Id,
                @LeadEntryStepId,
                lead.Id,
                NULL,
                NULL,
                NULL,
                NULL,
                @LeadEntryActionType,
                @ProcessTaskStatePending,
                DATEADD(HOUR, @LeadEntryDueAfterHours, DATEADD(DAY, @LeadEntryDueAfterDays, @Now)),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryTitleTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryInstructionsTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryEmailSubjectTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryEmailBodyTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryCallScriptTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@LeadEntryQuestionSetTemplate,
                    '{{Lead.RawCompanyName}}', ISNULL(lead.RawCompanyName, '')),
                    '{{Lead.RawTradingName}}', ISNULL(lead.RawTradingName, '')),
                    '{{Lead.RawCompanyNumber}}', ISNULL(lead.RawCompanyNumber, '')),
                    '{{Lead.RawWebsiteUrl}}', ISNULL(lead.RawWebsiteUrl, '')),
                    '{{Lead.QualificationNotes}}', ISNULL(lead.QualificationNotes, '')),
                NULL,
                NULL,
                NULL,
                NULL,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM process.ProcessInstances processInstance
            INNER JOIN leads.Leads lead
                ON lead.Id = processInstance.LeadId
            INNER JOIN #BatchSlice stage
                ON stage.CompanyNumber = lead.SourceRecordId
            WHERE processInstance.ProcessDefinitionId = @LeadProcessDefinitionId
                AND processInstance.CurrentProcessStepId = @LeadEntryStepId
                AND processInstance.State = @ProcessInstanceStateActive
                AND lead.TenantId = @DefaultTenantId
                AND lead.SourceSystem = @SourceSystem
                AND lead.SourceFileName = @SourceFileName
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM process.ProcessTasks processTask
                    WHERE processTask.ProcessInstanceId = processInstance.Id
                        AND processTask.State = @ProcessTaskStatePending
                );

            UPDATE processInstance
            SET
                processInstance.CurrentProcessTaskId = processTask.Id,
                processInstance.LastUpdatedBy = @ExecutionUserId,
                processInstance.LastUpdated = @Now
            FROM process.ProcessInstances processInstance
            INNER JOIN process.ProcessTasks processTask
                ON processTask.ProcessInstanceId = processInstance.Id
                AND processTask.State = @ProcessTaskStatePending
            INNER JOIN leads.Leads lead
                ON lead.Id = processInstance.LeadId
            INNER JOIN #BatchSlice stage
                ON stage.CompanyNumber = lead.SourceRecordId
            WHERE processInstance.ProcessDefinitionId = @LeadProcessDefinitionId
                AND processInstance.CurrentProcessStepId = @LeadEntryStepId
                AND processInstance.State = @ProcessInstanceStateActive
                AND processInstance.CurrentProcessTaskId IS NULL
                AND lead.TenantId = @DefaultTenantId
                AND lead.SourceSystem = @SourceSystem
                AND lead.SourceFileName = @SourceFileName;

            UPDATE lead
            SET
                lead.SourceId = @SourceId,
                lead.LastUpdatedBy = @ExecutionUserId,
                lead.LastUpdated = @Now
            FROM leads.Leads lead
            INNER JOIN #BatchSlice stage
                ON stage.CompanyNumber = lead.SourceRecordId
            WHERE lead.TenantId = @DefaultTenantId
                AND lead.SourceSystem = @SourceSystem
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
                company.CompanyNumber,
                NULL,
                @ExecutionUserId,
                @ExecutionUserId,
                @Now,
                @Now
            FROM #BatchSlice stage
            INNER JOIN masterdata.Companies company
                ON company.CompanyNumber = stage.CompanyNumber
            INNER JOIN leads.Leads lead
                ON lead.TenantId = @DefaultTenantId
                AND lead.SourceSystem = @SourceSystem
                AND lead.SourceRecordId = company.CompanyNumber
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM leads.ImportLinks existing
                WHERE existing.ImportId = @ImportId
                    AND existing.SourceId = @SourceId
                    AND existing.SourceRowKey = company.CompanyNumber
            );

            DELETE stage
            FROM crm.CompaniesHouseImportStaging stage
            INNER JOIN #BatchSlice batchSlice
                ON batchSlice.StagingRowId = stage.StagingRowId;

            COMMIT TRANSACTION;
            """;

        SqlParameter[] parameters =
        [
            new("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId },
            new("@ImportId", SqlDbType.UniqueIdentifier) { Value = provenance.ImportId },
            new("@SourceId", SqlDbType.UniqueIdentifier) { Value = provenance.SourceId },
            new("@MergeBatchSize", SqlDbType.Int) { Value = mergeBatchSize },
            new("@SourceFileName", SqlDbType.NVarChar, 256) { Value = sourceFileName },
            new("@SourceSystem", SqlDbType.NVarChar, 128) { Value = authorityDataOptions.SourceSystem },
            new("@DefaultTenantId", SqlDbType.NVarChar, 128) { Value = authorityDataOptions.DefaultTenantId },
            new("@ImportPrefix", SqlDbType.NVarChar, 128) { Value = ImportedFromAuthorityPrefix },
            new("@ExecutionUserId", SqlDbType.NVarChar, 256) { Value = executionUserId },
            new("@LeadStatusImported", SqlDbType.Int) { Value = (int)LeadStatus.Imported },
            new("@ProcessInstanceStateActive", SqlDbType.Int) { Value = (int)ProcessInstanceState.Active },
            new("@ProcessTaskStatePending", SqlDbType.Int) { Value = (int)ProcessTaskState.Pending },
            new("@LeadProcessDefinitionId", SqlDbType.UniqueIdentifier) { Value = leadProcessSeedData.ProcessDefinitionId },
            new("@LeadEntryStepId", SqlDbType.UniqueIdentifier) { Value = leadProcessSeedData.EntryStepId },
            new("@LeadEntryActionType", SqlDbType.Int) { Value = (int)leadProcessSeedData.ActionType },
            new("@LeadEntryDueAfterDays", SqlDbType.Int) { Value = leadProcessSeedData.DueAfterDays },
            new("@LeadEntryDueAfterHours", SqlDbType.Int) { Value = leadProcessSeedData.DueAfterHours },
            new("@LeadEntryTitleTemplate", SqlDbType.NVarChar, 512) { Value = leadProcessSeedData.TitleTemplate },
            new("@LeadEntryInstructionsTemplate", SqlDbType.NVarChar, -1) { Value = ToSqlString(leadProcessSeedData.InstructionsTemplate) },
            new("@LeadEntryEmailSubjectTemplate", SqlDbType.NVarChar, -1) { Value = ToSqlString(leadProcessSeedData.EmailSubjectTemplate) },
            new("@LeadEntryEmailBodyTemplate", SqlDbType.NVarChar, -1) { Value = ToSqlString(leadProcessSeedData.EmailBodyTemplate) },
            new("@LeadEntryCallScriptTemplate", SqlDbType.NVarChar, -1) { Value = ToSqlString(leadProcessSeedData.CallScriptTemplate) },
            new("@LeadEntryQuestionSetTemplate", SqlDbType.NVarChar, -1) { Value = ToSqlString(leadProcessSeedData.QuestionSetTemplate) }
        ];

        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
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
                imported.ImportedRowCount = provenance.ImportedRowCount,
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

            UPDATE imported
            SET
                imported.ImportedRowCount = provenance.ImportedRowCount,
                imported.TotalRowCount = provenance.ImportedRowCount,
                imported.JobStatus = @CompletedJobStatus,
                imported.ProcessingStatus = @CompletedProcessingStatus,
                imported.ProcessingCheckpoint = 'completed',
                imported.ProcessingCompletedOn = @Now,
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
        table.Columns.Add("SicCodes", typeof(string));
        table.Columns.Add("RegistryUri", typeof(string));
        table.Columns.Add("PreviousNamesJson", typeof(string));
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

    sealed class LeadProcessSeedData
    {
        public Guid ProcessDefinitionId { get; init; }
        public Guid EntryStepId { get; init; }
        public ProcessActionType ActionType { get; init; }
        public int DueAfterDays { get; init; }
        public int DueAfterHours { get; init; }
        public string TitleTemplate { get; init; } = string.Empty;
        public string InstructionsTemplate { get; init; } = string.Empty;
        public string EmailSubjectTemplate { get; init; } = string.Empty;
        public string EmailBodyTemplate { get; init; } = string.Empty;
        public string CallScriptTemplate { get; init; } = string.Empty;
        public string QuestionSetTemplate { get; init; } = string.Empty;
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
