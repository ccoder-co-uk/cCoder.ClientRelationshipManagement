// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[CrmUserAuthorize]
public sealed class ClientAccountsController(
    IClientAccountOrchestrationService orchestration,
    ILoggingBroker<ClientAccountsController> loggingBroker)
    : CrmODataController<ClientAccountsController>(loggingBroker)
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
            return Complete(response: Ok(value: orchestration.RetrieveAll()));
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get([FromRoute] Guid key)
    {
        try
        {
            return Complete(response: Ok(value: SingleResult.Create(
                queryable: orchestration.RetrieveAll().Where(predicate: item => item.Id == key))));
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }

    public async Task<IActionResult> Post(
        [FromBody] ClientAccount entity,
        CancellationToken cancellationToken)
    {
        try
        {
            ClientAccount created = await orchestration.AddAsync(
                entity: entity,
                cancellationToken: cancellationToken);

            return Complete(response: Created(entity: created));
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }

    public async Task<IActionResult> Put(
        [FromRoute] Guid key,
        [FromBody] ClientAccount entity,
        CancellationToken cancellationToken)
    {
        try
        {
            bool exists = await orchestration.RetrieveWriteable().AnyAsync(
                predicate: item => item.Id == key,
                cancellationToken: cancellationToken);

            if (!exists)
                return Complete(response: NotFound());

            entity.Id = key;
            ClientAccount updated = await orchestration.ModifyAsync(
                entity: entity,
                cancellationToken: cancellationToken);

            return Complete(response: Updated(entity: updated));
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        Delta<ClientAccount> delta,
        CancellationToken cancellationToken)
    {
        try
        {
            ClientAccount entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(
                predicate: item => item.Id == key,
                cancellationToken: cancellationToken);

            if (entity is null)
                return Complete(response: NotFound());

            string protectedProperty = delta.GetChangedPropertyNames().FirstOrDefault(
                predicate: ServerManaged.Contains);

            if (protectedProperty is not null)
            {
                return Complete(response: BadRequest(
                    error: $"Property '{protectedProperty}' is server-managed."));
            }

            delta.Patch(entity);
            entity.Id = key;
            ClientAccount updated = await orchestration.ModifyAsync(
                entity: entity,
                cancellationToken: cancellationToken);

            return Complete(response: Updated(entity: updated));
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }

    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        try
        {
            ClientAccount entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(
                predicate: item => item.Id == key,
                cancellationToken: cancellationToken);

            if (entity is null)
                return Complete(response: NotFound());

            await orchestration.RemoveAsync(
                entity: entity,
                cancellationToken: cancellationToken);

            return Complete(response: NoContent());
        }
        catch (ValidationException exception) { return Reject(exception: exception); }
        catch (ArgumentException exception) { return RejectArgument(exception: exception); }
        catch (AuthenticationException exception) { return RequireAuthentication(exception: exception); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception: exception); }
        catch (SecurityException exception) { return Forbid(exception: exception); }
        catch (Exception exception) { return Fail(exception: exception); }
    }
}