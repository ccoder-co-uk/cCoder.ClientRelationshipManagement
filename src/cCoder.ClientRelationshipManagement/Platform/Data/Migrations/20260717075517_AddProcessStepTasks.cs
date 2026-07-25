using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessStepTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessStepTasks",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    HandlerKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstructionsTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredContextKeys = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ProducedContextKeys = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NextTaskKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FailureTaskKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStepTasks_ProcessSteps_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalSchema: "process",
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepTaskRuns",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessStepTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationErrors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepTaskRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStepTaskRuns_ProcessStepTasks_ProcessStepTaskId",
                        column: x => x.ProcessStepTaskId,
                        principalSchema: "process",
                        principalTable: "ProcessStepTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessStepTaskRuns_ProcessTasks_ProcessTaskId",
                        column: x => x.ProcessTaskId,
                        principalSchema: "process",
                        principalTable: "ProcessTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepTaskAttempts",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessStepTaskRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    InputContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationErrors = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepTaskAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStepTaskAttempts_ProcessStepTaskRuns_ProcessStepTaskRunId",
                        column: x => x.ProcessStepTaskRunId,
                        principalSchema: "process",
                        principalTable: "ProcessStepTaskRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTaskAttempts_ProcessStepTaskRunId_AttemptNumber",
                schema: "process",
                table: "ProcessStepTaskAttempts",
                columns: new[] { "ProcessStepTaskRunId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTaskRuns_ProcessStepTaskId",
                schema: "process",
                table: "ProcessStepTaskRuns",
                column: "ProcessStepTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTaskRuns_ProcessTaskId_ProcessStepTaskId",
                schema: "process",
                table: "ProcessStepTaskRuns",
                columns: new[] { "ProcessTaskId", "ProcessStepTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTaskRuns_State_LastUpdated",
                schema: "process",
                table: "ProcessStepTaskRuns",
                columns: new[] { "State", "LastUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTasks_ProcessStepId_Key",
                schema: "process",
                table: "ProcessStepTasks",
                columns: new[] { "ProcessStepId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessStepTaskAttempts",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ProcessStepTaskRuns",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ProcessStepTasks",
                schema: "process");
        }
    }
}
