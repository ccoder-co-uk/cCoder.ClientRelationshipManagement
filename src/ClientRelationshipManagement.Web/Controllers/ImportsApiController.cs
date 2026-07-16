using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
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
    IImportCoordinationService importService,
    IHostedImportClient hostedImportClient,
    ICRMAuthInfo authInfo)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        List<ImportStatusResponse> imports = await importService.RetrieveAllImports()
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

        Import import = await importService.RetrieveAllImports().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return import is null ? NotFound() : Ok(ToStatus(import));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateImportRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("A file name is required.");

        Import import = await importService.CreateAsync(new CreateImportCommand(
            request.SourceId, request.SourceName, request.SourceType, request.CountryCode,
            request.IsAuthoritative, request.SourceNotes, request.FileName, request.ContentType,
            request.SizeBytes, request.UserInstructions), cancellationToken);

        return Created($"/Api/Imports/{import.Id}", ToStatus(import));
    }

    [HttpPost("{id:guid}/upload-session")]
    public async Task<IActionResult> CreateUploadSession(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        Import import = await importService.RetrieveAllImports().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
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

        Import import = await importService.SaveMappingAsync(id, request.MappingSnapshotJson, request.UserInstructions, cancellationToken);
        if (import is null)
            return NotFound();

        return Ok(ToStatus(import));
    }

    [HttpPost("{id:guid}/mark-ready")]
    public async Task<IActionResult> MarkReady(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        try { Import import = await importService.MarkReadyAsync(id, cancellationToken); return import is null ? NotFound() : Ok(ToStatus(import)); }
        catch (InvalidOperationException exception) { return BadRequest(exception.Message); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        try { Import import = await importService.CancelAsync(id, cancellationToken); return import is null ? NotFound() : Ok(ToStatus(import)); }
        catch (InvalidOperationException exception) { return BadRequest(exception.Message); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated())
            return Unauthorized();

        Import import = await importService.RetrieveAllImports().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
            return NotFound();
        if (import.JobStatus == ImportJobStatus.Processing)
            return BadRequest("An active import cannot be deleted.");

        try
        {
            await hostedImportClient.DeleteFilesAsync(id, cancellationToken);
            return await importService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception) { return BadRequest(exception.Message); }
    }

    bool IsAuthenticated() =>
        !string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
        && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase);

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
