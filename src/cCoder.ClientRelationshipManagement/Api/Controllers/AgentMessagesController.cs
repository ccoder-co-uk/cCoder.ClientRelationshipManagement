using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;
[CrmUserAuthorize]
public sealed class AgentMessagesController(IAgentMessageOrchestrationService orchestration, ILoggingBroker<AgentMessagesController> loggingBroker) : ODataController
{
    [EnableQuery(MaxExpansionDepth = 6)]
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

    [EnableQuery(MaxExpansionDepth = 6)]
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
            return Ok(SingleResult.Create(orchestration.RetrieveAll().Where(message => message.Id == key)));
        }
    }

    public async Task<IActionResult> Post([FromBody] AgentMessage message, CancellationToken cancellationToken)
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
            return Created(await orchestration.AddAsync(message, cancellationToken));
        }
    }

    public async Task<IActionResult> Put([FromRoute] Guid key, [FromBody] AgentMessage message, CancellationToken cancellationToken)
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
            message.Id = key;
            return Updated(await orchestration.ModifyAsync(message, cancellationToken));
        }
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch([FromRoute] Guid key, Delta<AgentMessage> delta, CancellationToken cancellationToken)
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
            AgentMessage message = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
            if (message is null)
                return NotFound();
            delta.Patch(message);
            message.Id = key;
            return Updated(await orchestration.ModifyAsync(message, cancellationToken));
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
            AgentMessage message = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
            if (message is null)
                return NotFound();
            await orchestration.RemoveAsync(message, cancellationToken);
            return NoContent();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reply([FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
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
            string body = parameters["body"] as string;
            return Ok(await orchestration.AppendEntryAsync(key, "User", body, cancellationToken));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Respond([FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
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
            AgentMessageState state = (AgentMessageState)parameters["state"];
            string responseNotes = parameters["responseNotes"] as string;
            return Ok(await orchestration.RespondAsync(key, state, responseNotes, cancellationToken));
        }
    }

    [HttpPost]
    public async Task<IActionResult> ChangeState([FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
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
            AgentMessageState state = (AgentMessageState)parameters["state"];
            string auditNote = parameters["auditNote"] as string;
            return Ok(await orchestration.ChangeStateAsync(key, state, auditNote, cancellationToken));
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