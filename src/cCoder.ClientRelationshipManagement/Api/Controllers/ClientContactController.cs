using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientContactController(IClientContactOrchestrationService clientContactService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientContactService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        ClientContact clientContact = clientContactService.Get(id);
        return clientContact is null ? NotFound() : Ok(clientContact);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] ClientContact clientContact) =>
        Ok(await clientContactService.AddAsync(clientContact));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] ClientContact clientContact)
    {
        clientContact.Id = id;
        return Ok(await clientContactService.UpdateAsync(clientContact));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientContactService.DeleteAsync(id);
        return NoContent();
    }
}
