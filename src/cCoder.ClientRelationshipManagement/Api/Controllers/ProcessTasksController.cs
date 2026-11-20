// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[CrmUserAuthorize]
public sealed class ProcessTasksController(
    IProcessTaskOrchestrationService orchestration,
    ILoggingBroker<ProcessTasksController> loggingBroker)
    : ODataController
{
    static readonly HashSet<string> ServerManaged = new(
        comparer: StringComparer.OrdinalIgnoreCase)
    {
        "Id", "TenantId", "UserId", "CreatedOn", "CreatedBy", "LastUpdated", "LastUpdatedBy"
    };

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get()
    {
        try
        {
            IActionResult response = Ok(value: orchestration.RetrieveAll());
            LogSuccess();
            return response;
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get([FromRoute] Guid key)
    {
        try
        {
            IActionResult response = Ok(value: SingleResult.Create(
                queryable: orchestration.RetrieveAll().Where(item => item.Id == key)));
            LogSuccess();
            return response;
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<IActionResult> Post(
        [FromBody] ProcessTask entity,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessTask created = await orchestration.AddAsync(entity, cancellationToken);
            LogSuccess();
            return Created(entity: created);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<IActionResult> Put(
        [FromRoute] Guid key,
        [FromBody] ProcessTask entity,
        CancellationToken cancellationToken)
    {
        try
        {
            bool exists = await orchestration.RetrieveWriteable()
                .AnyAsync(item => item.Id == key, cancellationToken);

            if (!exists)
            {
                LogDirectRejection("The requested resource was not found.");
                return NotFound();
            }

            entity.Id = key;
            ProcessTask updated = await orchestration.ModifyAsync(entity, cancellationToken);
            LogSuccess();
            return Updated(entity: updated);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        Delta<ProcessTask> delta,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessTask entity = await orchestration.RetrieveWriteable()
                .SingleOrDefaultAsync(item => item.Id == key, cancellationToken);

            if (entity is null)
            {
                LogDirectRejection("The requested resource was not found.");
                return NotFound();
            }

            string protectedProperty = delta.GetChangedPropertyNames()
                .FirstOrDefault(ServerManaged.Contains);

            if (protectedProperty is not null)
            {
                LogDirectRejection("A server-managed property cannot be changed.");
                return BadRequest(error: $"Property '{protectedProperty}' is server-managed.");
            }

            delta.Patch(entity);
            entity.Id = key;
            ProcessTask updated = await orchestration.ModifyAsync(entity, cancellationToken);
            LogSuccess();
            return Updated(entity: updated);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessTask entity = await orchestration.RetrieveWriteable()
                .SingleOrDefaultAsync(item => item.Id == key, cancellationToken);

            if (entity is null)
            {
                LogDirectRejection("The requested resource was not found.");
                return NotFound();
            }

            await orchestration.RemoveAsync(entity, cancellationToken);
            LogSuccess();
            return NoContent();
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was rejected: {FailureReason}",
                args: exception.Message);
            return BadRequest(error: exception.Message);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was unauthenticated: {FailureReason}",
                args: exception.Message);
            return Unauthorized(value: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Controller request was forbidden: {FailureReason}",
                args: exception.Message);
            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Controller request failed.");
            return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    void LogSuccess() =>
        loggingBroker.LogInformation(message: "Controller request completed successfully.");

    void LogDirectRejection(string reason) =>
        loggingBroker.LogWarning(message: "Controller request was rejected: {FailureReason}", args: reason);
}