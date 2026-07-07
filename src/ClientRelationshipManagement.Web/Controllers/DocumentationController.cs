using ClientRelationshipManagement.Web.Documentation;
using ClientRelationshipManagement.Web.Models.Documentation;
using Microsoft.AspNetCore.Mvc;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class DocumentationController : Controller
{
    [Route("Documentation/{*slug}")]
    public IActionResult Index(string slug)
    {
        DocumentationPageDefinition page = DocumentationCatalog.GetPage(slug);

        DocumentationPageViewModel model = new()
        {
            Title = page.Title,
            Eyebrow = page.Eyebrow,
            Lead = page.Lead,
            CurrentSlug = page.Slug,
            Sections = page.Sections,
            Navigation = DocumentationCatalog.Root.Children
                .Select(item => BuildNavigation(item, page.Slug))
                .ToList()
        };

        return View(model);
    }

    static DocumentationNavItemViewModel BuildNavigation(
        DocumentationPageDefinition page,
        string currentSlug)
    {
        List<DocumentationNavItemViewModel> children = page.Children
            .Select(child => BuildNavigation(child, currentSlug))
            .ToList();

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
}
