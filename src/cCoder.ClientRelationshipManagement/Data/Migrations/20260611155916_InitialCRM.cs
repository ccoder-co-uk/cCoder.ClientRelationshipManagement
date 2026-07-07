using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCRM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "CRM");

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoBox = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Line1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Line2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ZipOrPostalCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TownOrCity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StateOrProvince = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CountryId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AccountOwner = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeadSource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InitialRoute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FitScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OpportunitySummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PreferredOpeningAngle = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    NextActionDueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientContacts",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RelationshipRoute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContacts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LegalEntityName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TradingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CompanyNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VatNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContactEmailAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ContactPhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RegisteredOfficeText = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Addresses_RegisteredAddressId",
                        column: x => x.RegisteredAddressId,
                        principalSchema: "CRM",
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientMaterials",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentToContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientMaterials_ClientContacts_SentToContactId",
                        column: x => x.SentToContactId,
                        principalSchema: "CRM",
                        principalTable: "ClientContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientMaterials_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientOpportunities",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EstimatedAnnualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PainSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ValueHypothesis = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DecisionProcess = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    NextActionDueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientOpportunities_ClientContacts_PrimaryContactId",
                        column: x => x.PrimaryContactId,
                        principalSchema: "CRM",
                        principalTable: "ClientContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOpportunities_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientActivities",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientOpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    NextActionDueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientActivities_ClientContacts_ClientContactId",
                        column: x => x.ClientContactId,
                        principalSchema: "CRM",
                        principalTable: "ClientContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientActivities_ClientMaterials_ClientMaterialId",
                        column: x => x.ClientMaterialId,
                        principalSchema: "CRM",
                        principalTable: "ClientMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientActivities_ClientOpportunities_ClientOpportunityId",
                        column: x => x.ClientOpportunityId,
                        principalSchema: "CRM",
                        principalTable: "ClientOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientActivities_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientHandoffPacks",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedContractPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LegalEntity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PrimaryCommercialContact = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PrimaryOperationalContact = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PrimaryTechnicalContact = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AgreedScope = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CommercialTermsSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    PromisedOutcomes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    KnownRisks = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    OnboardingOwner = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HandedOffOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientHandoffPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientHandoffPacks_ClientOpportunities_ClientOpportunityId",
                        column: x => x.ClientOpportunityId,
                        principalSchema: "CRM",
                        principalTable: "ClientOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientHandoffPacks_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivities_ActivityOn",
                schema: "CRM",
                table: "ClientActivities",
                column: "ActivityOn");

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivities_ClientContactId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivities_ClientId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivities_ClientMaterialId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivities_ClientOpportunityId",
                schema: "CRM",
                table: "ClientActivities",
                column: "ClientOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId",
                schema: "CRM",
                table: "ClientContacts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_EmailAddress",
                schema: "CRM",
                table: "ClientContacts",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "IX_ClientHandoffPacks_ClientId",
                schema: "CRM",
                table: "ClientHandoffPacks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientHandoffPacks_ClientOpportunityId",
                schema: "CRM",
                table: "ClientHandoffPacks",
                column: "ClientOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMaterials_ClientId",
                schema: "CRM",
                table: "ClientMaterials",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMaterials_SentToContactId",
                schema: "CRM",
                table: "ClientMaterials",
                column: "SentToContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOpportunities_ClientId",
                schema: "CRM",
                table: "ClientOpportunities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOpportunities_PrimaryContactId",
                schema: "CRM",
                table: "ClientOpportunities",
                column: "PrimaryContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId",
                schema: "CRM",
                table: "Clients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ClientId",
                schema: "CRM",
                table: "Companies",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CompanyNumber",
                schema: "CRM",
                table: "Companies",
                column: "CompanyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_RegisteredAddressId",
                schema: "CRM",
                table: "Companies",
                column: "RegisteredAddressId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientActivities",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientHandoffPacks",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientMaterials",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientOpportunities",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "ClientContacts",
                schema: "CRM");

            migrationBuilder.DropTable(
                name: "Clients",
                schema: "CRM");
        }
    }
}
