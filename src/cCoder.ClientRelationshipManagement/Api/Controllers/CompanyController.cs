using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.AspNetCore.Mvc;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[ApiController]
[Route("Api/[controller]")]
public class CompanyController(ICompanyOrchestrationService companyService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(companyService.GetAll());

    [HttpGet("{id:guid}")]
    public IActionResult Get([FromRoute] Guid id)
    {
        Company company = companyService.Get(id);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async ValueTask<IActionResult> Post([FromBody] Company company) =>
        Ok(await companyService.AddAsync(company));

    [HttpPut("{id:guid}")]
    public async ValueTask<IActionResult> Put([FromRoute] Guid id, [FromBody] Company company)
    {
        company.Id = id;
        return Ok(await companyService.UpdateAsync(company));
    }

    [HttpDelete("{id:guid}")]
    public async ValueTask<IActionResult> Delete([FromRoute] Guid id)
    {
        await companyService.DeleteAsync(id);
        return NoContent();
    }
}
