using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentMessageProcessStepProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProcessStepId",
                schema: "crm",
                table: "AgentMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProcessStepId",
                schema: "crm",
                table: "AgentMessages",
                column: "ProcessStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentMessages_ProcessSteps_ProcessStepId",
                schema: "crm",
                table: "AgentMessages",
                column: "ProcessStepId",
                principalSchema: "process",
                principalTable: "ProcessSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentMessages_ProcessSteps_ProcessStepId",
                schema: "crm",
                table: "AgentMessages");

            migrationBuilder.DropIndex(
                name: "IX_AgentMessages_ProcessStepId",
                schema: "crm",
                table: "AgentMessages");

            migrationBuilder.DropColumn(
                name: "ProcessStepId",
                schema: "crm",
                table: "AgentMessages");
        }
    }
}
