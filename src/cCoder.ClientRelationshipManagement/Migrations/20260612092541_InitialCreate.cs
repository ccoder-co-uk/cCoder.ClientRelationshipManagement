using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies",
                column: "RegisteredAddressId",
                principalSchema: "CRM",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies",
                column: "RegisteredAddressId",
                principalSchema: "CRM",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
