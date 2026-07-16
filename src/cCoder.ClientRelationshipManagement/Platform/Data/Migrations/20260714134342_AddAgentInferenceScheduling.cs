using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentInferenceScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AgentClaimExpiresOn",
                schema: "process",
                table: "ProcessTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgentClaimId",
                schema: "process",
                table: "ProcessTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentClaimedBy",
                schema: "process",
                table: "ProcessTasks",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AgentClaimedOn",
                schema: "process",
                table: "ProcessTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ClientAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LeadAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpportunityAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OpportunityAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_State_AgentClaimExpiresOn",
                schema: "process",
                table: "ProcessTasks",
                columns: new[] { "State", "AgentClaimExpiresOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_State_AgentClaimExpiresOn",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "AgentClaimExpiresOn",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "AgentClaimId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "AgentClaimedBy",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "AgentClaimedOn",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "ClientAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "ClientAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "LeadAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "LeadAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "OpportunityAgentConcurrency",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "OpportunityAiProfileKey",
                schema: "crm",
                table: "AgentAutomationSettings");
        }
    }
}
