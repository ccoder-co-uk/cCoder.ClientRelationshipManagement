using System.Security;
using cCoder.Security.Objects;
using cCoder.Security.Objects.DTOs;
using cCoder.Security.Services.Orchestrations.Interfaces;
using ClientRelationshipManagement.Web.Models.Account;
using Microsoft.AspNetCore.Mvc;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class AccountController(
    IAuthenticationOrchestrationService accountManager,
    ISSOAuthInfo authInfo)
    : Controller
{
    [HttpGet("/Account/Login")]
    public IActionResult Login(string returnUrl = null)
    {
        if (IsAuthenticated())
            return RedirectToLocal(returnUrl);

        return View(new LoginViewModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl),
            ErrorMessage = TempData["AccountError"]?.ToString(),
        });
    }

    [HttpPost("/Account/Login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        string returnUrl = NormalizeReturnUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
        {
            model.ReturnUrl = returnUrl;
            return View(model);
        }

        try
        {
            await accountManager.LoginAsync(model.User, model.Pass);
            return RedirectToLocal(returnUrl);
        }
        catch (SecurityException)
        {
            model.ReturnUrl = returnUrl;
            model.ErrorMessage = "Access denied. Please check your username and password.";
            return View(model);
        }
    }

    [HttpPost("/Account/Logout")]
    public async Task<IActionResult> Logout(string returnUrl = null)
    {
        await accountManager.LogoutAsync();
        return RedirectToAction(nameof(Login), new { returnUrl = NormalizeReturnUrl(returnUrl) });
    }

    bool IsAuthenticated() =>
        !string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
        && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase);

    IActionResult RedirectToLocal(string returnUrl) =>
        Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");

    static string NormalizeReturnUrl(string returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl)
            ? "/"
            : returnUrl;
}
