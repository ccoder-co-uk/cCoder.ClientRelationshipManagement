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
public sealed class ProcessDefinitionsController(
    IProcessDefinitionOrchestrationService orchestration,
    ILoggingBroker<ProcessDefinitionsController> loggingBroker)
    : ODataController
{
    static readonly HashSet<string> ServerManaged = new(
        comparer: StringComparer.OrdinalIgnoreCase)
    {
        "Id", "TenantId", "UserId", "CreatedOn", "CreatedBy",
        "LastUpdated", "LastUpdatedBy"
    };

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get()
    {
        try
        {
            IActionResult response = Ok(value: orchestration.RetrieveAll());
            loggingBroker.LogInformation(
                message: "Process definitions were retrieved successfully.");

            return response;
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get([FromRoute] Guid key)
    {
        try
        {
            IActionResult response = Ok(value: SingleResult.Create(
                queryable: orchestration
                    .RetrieveAll()
                    .Where(predicate: item => item.Id == key)));
            loggingBroker.LogInformation(
                message: "Process definition {ProcessDefinitionId} was retrieved successfully.",
                args: key);

            return response;
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

    public async Task<IActionResult> Post(
        [FromBody] ProcessDefinition entity,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                loggingBroker.LogWarning(
                    message: "Process definition creation was rejected because the model is invalid.");

                return BadRequest(modelState: ModelState);
            }

            ProcessDefinition created = await orchestration.AddAsync(
                entity: entity,
                cancellationToken: cancellationToken);
            loggingBroker.LogInformation(
                message: "Process definition {ProcessDefinitionId} was created successfully.",
                args: created.Id);

            return Created(entity: created);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

    public async Task<IActionResult> Put(
        [FromRoute] Guid key,
        [FromBody] ProcessDefinition entity,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} update was rejected because the model is invalid.",
                    args: key);

                return BadRequest(modelState: ModelState);
            }

            bool exists = await orchestration
                .RetrieveWriteable()
                .AnyAsync(
                    predicate: item => item.Id == key,
                    cancellationToken: cancellationToken);

            if (!exists)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} could not be updated because it was not found.",
                    args: key);

                return NotFound();
            }

            entity.Id = key;
            ProcessDefinition updated = await orchestration.ModifyAsync(
                entity: entity,
                cancellationToken: cancellationToken);
            loggingBroker.LogInformation(
                message: "Process definition {ProcessDefinitionId} was updated successfully.",
                args: key);

            return Updated(entity: updated);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        Delta<ProcessDefinition> delta,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} patch was rejected because the model is invalid.",
                    args: key);

                return BadRequest(modelState: ModelState);
            }

            ProcessDefinition entity = await orchestration
                .RetrieveWriteable()
                .SingleOrDefaultAsync(
                    predicate: item => item.Id == key,
                    cancellationToken: cancellationToken);

            if (entity is null)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} could not be patched because it was not found.",
                    args: key);

                return NotFound();
            }

            string protectedProperty = delta
                .GetChangedPropertyNames()
                .FirstOrDefault(predicate: ServerManaged.Contains);

            if (protectedProperty is not null)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} patch attempted to change server-managed property {PropertyName}.",
                    args: [key, protectedProperty]);

                return BadRequest(
                    error: $"Property '{protectedProperty}' is server-managed.");
            }

            delta.Patch(entity);
            entity.Id = key;
            ProcessDefinition updated = await orchestration.ModifyAsync(
                entity: entity,
                cancellationToken: cancellationToken);
            loggingBroker.LogInformation(
                message: "Process definition {ProcessDefinitionId} was patched successfully.",
                args: key);

            return Updated(entity: updated);
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessDefinition entity = await orchestration
                .RetrieveWriteable()
                .SingleOrDefaultAsync(
                    predicate: item => item.Id == key,
                    cancellationToken: cancellationToken);

            if (entity is null)
            {
                loggingBroker.LogWarning(
                    message: "Process definition {ProcessDefinitionId} could not be deleted because it was not found.",
                    args: key);

                return NotFound();
            }

            await orchestration.RemoveAsync(
                entity: entity,
                cancellationToken: cancellationToken);
            loggingBroker.LogInformation(
                message: "Process definition {ProcessDefinitionId} was deleted successfully.",
                args: key);

            return NoContent();
        }
        catch (ValidationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (ArgumentException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was rejected: {FailureReason}",
                args: exception.Message);

            return BadRequest(error: exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AuthenticationException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was unauthenticated: {FailureReason}",
                args: exception.Message);

            return Unauthorized();
        }
        catch (SecurityException exception)
        {
            loggingBroker.LogWarning(
                message: "Process definition request was forbidden: {FailureReason}",
                args: exception.Message);

            return StatusCode(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            loggingBroker.LogError(
                exception: exception,
                message: "Process definition request failed.");

            return StatusCode(
                statusCode: StatusCodes.Status500InternalServerError,
                value: "The process definition operation failed.");
        }
    }

}
