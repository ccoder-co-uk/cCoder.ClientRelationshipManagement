using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class EmailController(IEmailOrchestrationService emailService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(emailService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        Email email = emailService.Get(id);
        return email is null ? NotFound() : Ok(email);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] Email email) =>
        Ok(await emailService.AddAsync(email));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] Email email)
    {
        email.Id = id;
        return Ok(await emailService.UpdateAsync(email));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await emailService.DeleteAsync(id);
        return NoContent();
    }
}
