using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;
[CrmUserAuthorize]
public sealed class MaterialsController(IMaterialOrchestrationService orchestration, ILoggingBroker<MaterialsController> loggingBroker) : ODataController
{
    static readonly HashSet<string> ServerManaged = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "TenantId",
        "UserId",
        "CreatedOn",
        "CreatedBy",
        "LastUpdated",
        "LastUpdatedBy"
    };
    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get()
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
            return Ok(orchestration.RetrieveAll());
        }
    }

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get([FromRoute] Guid key)
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
            return Ok(SingleResult.Create(orchestration.RetrieveAll().Where(item => item.Id == key)));
        }
    }

    public async Task<IActionResult> Post([FromBody] Material entity, CancellationToken cancellationToken)
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
            return Created(await orchestration.AddAsync(entity, cancellationToken));
        }
    }

    public async Task<IActionResult> Put([FromRoute] Guid key, [FromBody] Material entity, CancellationToken cancellationToken)
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
            if (!await orchestration.RetrieveWriteable().AnyAsync(item => item.Id == key, cancellationToken))
                return NotFound();
            entity.Id = key;
            return Updated(await orchestration.ModifyAsync(entity, cancellationToken));
        }
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch([FromRoute] Guid key, Delta<Material> delta, CancellationToken cancellationToken)
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
            Material entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
            if (entity is null)
                return NotFound();
            string protectedProperty = delta.GetChangedPropertyNames().FirstOrDefault(ServerManaged.Contains);
            if (protectedProperty is not null)
                return BadRequest($"Property '{protectedProperty}' is server-managed.");
            delta.Patch(entity);
            entity.Id = key;
            return Updated(await orchestration.ModifyAsync(entity, cancellationToken));
        }
    }

    public async Task<IActionResult> Delete([FromRoute] Guid key, CancellationToken cancellationToken)
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
            Material entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
            if (entity is null)
                return NotFound();
            await orchestration.RemoveAsync(entity, cancellationToken);
            return NoContent();
        }
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