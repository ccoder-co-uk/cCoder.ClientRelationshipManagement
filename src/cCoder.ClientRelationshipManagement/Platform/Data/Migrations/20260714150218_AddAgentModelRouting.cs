using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentModelRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientAiModel",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadAiModel",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpportunityAiModel",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedAiModel",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientAiModel",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "LeadAiModel",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "OpportunityAiModel",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.DropColumn(
                name: "SelectedAiModel",
                schema: "crm",
                table: "AgentAutomationSettings");
        }
    }
}
