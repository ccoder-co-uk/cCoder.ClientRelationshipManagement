using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class WidenMailboxReferencesAndTrackBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "References",
                schema: "crm",
                table: "MailboxMessageRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MailboxEvidenceBackfillCompletedOn",
                schema: "crm",
                table: "AgentAutomationSettings",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailboxEvidenceBackfillCompletedOn",
                schema: "crm",
                table: "AgentAutomationSettings");

            migrationBuilder.AlterColumn<string>(
                name: "References",
                schema: "crm",
                table: "MailboxMessageRecords",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
