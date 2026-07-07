using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDatabaseDeleteActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientActivities_Clients_ClientId",
                schema: "CRM",
                table: "ClientActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                schema: "CRM",
                table: "ClientContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientHandoffPacks_Clients_ClientId",
                schema: "CRM",
                table: "ClientHandoffPacks");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientOpportunities_Clients_ClientId",
                schema: "CRM",
                table: "ClientOpportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Clients_ClientId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientActivities_Clients_ClientId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                schema: "CRM",
                table: "ClientContacts",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientHandoffPacks_Clients_ClientId",
                schema: "CRM",
                table: "ClientHandoffPacks",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientOpportunities_Clients_ClientId",
                schema: "CRM",
                table: "ClientOpportunities",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies",
                column: "RegisteredAddressId",
                principalSchema: "CRM",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Clients_ClientId",
                schema: "CRM",
                table: "Companies",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientActivities_Clients_ClientId",
                schema: "CRM",
                table: "ClientActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                schema: "CRM",
                table: "ClientContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientHandoffPacks_Clients_ClientId",
                schema: "CRM",
                table: "ClientHandoffPacks");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientOpportunities_Clients_ClientId",
                schema: "CRM",
                table: "ClientOpportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Clients_ClientId",
                schema: "CRM",
                table: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientActivities_Clients_ClientId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                schema: "CRM",
                table: "ClientContacts",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientHandoffPacks_Clients_ClientId",
                schema: "CRM",
                table: "ClientHandoffPacks",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientOpportunities_Clients_ClientId",
                schema: "CRM",
                table: "ClientOpportunities",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "CRM",
                table: "Companies",
                column: "RegisteredAddressId",
                principalSchema: "CRM",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Clients_ClientId",
                schema: "CRM",
                table: "Companies",
                column: "ClientId",
                principalSchema: "CRM",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
