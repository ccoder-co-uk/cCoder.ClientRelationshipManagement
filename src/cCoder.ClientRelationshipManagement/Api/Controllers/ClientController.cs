using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientController(IClientOrchestrationService clientService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        Client client = clientService.Get(id);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] Client client) =>
        Ok(await clientService.AddAsync(client));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] Client client)
    {
        client.Id = id;
        return Ok(await clientService.UpdateAsync(client));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientService.DeleteAsync(id);
        return NoContent();
    }
}
