using System.Security;
using cCoder.Security.Models;
using cCoder.Security.Models.Configurations;
using cCoder.Security.Models.DTOs;
using cCoder.Security.Exposures;
using ClientRelationshipManagement.Web.Models.Account;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ClientRelationshipManagement.Web.Controllers;
public sealed class AccountController(IAuthenticationManager accountManager, ISSOAuthInfo authInfo, ILoggingBroker<AccountController> loggingBroker) : Controller
{
    [HttpGet("/Account/Login")]
    public IActionResult Login(string returnUrl = null)
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
            if (IsAuthenticated())
                return RedirectToLocal(returnUrl);
            return View(new LoginViewModel { ReturnUrl = NormalizeReturnUrl(returnUrl), ErrorMessage = TempData["AccountError"]?.ToString(), });
        }
    }

    [HttpPost("/Account/Login")]
    public async Task<IActionResult> Login(LoginViewModel model)
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
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }
    }

    [HttpPost("/Account/Logout")]
    public async Task<IActionResult> Logout(string returnUrl = null)
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
            await accountManager.LogoutAsync();
            return RedirectToAction(nameof(Login), new { returnUrl = NormalizeReturnUrl(returnUrl) });
        }
    }

    bool IsAuthenticated() => !string.IsNullOrWhiteSpace(authInfo?.SSOUserId) && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase);
    IActionResult RedirectToLocal(string returnUrl) => Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    static string NormalizeReturnUrl(string returnUrl) => string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    private void LogActionResult(IActionResult result)
    {
        int statusCode = result is IStatusCodeActionResult response ? response.StatusCode ?? StatusCodes.Status200OK : result is RedirectResult or RedirectToActionResult ? StatusCodes.Status302Found : StatusCodes.Status200OK;
        if (statusCode >= 200 && statusCode < 400)
            loggingBroker.LogInformation("Controller action completed successfully.");
        else if (statusCode >= 400 && statusCode < 500)
            loggingBroker.LogWarning("Controller action returned {StatusCode}.", statusCode);
    }
}