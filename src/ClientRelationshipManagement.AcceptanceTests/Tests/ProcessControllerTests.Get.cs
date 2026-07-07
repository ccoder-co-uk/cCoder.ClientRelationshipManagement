using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ProcessControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_And_Edit_ShowProcessDesigner()
    {
        string indexResponse = await GetStringAsync("/Process");

        ProcessDefinition definition = await QueryInAdminContextAsync(db =>
            db.ProcessDefinitions
                .Where(item => item.TenantId == AcceptanceSettings.TenantId)
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.Name)
                .FirstAsync());

        string editResponse = await GetStringAsync($"/Process/Edit/{definition.Id}");

        indexResponse.Should().Contain("Process List");
        editResponse.Should().Contain("Process Details");
        editResponse.Should().Contain("Step List");
        editResponse.Should().Contain("Supported Tokens");
    }
}
