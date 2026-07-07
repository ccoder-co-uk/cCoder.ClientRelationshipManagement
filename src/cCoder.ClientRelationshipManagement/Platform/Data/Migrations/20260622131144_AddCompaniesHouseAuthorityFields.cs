using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompaniesHouseAuthorityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyCategory",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyStatus",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfOrigin",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DissolvedOn",
                schema: "masterdata",
                table: "Companies",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IncorporatedOn",
                schema: "masterdata",
                table: "Companies",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousNamesJson",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimarySicCodes",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryUri",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyCategory",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CompanyStatus",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CountryOfOrigin",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DissolvedOn",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IncorporatedOn",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PreviousNamesJson",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PrimarySicCodes",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RegistryUri",
                schema: "masterdata",
                table: "Companies");
        }
    }
}
