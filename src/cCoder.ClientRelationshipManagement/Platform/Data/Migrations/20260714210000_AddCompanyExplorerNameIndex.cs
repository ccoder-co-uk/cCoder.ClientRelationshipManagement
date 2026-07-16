using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260714210000_AddCompanyExplorerNameIndex")]
public sealed class AddCompanyExplorerNameIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Companies_OfficialName",
            schema: "masterdata",
            table: "Companies",
            column: "OfficialName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Companies_OfficialName",
            schema: "masterdata",
            table: "Companies");
    }
}
