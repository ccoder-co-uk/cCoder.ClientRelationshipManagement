using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportManagementWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RankingRationale",
                schema: "leads",
                table: "Leads",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RankingScore",
                schema: "leads",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                schema: "leads",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualRevenue",
                schema: "masterdata",
                table: "Companies",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeCount",
                schema: "masterdata",
                table: "Companies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RankingRationale",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RankingScore",
                schema: "masterdata",
                table: "Companies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevenueCurrency",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sources",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IsAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Imports",
                schema: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    StoredObjectKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    JobStatus = table.Column<int>(type: "int", nullable: false),
                    UploadStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    UploadedBytes = table.Column<long>(type: "bigint", nullable: false),
                    TotalRowCount = table.Column<int>(type: "int", nullable: false),
                    ImportedRowCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    WarningSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    MappingSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessingCheckpoint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UploadSessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UploadSessionExpiresOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UploadedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MarkedReadyOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessingStartedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessingCompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Imports_Sources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "masterdata",
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportLinks",
                schema: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRowKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceRowNumber = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportLinks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportLinks_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportLinks_Imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "leads",
                        principalTable: "Imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportLinks_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "leads",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportLinks_Sources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "masterdata",
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SourceId",
                schema: "leads",
                table: "Leads",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLinks_CompanyContactId",
                schema: "leads",
                table: "ImportLinks",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLinks_CompanyId",
                schema: "leads",
                table: "ImportLinks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLinks_ImportId_SourceRowNumber",
                schema: "leads",
                table: "ImportLinks",
                columns: new[] { "ImportId", "SourceRowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportLinks_LeadId",
                schema: "leads",
                table: "ImportLinks",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLinks_SourceId_SourceRowKey",
                schema: "leads",
                table: "ImportLinks",
                columns: new[] { "SourceId", "SourceRowKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Imports_JobStatus",
                schema: "leads",
                table: "Imports",
                column: "JobStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Imports_SourceId",
                schema: "leads",
                table: "Imports",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Imports_UploadSessionId",
                schema: "leads",
                table: "Imports",
                column: "UploadSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Name_CountryCode",
                schema: "masterdata",
                table: "Sources",
                columns: new[] { "Name", "CountryCode" },
                unique: true,
                filter: "[CountryCode] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Sources_SourceId",
                schema: "leads",
                table: "Leads",
                column: "SourceId",
                principalSchema: "masterdata",
                principalTable: "Sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Sources_SourceId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropTable(
                name: "ImportLinks",
                schema: "leads");

            migrationBuilder.DropTable(
                name: "Imports",
                schema: "leads");

            migrationBuilder.DropTable(
                name: "Sources",
                schema: "masterdata");

            migrationBuilder.DropIndex(
                name: "IX_Leads_SourceId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "RankingRationale",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "RankingScore",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SourceId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AnnualRevenue",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EmployeeCount",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RankingRationale",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RankingScore",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RevenueCurrency",
                schema: "masterdata",
                table: "Companies");
        }
    }
}
