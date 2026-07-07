using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientMaterialController(IClientMaterialOrchestrationService clientMaterialService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientMaterialService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        ClientMaterial clientMaterial = clientMaterialService.Get(id);
        return clientMaterial is null ? NotFound() : Ok(clientMaterial);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] ClientMaterial clientMaterial) =>
        Ok(await clientMaterialService.AddAsync(clientMaterial));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] ClientMaterial clientMaterial)
    {
        clientMaterial.Id = id;
        return Ok(await clientMaterialService.UpdateAsync(clientMaterial));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientMaterialService.DeleteAsync(id);
        return NoContent();
    }
}
