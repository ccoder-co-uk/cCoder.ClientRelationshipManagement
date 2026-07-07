using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Models.Imports;
using ClientRelationshipManagement.Web.Services.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Controllers;

[ApiController]
[Route("Api/Imports")]
public sealed class ImportsApiController(
    IPlatformDbContextFactory dbContextFactory,
    IHostedImportClient hostedImportClient,
    ICRMAuthInfo authInfo)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        List<ImportStatusResponse> imports = await context.Imports
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => ToStatus(item))
            .ToListAsync(cancellationToken);

        return Ok(imports);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        Import import = await context.Imports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return import is null ? NotFound() : Ok(ToStatus(import));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateImportRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("A file name is required.");

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Source source = await ResolveSourceAsync(context, request, now, cancellationToken);
        Import import = new()
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            OriginalFileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            JobStatus = ImportJobStatus.Draft,
            UploadStatus = ImportUploadStatus.NotStarted,
            ProcessingStatus = ImportProcessingStatus.NotReady,
            UserInstructions = Normalize(request.UserInstructions),
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        context.Imports.Add(import);
        await context.SaveChangesAsync(cancellationToken);

        return Created($"/Api/Imports/{import.Id}", ToStatus(import));
    }

    [HttpPost("{id:guid}/upload-session")]
    public async Task<IActionResult> CreateUploadSession(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        Import import = await context.Imports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();

        ImportUploadSessionResponse response = await hostedImportClient.CreateUploadSessionAsync(
            new HostedImportUploadSessionRequest
            {
                ImportId = import.Id,
                FileName = import.OriginalFileName,
                ContentType = import.ContentType,
                SizeBytes = import.SizeBytes
            },
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/analyse")]
    public async Task<IActionResult> Analyse(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        ImportStatusResponse response = await hostedImportClient.AnalyseAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/mapping")]
    public async Task<IActionResult> SaveMapping(
        Guid id,
        SaveImportMappingRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();

        import.MappingSnapshotJson = request.MappingSnapshotJson;
        import.UserInstructions = Normalize(request.UserInstructions);
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = CurrentUserId;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToStatus(import));
    }

    [HttpPost("{id:guid}/mark-ready")]
    public async Task<IActionResult> MarkReady(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();

        if (import.UploadStatus != ImportUploadStatus.Uploaded)
            return BadRequest("Upload must be completed before the import can be marked ready.");

        if (string.IsNullOrWhiteSpace(import.MappingSnapshotJson))
            return BadRequest("Mapping must be reviewed before the import can be marked ready.");

        import.JobStatus = ImportJobStatus.Ready;
        import.ProcessingStatus = ImportProcessingStatus.Ready;
        import.MarkedReadyOn = DateTimeOffset.UtcNow;
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = CurrentUserId;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToStatus(import));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();

        if (import.JobStatus == ImportJobStatus.Processing)
            return BadRequest("An active import cannot be cancelled.");

        import.JobStatus = ImportJobStatus.Cancelled;
        import.ProcessingStatus = ImportProcessingStatus.Cancelled;
        import.LastUpdated = DateTimeOffset.UtcNow;
        import.LastUpdatedBy = CurrentUserId;
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToStatus(import));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        Import import = await context.Imports.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();

        if (import.JobStatus == ImportJobStatus.Processing)
            return BadRequest("An active import cannot be deleted.");

        await hostedImportClient.DeleteFilesAsync(id, cancellationToken);

        ImportLink[] links = await context.ImportLinks.Where(item => item.ImportId == id).ToArrayAsync(cancellationToken);
        context.ImportLinks.RemoveRange(links);
        context.Imports.Remove(import);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    async ValueTask<Source> ResolveSourceAsync(
        PlatformDbContext context,
        CreateImportRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.SourceId.HasValue)
        {
            Source existing = await context.Sources.FirstOrDefaultAsync(item => item.Id == request.SourceId.Value, cancellationToken);
            if (existing is not null)
                return existing;
        }

        string sourceName = string.IsNullOrWhiteSpace(request.SourceName)
            ? "Unspecified source"
            : request.SourceName.Trim();

        string countryCode = string.IsNullOrWhiteSpace(request.CountryCode)
            ? "GB"
            : request.CountryCode.Trim().ToUpperInvariant();

        Source source = await context.Sources.FirstOrDefaultAsync(item =>
            item.Name == sourceName && item.CountryCode == countryCode,
            cancellationToken);

        if (source is not null)
            return source;

        source = new Source
        {
            Id = Guid.NewGuid(),
            Name = sourceName,
            SourceType = request.SourceType,
            CountryCode = countryCode,
            IsAuthoritative = request.IsAuthoritative || request.SourceType == SourceType.Authority,
            Notes = Normalize(request.SourceNotes),
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        context.Sources.Add(source);
        return source;
    }

    bool IsAuthenticated() =>
        !string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
        && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase);

    string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            ? "system"
            : authInfo.SSOUserId;

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
