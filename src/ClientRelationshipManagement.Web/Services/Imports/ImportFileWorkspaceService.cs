using System.Globalization;
using System.Text.Json;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

namespace ClientRelationshipManagement.Web.Services.Imports;

public sealed class ImportFileWorkspaceService(
    IHostEnvironment environment,
    IPlatformDbContextFactory dbContextFactory,
    IOptions<ImportWorkflowOptions> options)
    : IImportFileWorkspaceService
{
    public async ValueTask<ImportUploadSessionResponse> CreateUploadSessionAsync(
        HostedImportUploadSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == request.ImportId, cancellationToken)
            ?? throw new InvalidOperationException("Import could not be found.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string uploadSessionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string incomingDirectory = GetImportDirectory("Incoming", import.Id);
        Directory.CreateDirectory(incomingDirectory);
        Directory.CreateDirectory(GetChunkDirectory(import.Id));

        string storedFileName = $"{import.Id:N}{Path.GetExtension(request.FileName)}";
        import.OriginalFileName = string.IsNullOrWhiteSpace(import.OriginalFileName) ? request.FileName : import.OriginalFileName;
        import.ContentType = request.ContentType;
        import.SizeBytes = request.SizeBytes;
        import.StoredFilePath = Path.Combine(incomingDirectory, storedFileName);
        import.StoredObjectKey = $"Imports/Incoming/{import.Id:N}/{storedFileName}";
        import.UploadStatus = ImportUploadStatus.SessionCreated;
        import.JobStatus = ImportJobStatus.Draft;
        import.ProcessingStatus = ImportProcessingStatus.WaitingForReady;
        import.UploadSessionId = uploadSessionId;
        import.UploadSessionExpiresOn = now.AddMinutes(options.Value.UploadSessionExpiryMinutes);
        import.LastUpdated = now;
        import.LastUpdatedBy = "hosted-services";

        await context.SaveChangesAsync(cancellationToken);

        string baseUrl = options.Value.HostedServicesBaseUrl.TrimEnd('/');
        return new ImportUploadSessionResponse
        {
            ImportId = import.Id,
            UploadSessionId = uploadSessionId,
            ChunkSizeBytes = options.Value.ChunkSizeBytes,
            ExpiresOn = import.UploadSessionExpiresOn.Value,
            ChunkUploadUrl = $"{baseUrl}/internal/imports/{import.Id}/chunks/{{chunkIndex}}?uploadSessionId={Uri.EscapeDataString(uploadSessionId)}",
            CompleteUploadUrl = $"{baseUrl}/internal/imports/{import.Id}/complete-upload",
            StatusUrl = $"{baseUrl}/internal/imports/{import.Id}/upload-status"
        };
    }

    public async ValueTask<ImportStatusResponse> SaveChunkAsync(
        Guid importId,
        string uploadSessionId,
        int chunkIndex,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await GetValidSessionImportAsync(context, importId, uploadSessionId, cancellationToken);

        Directory.CreateDirectory(GetChunkDirectory(importId));
        string chunkPath = GetChunkPath(importId, chunkIndex);
        await using (FileStream output = File.Create(chunkPath))
        {
            await content.CopyToAsync(output, cancellationToken);
        }

        import.UploadStatus = ImportUploadStatus.Uploading;
        import.UploadedBytes = GetUploadedBytes(importId);
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = "hosted-services";
        await context.SaveChangesAsync(cancellationToken);

        return ToStatus(import);
    }

    public async ValueTask<ImportStatusResponse> CompleteUploadAsync(
        Guid importId,
        HostedImportCompleteUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await GetValidSessionImportAsync(context, importId, request.UploadSessionId, cancellationToken);

        if (string.IsNullOrWhiteSpace(import.StoredFilePath))
            throw new InvalidOperationException("Import has no stored file path.");

        Directory.CreateDirectory(Path.GetDirectoryName(import.StoredFilePath));
        await using (FileStream output = File.Create(import.StoredFilePath))
        {
            for (int index = 0; index < request.TotalChunks; index++)
            {
                string chunkPath = GetChunkPath(importId, index);
                if (!File.Exists(chunkPath))
                    throw new InvalidOperationException($"Upload chunk {index} is missing.");

                await using FileStream input = File.OpenRead(chunkPath);
                await input.CopyToAsync(output, cancellationToken);
            }
        }

        import.UploadedBytes = new FileInfo(import.StoredFilePath).Length;
        import.UploadStatus = ImportUploadStatus.Uploaded;
        import.UploadedOn = DateTimeOffset.UtcNow;
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = "hosted-services";
        await context.SaveChangesAsync(cancellationToken);
        return ToStatus(import);
    }

    public async ValueTask<ImportStatusResponse> AnalyseAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == importId, cancellationToken)
            ?? throw new InvalidOperationException("Import could not be found.");

        if (string.IsNullOrWhiteSpace(import.StoredFilePath) || !File.Exists(import.StoredFilePath))
            throw new InvalidOperationException("Upload must be completed before mapping can be analysed.");

        string[] headers = ReadHeaders(import.StoredFilePath);
        Dictionary<string, string> mapping = InferMapping(headers);
        import.MappingSnapshotJson = JsonSerializer.Serialize(new
        {
            format = "csv",
            generatedOn = DateTimeOffset.UtcNow,
            fields = mapping
        }, new JsonSerializerOptions { WriteIndented = true });

        import.JobStatus = ImportJobStatus.Analysed;
        import.ProcessingStatus = ImportProcessingStatus.WaitingForReady;
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = "hosted-services";
        await context.SaveChangesAsync(cancellationToken);

        return ToStatus(import);
    }

    public async ValueTask<ImportStatusResponse> GetStatusAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == importId, cancellationToken)
            ?? throw new InvalidOperationException("Import could not be found.");

        return ToStatus(import);
    }

    public async ValueTask DeleteFilesAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (string phase in new[] { "Incoming", "Canonical", "Archive", "Failed" })
        {
            string path = GetImportDirectory(phase, importId);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    async ValueTask<Import> GetValidSessionImportAsync(
        PlatformDbContext context,
        Guid importId,
        string uploadSessionId,
        CancellationToken cancellationToken)
    {
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == importId, cancellationToken)
            ?? throw new InvalidOperationException("Import could not be found.");

        if (!string.Equals(import.UploadSessionId, uploadSessionId, StringComparison.Ordinal))
            throw new InvalidOperationException("Upload session is not valid for this import.");

        if (import.UploadSessionExpiresOn.HasValue && import.UploadSessionExpiresOn.Value < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Upload session has expired.");

        return import;
    }

    string GetImportDirectory(string phase, Guid importId) =>
        Path.Combine(GetWorkspaceRoot(), "Imports", phase, importId.ToString("N", CultureInfo.InvariantCulture));

    string GetChunkDirectory(Guid importId) =>
        Path.Combine(GetImportDirectory("Incoming", importId), "chunks");

    string GetChunkPath(Guid importId, int chunkIndex) =>
        Path.Combine(GetChunkDirectory(importId), $"{chunkIndex:D10}.chunk");

    long GetUploadedBytes(Guid importId)
    {
        string chunkDirectory = GetChunkDirectory(importId);
        if (!Directory.Exists(chunkDirectory))
            return 0;

        return new DirectoryInfo(chunkDirectory)
            .GetFiles("*.chunk", System.IO.SearchOption.TopDirectoryOnly)
            .Sum(file => file.Length);
    }

    string GetWorkspaceRoot()
    {
        string configuredPath = options.Value.AgentWorkspacePath;
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }

    static string[] ReadHeaders(string filePath)
    {
        using TextFieldParser parser = new(filePath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        return parser.ReadFields() ?? [];
    }

    static Dictionary<string, string> InferMapping(string[] headers)
    {
        Dictionary<string, string> normalized = headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToDictionary(NormalizeHeader, header => header, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, string>
        {
            ["companyNumber"] = Pick(normalized, "companynumber", "company_number", "number", "regnumber"),
            ["name"] = Pick(normalized, "companyname", "company_name", "name", "organisationname", "businessname"),
            ["vatNumber"] = Pick(normalized, "vatnumber", "vat", "taxnumber", "taxreference"),
            ["addressLine1"] = Pick(normalized, "regaddressaddressline1", "addressline1", "line1", "address1"),
            ["addressLine2"] = Pick(normalized, "regaddressaddressline2", "addressline2", "line2", "address2"),
            ["townOrCity"] = Pick(normalized, "regaddressposttown", "town", "city", "townorcity"),
            ["county"] = Pick(normalized, "regaddresscounty", "county", "state", "province"),
            ["postcode"] = Pick(normalized, "regaddresspostcode", "postcode", "postalcode", "zip"),
            ["country"] = Pick(normalized, "regaddresscountry", "country", "countrycode"),
            ["status"] = Pick(normalized, "companystatus", "status"),
            ["category"] = Pick(normalized, "companycategory", "category"),
            ["sicCodes"] = Pick(normalized, "siccode.sictext_1", "siccodes", "industrycodes", "sic"),
            ["website"] = Pick(normalized, "website", "websiteurl", "url"),
            ["contactName"] = Pick(normalized, "contactname", "contact", "primarycontact"),
            ["contactEmail"] = Pick(normalized, "contactemail", "email", "emailaddress"),
            ["contactPhone"] = Pick(normalized, "contactphone", "phone", "phonenumber")
        };
    }

    static string Pick(IReadOnlyDictionary<string, string> headers, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            string normalizedCandidate = NormalizeHeader(candidate);
            if (headers.TryGetValue(normalizedCandidate, out string header))
                return header;
        }

        return string.Empty;
    }

    static string NormalizeHeader(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    static ImportStatusResponse ToStatus(Import import) =>
        new()
        {
            Id = import.Id,
            JobStatus = import.JobStatus.ToString(),
            UploadStatus = import.UploadStatus.ToString(),
            ProcessingStatus = import.ProcessingStatus.ToString(),
            UploadedBytes = import.UploadedBytes,
            SizeBytes = import.SizeBytes,
            TotalRowCount = import.TotalRowCount,
            ImportedRowCount = import.ImportedRowCount,
            WarningCount = import.WarningCount,
            ErrorCount = import.ErrorCount,
            WarningSummary = import.WarningSummary ?? string.Empty,
            ErrorSummary = import.ErrorSummary ?? string.Empty,
            MappingSnapshotJson = import.MappingSnapshotJson ?? string.Empty
        };
}
