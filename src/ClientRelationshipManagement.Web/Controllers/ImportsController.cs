using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Models.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ClientRelationshipManagement.Web.Controllers;
[Route("Admin/Imports")]
public sealed class ImportsController(IImportCoordinationService importService, IOptions<ImportWorkflowOptions> options, ICRMAuthInfo authInfo, ILoggingBroker<ImportsController> loggingBroker) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            IActionResult result = await ExecuteActionAsync();
            LogActionResult(result);
            return result;
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(exception.Message);
            return StatusCode(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(exception.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "The request is invalid.");
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(exception.Message);
            return StatusCode(StatusCodes.Status401Unauthorized, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(exception.Message);
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(exception.Message);
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(exception, "Controller request failed.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        async Task<IActionResult> ExecuteActionAsync()
        {
            if (RedirectIfUnauthenticated()is IActionResult redirect)
                return redirect;
            return View(new ImportListPageViewModel { Notice = TempData["ImportsNotice"]?.ToString() ?? string.Empty, HostedServicesBaseUrl = options.Value.HostedServicesBaseUrl, ChunkSizeBytes = options.Value.ChunkSizeBytes, Sources = await importService.RetrieveAllSources().AsNoTracking().OrderBy(item => item.Name).Select(item => new SourceOptionViewModel { Id = item.Id, Name = item.Name, CountryCode = item.CountryCode ?? string.Empty, IsAuthoritative = item.IsAuthoritative }).ToListAsync(), Imports = await importService.RetrieveAllImports().AsNoTracking().Include(item => item.Source).OrderByDescending(item => item.CreatedOn).Take(100).Select(item => new ImportListItemViewModel { Id = item.Id, SourceName = item.Source.Name, FileName = item.OriginalFileName, JobStatus = item.JobStatus.ToString(), UploadStatus = item.UploadStatus.ToString(), ProcessingStatus = item.ProcessingStatus.ToString(), SizeBytes = item.SizeBytes, UploadedBytes = item.UploadedBytes, TotalRowCount = item.TotalRowCount, ImportedRowCount = item.ImportedRowCount, WarningCount = item.WarningCount, ErrorCount = item.ErrorCount, WarningSummary = item.WarningSummary ?? string.Empty, ErrorSummary = item.ErrorSummary ?? string.Empty, MappingSnapshotJson = item.MappingSnapshotJson ?? string.Empty, CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm") }).ToListAsync() });
        }
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(authInfo?.SSOUserId) && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;
        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    private void LogActionResult(IActionResult result)
    {
        int statusCode = result is IStatusCodeActionResult response ? response.StatusCode ?? StatusCodes.Status200OK : result is RedirectResult or RedirectToActionResult ? StatusCodes.Status302Found : StatusCodes.Status200OK;
        if (statusCode >= 200 && statusCode < 400)
            loggingBroker.LogInformation("Controller action completed successfully.");
        else if (statusCode >= 400 && statusCode < 500)
            loggingBroker.LogWarning("Controller action returned {StatusCode}.", statusCode);
    }
}