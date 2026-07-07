using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientProcessAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientProcessDefinitions",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProcessDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientProcessSteps",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsEntryPoint = table.Column<bool>(type: "bit", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusOnActivate = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StageOnActivate = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DueAfterDays = table.Column<int>(type: "int", nullable: false),
                    DueAfterHours = table.Column<int>(type: "int", nullable: false),
                    TaskTitleTemplate = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TaskInstructionsTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    EmailSubjectTemplate = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EmailBodyTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CallScriptTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    QuestionSetTemplate = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProcessSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProcessSteps_ClientProcessDefinitions_ClientProcessDefinitionId",
                        column: x => x.ClientProcessDefinitionId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientProcessTransitions",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextClientProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutcomeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OutcomeLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsDefaultOutcome = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    TerminalStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TerminalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProcessTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProcessTransitions_ClientProcessSteps_ClientProcessStepId",
                        column: x => x.ClientProcessStepId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessTransitions_ClientProcessSteps_NextClientProcessStepId",
                        column: x => x.NextClientProcessStepId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientProcessInstances",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentClientProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentClientProcessTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompletionOutcomeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StartedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProcessInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProcessInstances_ClientProcessDefinitions_ClientProcessDefinitionId",
                        column: x => x.ClientProcessDefinitionId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessInstances_ClientProcessSteps_CurrentClientProcessStepId",
                        column: x => x.CurrentClientProcessStepId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessInstances_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientProcessTasks",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProcessInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RenderedTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RenderedInstructions = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    RenderedEmailSubject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RenderedEmailBody = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    RenderedCallScript = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    RenderedQuestionSet = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CompletionOutcomeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProcessTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProcessTasks_ClientProcessInstances_ClientProcessInstanceId",
                        column: x => x.ClientProcessInstanceId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessTasks_ClientProcessSteps_ClientProcessStepId",
                        column: x => x.ClientProcessStepId,
                        principalSchema: "CRM",
                        principalTable: "ClientProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessTasks_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientProcessTasks_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "CRM",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessDefinitions_TenantId",
                schema: "CRM",
                table: "ClientProcessDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessDefinitions_TenantId_IsDefault",
                schema: "CRM",
                table: "ClientProcessDefinitions",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessInstances_ClientId",
                schema: "CRM",
                table: "ClientProcessInstances",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessInstances_ClientId_State",
                schema: "CRM",
                table: "ClientProcessInstances",
                columns: new[] { "ClientId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessInstances_ClientProcessDefinitionId",
                schema: "CRM",
                table: "ClientProcessInstances",
                column: "ClientProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessInstances_CurrentClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessInstances",
                column: "CurrentClientProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessInstances_CurrentClientProcessTaskId",
                schema: "CRM",
                table: "ClientProcessInstances",
                column: "CurrentClientProcessTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessSteps_ClientProcessDefinitionId",
                schema: "CRM",
                table: "ClientProcessSteps",
                column: "ClientProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessSteps_ClientProcessDefinitionId_Sequence",
                schema: "CRM",
                table: "ClientProcessSteps",
                columns: new[] { "ClientProcessDefinitionId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_ClientId",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_ClientProcessInstanceId",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "ClientProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_ClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "ClientProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_DueOn",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_EmailId",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "EmailId",
                unique: true,
                filter: "[EmailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTasks_State",
                schema: "CRM",
                table: "ClientProcessTasks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTransitions_ClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessTransitions",
                column: "ClientProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProcessTransitions_NextClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessTransitions",
                column: "NextClientProcessStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientProcessInstances_ClientProcessTasks_CurrentClientProcessTaskId",
                schema: "CRM",
                table: "ClientProcessInstances",
                column: "CurrentClientProcessTaskId",
                principalSchema: "CRM",
                principalTable: "ClientProcessTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientProcessInstances_ClientProcessDefinitions_ClientProcessDefinitionId",
                schema: "CRM",
                table: "ClientProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientProcessSteps_ClientProcessDefinitions_ClientProcessDefinitionId",
                schema: "CRM",
                table: "ClientProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientProcessInstances_ClientProcessSteps_CurrentClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientProcessTasks_ClientProcessSteps_ClientProcessStepId",
                schema: "CRM",
                table: "ClientProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientProcessInstances_ClientProcessTasks_CurrentClientProcessTaskId",
                schema: "CRM",
                table: "ClientProcessInstances");

            migrationBuilder.DropTable(
                name: "ClientProcessTransitions",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientProcessDefinitions",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientProcessSteps",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientProcessTasks",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientProcessInstances",
                schema: "CRM");
        }
    }
}
