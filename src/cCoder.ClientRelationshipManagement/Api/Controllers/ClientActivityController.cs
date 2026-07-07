using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientActivityController(IClientActivityOrchestrationService clientActivityService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientActivityService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        ClientActivity clientActivity = clientActivityService.Get(id);
        return clientActivity is null ? NotFound() : Ok(clientActivity);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] ClientActivity clientActivity) =>
        Ok(await clientActivityService.AddAsync(clientActivity));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] ClientActivity clientActivity)
    {
        clientActivity.Id = id;
        return Ok(await clientActivityService.UpdateAsync(clientActivity));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientActivityService.DeleteAsync(id);
        return NoContent();
    }
}
