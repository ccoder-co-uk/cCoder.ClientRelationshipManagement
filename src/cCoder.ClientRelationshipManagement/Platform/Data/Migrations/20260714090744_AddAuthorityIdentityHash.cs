using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorityIdentityHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorityRecordHash",
                schema: "masterdata",
                table: "Companies",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SourceSystem_SourceRecordId",
                schema: "masterdata",
                table: "Companies",
                columns: new[] { "SourceSystem", "SourceRecordId" },
                unique: true,
                filter: "[SourceSystem] IS NOT NULL AND [SourceRecordId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_SourceSystem_SourceRecordId",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AuthorityRecordHash",
                schema: "masterdata",
                table: "Companies");
        }
    }
}
