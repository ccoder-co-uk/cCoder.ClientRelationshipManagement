using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyLifecycleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Objective",
                schema: "process",
                table: "ProcessSteps",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProducedFacts",
                schema: "process",
                table: "ProcessSteps",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredFacts",
                schema: "process",
                table: "ProcessSteps",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViabilityImpact",
                schema: "process",
                table: "ProcessSteps",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyHistory",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FactKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FactValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyHistory_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHistory_CompanyId_FactKey_OccurredOn",
                schema: "masterdata",
                table: "CompanyHistory",
                columns: new[] { "CompanyId", "FactKey", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHistory_CompanyId_TenantId_OccurredOn",
                schema: "masterdata",
                table: "CompanyHistory",
                columns: new[] { "CompanyId", "TenantId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHistory_ProcessTaskId",
                schema: "masterdata",
                table: "CompanyHistory",
                column: "ProcessTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyHistory",
                schema: "masterdata");

            migrationBuilder.DropColumn(
                name: "Objective",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ProducedFacts",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiredFacts",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ViabilityImpact",
                schema: "process",
                table: "ProcessSteps");
        }
    }
}
