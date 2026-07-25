using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedWorkflowStepsAndCompanyEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                schema: "process",
                table: "ProcessSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepType",
                schema: "process",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContextJson",
                schema: "process",
                table: "ProcessInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "masterdata",
                table: "CompanyContacts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ObservedOn",
                schema: "masterdata",
                table: "CompanyContacts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTitle",
                schema: "masterdata",
                table: "CompanyContacts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                schema: "masterdata",
                table: "CompanyContacts",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyEvidence",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SourceTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SourceSnippet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Extractor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResourceHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ObservedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyEvidence_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEvidence_CompanyId_Key_ResourceHash",
                schema: "masterdata",
                table: "CompanyEvidence",
                columns: new[] { "CompanyId", "Key", "ResourceHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyEvidence",
                schema: "masterdata");

            migrationBuilder.DropColumn(
                name: "ConfigurationJson",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "StepType",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ContextJson",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "ObservedOn",
                schema: "masterdata",
                table: "CompanyContacts");

            migrationBuilder.DropColumn(
                name: "SourceTitle",
                schema: "masterdata",
                table: "CompanyContacts");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                schema: "masterdata",
                table: "CompanyContacts");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "masterdata",
                table: "CompanyContacts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
