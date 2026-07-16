using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequireLeadCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Reconnect any legacy lead to an existing company before enforcing the invariant.
                UPDATE lead
                SET lead.CompanyId = company.Id
                FROM leads.Leads AS lead
                INNER JOIN masterdata.Companies AS company
                    ON company.SourceSystem = lead.SourceSystem
                    AND company.SourceRecordId = lead.SourceRecordId
                WHERE lead.CompanyId IS NULL
                    AND lead.SourceSystem IS NOT NULL
                    AND lead.SourceRecordId IS NOT NULL;

                -- Preserve anomalous standalone leads by creating their company before dropping AddressId.
                CREATE TABLE #LeadCompanyBackfill
                (
                    LeadId uniqueidentifier NOT NULL PRIMARY KEY,
                    CompanyId uniqueidentifier NOT NULL
                );

                INSERT INTO #LeadCompanyBackfill (LeadId, CompanyId)
                SELECT lead.Id, NEWID()
                FROM leads.Leads AS lead
                WHERE lead.CompanyId IS NULL;

                INSERT INTO masterdata.Companies
                (
                    Id, SourceSystem, SourceRecordId, IsVerified, OfficialName,
                    TradingName, CompanyNumber, VatNumber, WebsiteUrl,
                    ContactEmailAddress, ContactPhoneNumber, ResearchSummary,
                    VerificationNotes, RegisteredAddressId, IsProspectingSuppressed,
                    CreatedBy, LastUpdatedBy, CreatedOn, LastUpdated
                )
                SELECT mapping.CompanyId,
                    lead.SourceSystem,
                    NULL,
                    0,
                    COALESCE(NULLIF(LTRIM(RTRIM(lead.RawCompanyName)), ''), 'Migrated lead company'),
                    lead.RawTradingName,
                    lead.RawCompanyNumber,
                    lead.RawVatNumber,
                    lead.RawWebsiteUrl,
                    lead.RawContactEmailAddress,
                    lead.RawContactPhoneNumber,
                    lead.QualificationNotes,
                    CONCAT('Created while requiring Lead.CompanyId. Original source record: ', COALESCE(lead.SourceRecordId, '(none)')),
                    lead.AddressId,
                    0,
                    'require-lead-company-migration',
                    'require-lead-company-migration',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM #LeadCompanyBackfill AS mapping
                INNER JOIN leads.Leads AS lead ON lead.Id = mapping.LeadId;

                UPDATE lead
                SET lead.CompanyId = mapping.CompanyId
                FROM leads.Leads AS lead
                INNER JOIN #LeadCompanyBackfill AS mapping ON mapping.LeadId = lead.Id;

                DROP TABLE #LeadCompanyBackfill;

                IF EXISTS (SELECT 1 FROM leads.Leads WHERE CompanyId IS NULL)
                    THROW 51000, 'Unable to associate every lead with a company.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Addresses_AddressId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_AddressId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AddressId",
                schema: "leads",
                table: "Leads");

            DropOperationalLeadIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "leads",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            CreateOperationalLeadIndexes(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropOperationalLeadIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "leads",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            CreateOperationalLeadIndexes(migrationBuilder);

            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                schema: "leads",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE lead
                SET lead.AddressId = company.RegisteredAddressId
                FROM leads.Leads AS lead
                INNER JOIN masterdata.Companies AS company ON company.Id = lead.CompanyId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AddressId",
                schema: "leads",
                table: "Leads",
                column: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Addresses_AddressId",
                schema: "leads",
                table: "Leads",
                column: "AddressId",
                principalSchema: "masterdata",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        static void DropOperationalLeadIndexes(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS IX_Leads_SourceLookup ON leads.Leads;
                DROP INDEX IF EXISTS IX_Leads_AgentSelection ON leads.Leads;
                """);

        static void CreateOperationalLeadIndexes(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                CREATE INDEX IX_Leads_SourceLookup
                    ON leads.Leads(TenantId, SourceSystem, SourceRecordId)
                    INCLUDE (SourceFileName, SourceId, CompanyId);
                CREATE INDEX IX_Leads_AgentSelection
                    ON leads.Leads(SourceSystem, RankingScore DESC, Status)
                    INCLUDE (Id, CompanyId, CreatedOn);
                """);
    }
}
