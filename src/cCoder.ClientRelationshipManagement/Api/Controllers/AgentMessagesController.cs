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

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[CrmUserAuthorize]
public sealed class AgentMessagesController(
    IAgentMessageOrchestrationService orchestration) : ODataController
{
    [EnableQuery(MaxExpansionDepth = 6)]
    public IActionResult Get() => Ok(orchestration.RetrieveAll());

    [EnableQuery(MaxExpansionDepth = 6)]
    public IActionResult Get([FromRoute] Guid key) =>
        Ok(SingleResult.Create(orchestration.RetrieveAll().Where(message => message.Id == key)));

    public async Task<IActionResult> Post([FromBody] AgentMessage message, CancellationToken cancellationToken) =>
        Created(await orchestration.AddAsync(message, cancellationToken));

    public async Task<IActionResult> Put([FromRoute] Guid key, [FromBody] AgentMessage message, CancellationToken cancellationToken)
    {
        message.Id = key;
        return Updated(await orchestration.ModifyAsync(message, cancellationToken));
    }

    [AcceptVerbs("PATCH", "MERGE")]
    public async Task<IActionResult> Patch([FromRoute] Guid key, Delta<AgentMessage> delta, CancellationToken cancellationToken)
    {
        AgentMessage message = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (message is null) return NotFound();
        delta.Patch(message);
        message.Id = key;
        return Updated(await orchestration.ModifyAsync(message, cancellationToken));
    }

    public async Task<IActionResult> Delete([FromRoute] Guid key, CancellationToken cancellationToken)
    {
        AgentMessage message = await orchestration.RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (message is null) return NotFound();
        await orchestration.RemoveAsync(message, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Reply(
        [FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
    {
        string body = parameters["body"] as string;
        return Ok(await orchestration.AppendEntryAsync(key, "User", body, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Respond(
        [FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
    {
        AgentMessageState state = (AgentMessageState)parameters["state"];
        string responseNotes = parameters["responseNotes"] as string;
        return Ok(await orchestration.RespondAsync(key, state, responseNotes, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> ChangeState(
        [FromRoute] Guid key, ODataActionParameters parameters, CancellationToken cancellationToken)
    {
        AgentMessageState state = (AgentMessageState)parameters["state"];
        string auditNote = parameters["auditNote"] as string;
        return Ok(await orchestration.ChangeStateAsync(key, state, auditNote, cancellationToken));
    }
}
