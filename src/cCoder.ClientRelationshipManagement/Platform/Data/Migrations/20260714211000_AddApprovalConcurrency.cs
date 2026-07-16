using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(ClientRelationshipDbContext))]
[Migration("20260714211000_AddApprovalConcurrency")]
public sealed class AddApprovalConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ApprovalAgentConcurrency",
            schema: "crm",
            table: "AgentAutomationSettings",
            type: "int",
            nullable: false,
            defaultValue: 2);

        migrationBuilder.Sql(
            """
            UPDATE [crm].[AgentAutomationSettings]
            SET [ApprovalAgentConcurrency] = 2,
                [LeadAgentConcurrency] = CASE
                    WHEN COALESCE([LeadAiProfileKey], N'local-ollama') IN (N'open-ai', N'azure-foundry') THEN 4
                    WHEN COALESCE([LeadAiProfileKey], N'local-ollama') <> N'none' THEN 2
                    ELSE [LeadAgentConcurrency]
                END,
                [OpportunityAgentConcurrency] = CASE
                    WHEN COALESCE([OpportunityAiProfileKey], N'local-ollama') IN (N'open-ai', N'azure-foundry') THEN 4
                    WHEN COALESCE([OpportunityAiProfileKey], N'local-ollama') <> N'none' THEN 2
                    ELSE [OpportunityAgentConcurrency]
                END,
                [ClientAgentConcurrency] = CASE
                    WHEN COALESCE([ClientAiProfileKey], N'none') IN (N'open-ai', N'azure-foundry') THEN 4
                    WHEN COALESCE([ClientAiProfileKey], N'none') <> N'none' THEN 2
                    ELSE [ClientAgentConcurrency]
                END,
                [LastUpdatedBy] = N'system',
                [LastUpdated] = SYSUTCDATETIME();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApprovalAgentConcurrency",
            schema: "crm",
            table: "AgentAutomationSettings");
    }
}
