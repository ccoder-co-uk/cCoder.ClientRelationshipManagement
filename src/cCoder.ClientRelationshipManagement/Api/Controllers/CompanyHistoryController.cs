using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[CrmUserAuthorize]
public sealed class CompanyHistoryController(ICompanyHistoryItemOrchestrationService orchestration) : ODataController
{
    static readonly HashSet<string> ServerManaged = new(StringComparer.OrdinalIgnoreCase)
        { "Id", "TenantId", "UserId", "CreatedOn", "CreatedBy", "LastUpdated", "LastUpdatedBy" };

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get() => Ok(orchestration.RetrieveAll());

    [EnableQuery(MaxExpansionDepth = 8)]
    public IActionResult Get([FromRoute] Guid key) =>
        Ok(SingleResult.Create(orchestration.RetrieveAll().Where(item => item.Id == key)));

    public async Task<IActionResult> Post([FromBody] CompanyHistoryItem entity, CancellationToken cancellationToken) =>
        Created(await orchestration.AddAsync(entity, cancellationToken));

    public async Task<IActionResult> Put([FromRoute] Guid key, [FromBody] CompanyHistoryItem entity, CancellationToken cancellationToken)
    {
        if (!await orchestration.RetrieveWriteable().AnyAsync(item => item.Id == key, cancellationToken)) return NotFound();
        entity.Id = key;
        return Updated(await orchestration.ModifyAsync(entity, cancellationToken));
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch([FromRoute] Guid key, Delta<CompanyHistoryItem> delta, CancellationToken cancellationToken)
    {
        CompanyHistoryItem entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (entity is null) return NotFound();
        string protectedProperty = delta.GetChangedPropertyNames().FirstOrDefault(ServerManaged.Contains);
        if (protectedProperty is not null) return BadRequest($"Property '{protectedProperty}' is server-managed.");
        delta.Patch(entity);
        entity.Id = key;
        return Updated(await orchestration.ModifyAsync(entity, cancellationToken));
    }

    public async Task<IActionResult> Delete([FromRoute] Guid key, CancellationToken cancellationToken)
    {
        CompanyHistoryItem entity = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (entity is null) return NotFound();
        await orchestration.RemoveAsync(entity, cancellationToken);
        return NoContent();
    }
}