using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class AddressController(IAddressOrchestrationService addressService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(addressService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        Address address = addressService.Get(id);
        return address is null ? NotFound() : Ok(address);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] Address address) =>
        Ok(await addressService.AddAsync(address));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] Address address)
    {
        address.Id = id;
        return Ok(await addressService.UpdateAsync(address));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await addressService.DeleteAsync(id);
        return NoContent();
    }
}
