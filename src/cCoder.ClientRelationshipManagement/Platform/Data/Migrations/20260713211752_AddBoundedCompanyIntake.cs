using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoundedCompanyIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProspectingSuppressed",
                schema: "masterdata",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProspectingSuppressedOn",
                schema: "masterdata",
                table: "Companies",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProspectingSuppressedReason",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SourceSystem_IsProspectingSuppressed_RankingScore",
                schema: "masterdata",
                table: "Companies",
                columns: new[] { "SourceSystem", "IsProspectingSuppressed", "RankingScore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_SourceSystem_IsProspectingSuppressed_RankingScore",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsProspectingSuppressed",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ProspectingSuppressedOn",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ProspectingSuppressedReason",
                schema: "masterdata",
                table: "Companies");
        }
    }
}
