using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Controllers;

[Route("Admin/Imports")]
public sealed class ImportsController(
    IImportCoordinationService importService,
    IOptions<ImportWorkflowOptions> options,
    ICRMAuthInfo authInfo)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        return View(new ImportListPageViewModel
        {
            Notice = TempData["ImportsNotice"]?.ToString() ?? string.Empty,
            HostedServicesBaseUrl = options.Value.HostedServicesBaseUrl,
            ChunkSizeBytes = options.Value.ChunkSizeBytes,
            Sources = await importService.RetrieveAllSources()
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new SourceOptionViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    CountryCode = item.CountryCode ?? string.Empty,
                    IsAuthoritative = item.IsAuthoritative
                })
                .ToListAsync(),
            Imports = await importService.RetrieveAllImports()
                .AsNoTracking()
                .Include(item => item.Source)
                .OrderByDescending(item => item.CreatedOn)
                .Take(100)
                .Select(item => new ImportListItemViewModel
                {
                    Id = item.Id,
                    SourceName = item.Source.Name,
                    FileName = item.OriginalFileName,
                    JobStatus = item.JobStatus.ToString(),
                    UploadStatus = item.UploadStatus.ToString(),
                    ProcessingStatus = item.ProcessingStatus.ToString(),
                    SizeBytes = item.SizeBytes,
                    UploadedBytes = item.UploadedBytes,
                    TotalRowCount = item.TotalRowCount,
                    ImportedRowCount = item.ImportedRowCount,
                    WarningCount = item.WarningCount,
                    ErrorCount = item.ErrorCount,
                    WarningSummary = item.WarningSummary ?? string.Empty,
                    ErrorSummary = item.ErrorSummary ?? string.Empty,
                    MappingSnapshotJson = item.MappingSnapshotJson ?? string.Empty,
                    CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
                })
                .ToListAsync()
        });
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }
}
