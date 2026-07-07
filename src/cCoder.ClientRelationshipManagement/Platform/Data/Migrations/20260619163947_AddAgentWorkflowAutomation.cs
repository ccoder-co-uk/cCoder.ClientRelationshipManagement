using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkflowAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                schema: "process",
                table: "ProcessDefinitions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "process",
                table: "ProcessDefinitions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedOn",
                schema: "process",
                table: "ProcessDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                schema: "process",
                table: "ProcessDefinitions",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                schema: "process",
                table: "ProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                schema: "process",
                table: "ProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProposedByAgent",
                schema: "process",
                table: "ProcessDefinitions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                schema: "process",
                table: "ProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ExecutionUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WorkingDirectory = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Iterations = table.Column<int>(type: "int", nullable: false),
                    ProcessedItemCount = table.Column<int>(type: "int", nullable: false),
                    StartedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentMessages",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CorrelationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespondedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RespondedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMessages_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalSchema: "crm",
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "crm",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "leads",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_ProcessDefinitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalSchema: "process",
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_ProcessDefinitions_ProposedProcessDefinitionId",
                        column: x => x.ProposedProcessDefinitionId,
                        principalSchema: "process",
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_ProcessTasks_ProcessTaskId",
                        column: x => x.ProcessTaskId,
                        principalSchema: "process",
                        principalTable: "ProcessTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessages_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions",
                column: "SupersedesProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_TenantId_ScopeType_FamilyId_VersionNumber",
                schema: "process",
                table: "ProcessDefinitions",
                columns: new[] { "TenantId", "ScopeType", "FamilyId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_AgentRunId",
                schema: "crm",
                table: "AgentMessages",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ClientAccountId",
                schema: "crm",
                table: "AgentMessages",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_CorrelationKey",
                schema: "crm",
                table: "AgentMessages",
                column: "CorrelationKey");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_EmailId",
                schema: "crm",
                table: "AgentMessages",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_LeadId",
                schema: "crm",
                table: "AgentMessages",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_OpportunityId",
                schema: "crm",
                table: "AgentMessages",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProcessDefinitionId",
                schema: "crm",
                table: "AgentMessages",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProcessTaskId",
                schema: "crm",
                table: "AgentMessages",
                column: "ProcessTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProposedProcessDefinitionId",
                schema: "crm",
                table: "AgentMessages",
                column: "ProposedProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_TenantCompanyRelationshipId",
                schema: "crm",
                table: "AgentMessages",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessDefinitions_ProcessDefinitions_SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions",
                column: "SupersedesProcessDefinitionId",
                principalSchema: "process",
                principalTable: "ProcessDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessDefinitions_ProcessDefinitions_SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropTable(
                name: "AgentMessages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "AgentRuns",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "IX_ProcessDefinitions_SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ProcessDefinitions_TenantId_ScopeType_FamilyId_VersionNumber",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ApprovedOn",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ProposedByAgent",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "SupersedesProcessDefinitionId",
                schema: "process",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                schema: "process",
                table: "ProcessDefinitions");
        }
    }
}
