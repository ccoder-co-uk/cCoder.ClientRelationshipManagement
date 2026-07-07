using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class ClientOpportunityController(IClientOpportunityOrchestrationService clientOpportunityService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(clientOpportunityService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        ClientOpportunity clientOpportunity = clientOpportunityService.Get(id);
        return clientOpportunity is null ? NotFound() : Ok(clientOpportunity);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] ClientOpportunity clientOpportunity) =>
        Ok(await clientOpportunityService.AddAsync(clientOpportunity));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] ClientOpportunity clientOpportunity)
    {
        clientOpportunity.Id = id;
        return Ok(await clientOpportunityService.UpdateAsync(clientOpportunity));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await clientOpportunityService.DeleteAsync(id);
        return NoContent();
    }
}
