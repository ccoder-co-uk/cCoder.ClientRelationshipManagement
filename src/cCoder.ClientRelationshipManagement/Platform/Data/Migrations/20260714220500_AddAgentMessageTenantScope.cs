using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(ClientRelationshipDbContext))]
[Migration("20260714220500_AddAgentMessageTenantScope")]
public sealed class AddAgentMessageTenantScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "TenantId", schema: "crm", table: "AgentMessages", type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "default");
        migrationBuilder.Sql("""
            UPDATE m SET TenantId = COALESCE(r.TenantId, p.TenantId, 'default')
            FROM crm.AgentMessages m
            LEFT JOIN crm.TenantCompanyRelationships r ON r.Id = m.TenantCompanyRelationshipId
            LEFT JOIN process.ProcessDefinitions p ON p.Id = m.ProcessDefinitionId;
            """);
        migrationBuilder.CreateIndex(name: "IX_AgentMessages_TenantId_State", schema: "crm", table: "AgentMessages", columns: new[] { "TenantId", "State" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AgentMessages_TenantId_State", schema: "crm", table: "AgentMessages");
        migrationBuilder.DropColumn(name: "TenantId", schema: "crm", table: "AgentMessages");
    }
}
