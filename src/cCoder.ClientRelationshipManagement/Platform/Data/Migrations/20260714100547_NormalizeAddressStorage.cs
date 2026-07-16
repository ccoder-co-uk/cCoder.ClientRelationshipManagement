using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAddressStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                schema: "leads",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                -- Reuse a canonical address already imported for the same authority record.
                UPDATE company
                SET company.RegisteredAddressId = address.Id
                FROM masterdata.Companies AS company
                INNER JOIN masterdata.Addresses AS address
                    ON address.SourceSystem = company.SourceSystem
                    AND address.LegacyId = company.SourceRecordId
                WHERE company.RegisteredAddressId IS NULL
                    AND company.SourceSystem IS NOT NULL
                    AND company.SourceRecordId IS NOT NULL;

                -- Preserve every remaining company address exactly as it was stored.
                CREATE TABLE #CompanyAddressBackfill
                (
                    CompanyId uniqueidentifier NOT NULL PRIMARY KEY,
                    AddressId uniqueidentifier NOT NULL
                );

                INSERT INTO #CompanyAddressBackfill (CompanyId, AddressId)
                SELECT company.Id, NEWID()
                FROM masterdata.Companies AS company
                WHERE company.RegisteredAddressId IS NULL
                    AND NULLIF(LTRIM(RTRIM(company.RegisteredOfficeText)), '') IS NOT NULL;

                INSERT INTO masterdata.Addresses
                (
                    Id, LegacyId, SourceSystem, IsVerified, PoBox, Line1, Line2,
                    TownOrCity, StateOrProvince, ZipOrPostalCode, CountryId,
                    VerificationNotes, CreatedBy, LastUpdatedBy, CreatedOn, LastUpdated
                )
                SELECT mapping.AddressId,
                    company.SourceRecordId,
                    company.SourceSystem,
                    company.IsVerified,
                    NULL,
                    company.RegisteredOfficeText,
                    NULL, NULL, NULL, NULL, NULL,
                    'Backfilled from Companies.RegisteredOfficeText',
                    'address-normalization-migration',
                    'address-normalization-migration',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM #CompanyAddressBackfill AS mapping
                INNER JOIN masterdata.Companies AS company ON company.Id = mapping.CompanyId;

                UPDATE company
                SET company.RegisteredAddressId = mapping.AddressId
                FROM masterdata.Companies AS company
                INNER JOIN #CompanyAddressBackfill AS mapping ON mapping.CompanyId = company.Id;

                DROP TABLE #CompanyAddressBackfill;

                -- Leads share their company's canonical address wherever possible.
                UPDATE lead
                SET lead.AddressId = company.RegisteredAddressId
                FROM leads.Leads AS lead
                INNER JOIN masterdata.Companies AS company ON company.Id = lead.CompanyId
                WHERE lead.AddressId IS NULL
                    AND company.RegisteredAddressId IS NOT NULL;

                -- Also reuse an authority address when a lead has not yet been linked to a company.
                UPDATE lead
                SET lead.AddressId = address.Id
                FROM leads.Leads AS lead
                INNER JOIN masterdata.Addresses AS address
                    ON address.SourceSystem = lead.SourceSystem
                    AND address.LegacyId = lead.SourceRecordId
                WHERE lead.AddressId IS NULL
                    AND lead.SourceSystem IS NOT NULL
                    AND lead.SourceRecordId IS NOT NULL;

                -- Any remaining lead-only text becomes its own canonical address.
                CREATE TABLE #LeadAddressBackfill
                (
                    LeadId uniqueidentifier NOT NULL PRIMARY KEY,
                    AddressId uniqueidentifier NOT NULL
                );

                INSERT INTO #LeadAddressBackfill (LeadId, AddressId)
                SELECT lead.Id, NEWID()
                FROM leads.Leads AS lead
                WHERE lead.AddressId IS NULL
                    AND NULLIF(LTRIM(RTRIM(lead.RawAddressText)), '') IS NOT NULL;

                INSERT INTO masterdata.Addresses
                (
                    Id, LegacyId, SourceSystem, IsVerified, PoBox, Line1, Line2,
                    TownOrCity, StateOrProvince, ZipOrPostalCode, CountryId,
                    VerificationNotes, CreatedBy, LastUpdatedBy, CreatedOn, LastUpdated
                )
                SELECT mapping.AddressId,
                    NULL,
                    lead.SourceSystem,
                    0,
                    NULL,
                    lead.RawAddressText,
                    NULL, NULL, NULL, NULL, NULL,
                    'Backfilled from Leads.RawAddressText',
                    'address-normalization-migration',
                    'address-normalization-migration',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM #LeadAddressBackfill AS mapping
                INNER JOIN leads.Leads AS lead ON lead.Id = mapping.LeadId;

                UPDATE lead
                SET lead.AddressId = mapping.AddressId
                FROM leads.Leads AS lead
                INNER JOIN #LeadAddressBackfill AS mapping ON mapping.LeadId = lead.Id;

                DROP TABLE #LeadAddressBackfill;
                """);

            migrationBuilder.DropColumn(
                name: "RawAddressText",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "RegisteredOfficeText",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AddressId",
                schema: "leads",
                table: "Leads",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SourceSystem_LegacyId",
                schema: "masterdata",
                table: "Addresses",
                columns: new[] { "SourceSystem", "LegacyId" },
                unique: true,
                filter: "[SourceSystem] IS NOT NULL AND [LegacyId] IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawAddressText",
                schema: "leads",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredOfficeText",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE company
                SET company.RegisteredOfficeText = CONCAT_WS(', ',
                    NULLIF(address.PoBox, ''), NULLIF(address.Line1, ''), NULLIF(address.Line2, ''),
                    NULLIF(address.TownOrCity, ''), NULLIF(address.StateOrProvince, ''),
                    NULLIF(address.ZipOrPostalCode, ''), NULLIF(address.CountryId, ''))
                FROM masterdata.Companies AS company
                INNER JOIN masterdata.Addresses AS address ON address.Id = company.RegisteredAddressId;

                UPDATE lead
                SET lead.RawAddressText = CONCAT_WS(', ',
                    NULLIF(address.PoBox, ''), NULLIF(address.Line1, ''), NULLIF(address.Line2, ''),
                    NULLIF(address.TownOrCity, ''), NULLIF(address.StateOrProvince, ''),
                    NULLIF(address.ZipOrPostalCode, ''), NULLIF(address.CountryId, ''))
                FROM leads.Leads AS lead
                INNER JOIN masterdata.Addresses AS address ON address.Id = lead.AddressId;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Addresses_AddressId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_AddressId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_SourceSystem_LegacyId",
                schema: "masterdata",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "AddressId",
                schema: "leads",
                table: "Leads");
        }
    }
}
