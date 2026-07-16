using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260714212000_AddAgentRunWorkLane")]
public sealed class AddAgentRunWorkLane : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WorkLane",
            schema: "crm",
            table: "AgentRuns",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProcessTaskId",
            schema: "crm",
            table: "AgentRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProcessStepId",
            schema: "crm",
            table: "AgentRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProcessStepKey",
            schema: "crm",
            table: "AgentRuns",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AgentRuns_Kind_WorkLane_CompletedOn",
            schema: "crm",
            table: "AgentRuns",
            columns: new[] { "Kind", "WorkLane", "CompletedOn" });

        migrationBuilder.CreateIndex(
            name: "IX_AgentRuns_ProcessTaskId",
            schema: "crm",
            table: "AgentRuns",
            column: "ProcessTaskId");

        migrationBuilder.CreateIndex(
            name: "IX_AgentRuns_ProcessStepId",
            schema: "crm",
            table: "AgentRuns",
            column: "ProcessStepId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AgentRuns_Kind_WorkLane_CompletedOn",
            schema: "crm",
            table: "AgentRuns");

        migrationBuilder.DropIndex(name: "IX_AgentRuns_ProcessTaskId", schema: "crm", table: "AgentRuns");
        migrationBuilder.DropIndex(name: "IX_AgentRuns_ProcessStepId", schema: "crm", table: "AgentRuns");

        migrationBuilder.DropColumn(
            name: "WorkLane",
            schema: "crm",
            table: "AgentRuns");

        migrationBuilder.DropColumn(name: "ProcessTaskId", schema: "crm", table: "AgentRuns");
        migrationBuilder.DropColumn(name: "ProcessStepId", schema: "crm", table: "AgentRuns");
        migrationBuilder.DropColumn(name: "ProcessStepKey", schema: "crm", table: "AgentRuns");
    }
}
