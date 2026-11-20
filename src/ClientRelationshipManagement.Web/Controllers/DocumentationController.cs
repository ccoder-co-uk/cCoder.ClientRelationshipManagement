using ClientRelationshipManagement.Web.Documentation;
using ClientRelationshipManagement.Web.Models.Documentation;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ClientRelationshipManagement.Web.Controllers;
public sealed class DocumentationController(ILoggingBroker<DocumentationController> loggingBroker) : Controller
{
    [Route("Documentation/{*slug}")]
    public IActionResult Index(string slug)
    {
        try
        {
            IActionResult result = ExecuteAction();
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

        IActionResult ExecuteAction()
        {
            DocumentationPageDefinition page = DocumentationCatalog.GetPage(slug);
            DocumentationPageViewModel model = new()
            {
                Title = page.Title,
                Eyebrow = page.Eyebrow,
                Lead = page.Lead,
                CurrentSlug = page.Slug,
                Sections = page.Sections,
                Navigation = DocumentationCatalog.Root.Children.Select(item => BuildNavigation(item, page.Slug)).ToList()
            };
            return View(model);
        }
    }

    static DocumentationNavItemViewModel BuildNavigation(DocumentationPageDefinition page, string currentSlug)
    {
        List<DocumentationNavItemViewModel> children = page.Children.Select(child => BuildNavigation(child, currentSlug)).ToList();
        bool isCurrent = string.Equals(page.Slug, currentSlug, StringComparison.OrdinalIgnoreCase);
        bool isAncestor = children.Any(child => child.IsCurrentOrAncestor);
        return new DocumentationNavItemViewModel
        {
            Title = page.Title,
            Url = page.Slug.Length == 0 ? "/Documentation" : $"/Documentation/{page.Slug}",
            IsCurrent = isCurrent,
            IsCurrentOrAncestor = isCurrent || isAncestor,
            Children = children
        };
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