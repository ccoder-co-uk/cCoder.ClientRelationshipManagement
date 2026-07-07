using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientHandoffPackController(IClientHandoffPackOrchestrationService clientHandoffPackService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientHandoffPackService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        ClientHandoffPack clientHandoffPack = clientHandoffPackService.Get(id);
        return clientHandoffPack is null ? NotFound() : Ok(clientHandoffPack);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] ClientHandoffPack clientHandoffPack) =>
        Ok(await clientHandoffPackService.AddAsync(clientHandoffPack));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] ClientHandoffPack clientHandoffPack)
    {
        clientHandoffPack.Id = id;
        return Ok(await clientHandoffPackService.UpdateAsync(clientHandoffPack));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientHandoffPackService.DeleteAsync(id);
        return NoContent();
    }
}
