using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatformSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.EnsureSchema(
                name: "masterdata");

            migrationBuilder.EnsureSchema(
                name: "leads");

            migrationBuilder.EnsureSchema(
                name: "process");

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    PoBox = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Line1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Line2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TownOrCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StateOrProvince = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipOrPostalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CountryId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessDefinitions",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceRecordId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    OfficialName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LegalEntityName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TradingName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VatNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContactEmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactPhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RegisteredOfficeText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResearchSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisteredAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Addresses_RegisteredAddressId",
                        column: x => x.RegisteredAddressId,
                        principalSchema: "masterdata",
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSteps",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsEntryPoint = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    DueAfterDays = table.Column<int>(type: "int", nullable: false),
                    DueAfterHours = table.Column<int>(type: "int", nullable: false),
                    RelationshipStatusOnActivate = table.Column<int>(type: "int", nullable: true),
                    SalesStageOnActivate = table.Column<int>(type: "int", nullable: true),
                    ClientAccountStatusOnActivate = table.Column<int>(type: "int", nullable: true),
                    TaskTitleTemplate = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TaskInstructionsTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailSubjectTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailBodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CallScriptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionSetTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessSteps_ProcessDefinitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalSchema: "process",
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyContacts",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyContacts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantCompanyRelationships",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountOwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AccountOwnerDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStage = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    LeadSource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InitialRoute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FitScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpportunitySummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreferredOpeningAngle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResearchSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataQualityNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCompanyRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantCompanyRelationships_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessTransitions",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutcomeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OutcomeLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsDefaultOutcome = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    ResultingRelationshipStatus = table.Column<int>(type: "int", nullable: true),
                    ResultingSalesStage = table.Column<int>(type: "int", nullable: true),
                    ResultingClientAccountStatus = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessTransitions_ProcessSteps_NextProcessStepId",
                        column: x => x.NextProcessStepId,
                        principalSchema: "process",
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTransitions_ProcessSteps_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalSchema: "process",
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RelationshipContacts",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    RelationshipRoute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationshipContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelationshipContacts_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelationshipContacts_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Opportunities",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryRelationshipContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    EstimatedAnnualValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PainSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueHypothesis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionProcess = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WonOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LostOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_RelationshipContacts_PrimaryRelationshipContactId",
                        column: x => x.PrimaryRelationshipContactId,
                        principalSchema: "crm",
                        principalTable: "RelationshipContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientAccounts",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WonOpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ContractSignedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GoLiveOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AccountReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BillingNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientAccounts_Opportunities_WonOpportunityId",
                        column: x => x.WonOpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientAccounts_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceRecordId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RawCompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RawTradingName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RawCompanyNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RawVatNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RawWebsiteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RawContactEmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RawContactPhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RawAddressText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualificationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "masterdata",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoffPacks",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreedScope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommercialTermsSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromisedOutcomes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryCommercialContact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryOperationalContact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryTechnicalContact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KnownRisks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OnboardingOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalEntity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedContractPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoffPacks_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SentOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Materials_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Materials_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Materials_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeadContacts",
                schema: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadContacts_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "leads",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextActionDueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalSchema: "crm",
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Emails",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FromDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FromEmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReplyToAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CcAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BccAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ApprovedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledSendTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSendAttemptOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SendFailureCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Emails_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalSchema: "crm",
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailRecipients",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecipientType = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "crm",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessInstances",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentProcessTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    CompletionOutcomeKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "leads",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_ProcessDefinitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalSchema: "process",
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_ProcessSteps_CurrentProcessStepId",
                        column: x => x.CurrentProcessStepId,
                        principalSchema: "process",
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessTasks",
                schema: "process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    DueOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RenderedTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RenderedInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenderedEmailSubject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenderedEmailBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenderedCallScript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenderedQuestionSet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletionOutcomeKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_ClientAccounts_ClientAccountId",
                        column: x => x.ClientAccountId,
                        principalSchema: "crm",
                        principalTable: "ClientAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_Emails_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "crm",
                        principalTable: "Emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_Leads_LeadId",
                        column: x => x.LeadId,
                        principalSchema: "leads",
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalSchema: "process",
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_ProcessSteps_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalSchema: "process",
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessTasks_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ClientAccountId",
                schema: "crm",
                table: "Activities",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_CompanyContactId",
                schema: "crm",
                table: "Activities",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_MaterialId",
                schema: "crm",
                table: "Activities",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_OpportunityId",
                schema: "crm",
                table: "Activities",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TenantCompanyRelationshipId",
                schema: "crm",
                table: "Activities",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccounts_TenantCompanyRelationshipId",
                schema: "crm",
                table: "ClientAccounts",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccounts_WonOpportunityId",
                schema: "crm",
                table: "ClientAccounts",
                column: "WonOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CompanyNumber",
                schema: "masterdata",
                table: "Companies",
                column: "CompanyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_RegisteredAddressId",
                schema: "masterdata",
                table: "Companies",
                column: "RegisteredAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_VatNumber",
                schema: "masterdata",
                table: "Companies",
                column: "VatNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyContacts_CompanyId",
                schema: "masterdata",
                table: "CompanyContacts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_CompanyContactId",
                schema: "crm",
                table: "EmailRecipients",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_EmailId",
                schema: "crm",
                table: "EmailRecipients",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_ClientAccountId",
                schema: "crm",
                table: "Emails",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_CompanyContactId",
                schema: "crm",
                table: "Emails",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MaterialId",
                schema: "crm",
                table: "Emails",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_OpportunityId",
                schema: "crm",
                table: "Emails",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_TenantCompanyRelationshipId",
                schema: "crm",
                table: "Emails",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffPacks_ClientAccountId",
                schema: "crm",
                table: "HandoffPacks",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadContacts_LeadId",
                schema: "leads",
                table: "LeadContacts",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId",
                schema: "leads",
                table: "Leads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OpportunityId",
                schema: "leads",
                table: "Leads",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantCompanyRelationshipId",
                schema: "leads",
                table: "Leads",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_Status",
                schema: "leads",
                table: "Leads",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ClientAccountId",
                schema: "crm",
                table: "Materials",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CompanyContactId",
                schema: "crm",
                table: "Materials",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_OpportunityId",
                schema: "crm",
                table: "Materials",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_TenantCompanyRelationshipId",
                schema: "crm",
                table: "Materials",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_PrimaryRelationshipContactId",
                schema: "crm",
                table: "Opportunities",
                column: "PrimaryRelationshipContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_TenantCompanyRelationshipId_Stage",
                schema: "crm",
                table: "Opportunities",
                columns: new[] { "TenantCompanyRelationshipId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_TenantId_ScopeType_IsDefault",
                schema: "process",
                table: "ProcessDefinitions",
                columns: new[] { "TenantId", "ScopeType", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_ClientAccountId",
                schema: "process",
                table: "ProcessInstances",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_CurrentProcessStepId",
                schema: "process",
                table: "ProcessInstances",
                column: "CurrentProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_CurrentProcessTaskId",
                schema: "process",
                table: "ProcessInstances",
                column: "CurrentProcessTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_LeadId",
                schema: "process",
                table: "ProcessInstances",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_OpportunityId",
                schema: "process",
                table: "ProcessInstances",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_ProcessDefinitionId",
                schema: "process",
                table: "ProcessInstances",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_TenantCompanyRelationshipId",
                schema: "process",
                table: "ProcessInstances",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessDefinitionId",
                schema: "process",
                table: "ProcessSteps",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_ClientAccountId",
                schema: "process",
                table: "ProcessTasks",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_EmailId",
                schema: "process",
                table: "ProcessTasks",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_LeadId",
                schema: "process",
                table: "ProcessTasks",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_OpportunityId",
                schema: "process",
                table: "ProcessTasks",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_ProcessInstanceId",
                schema: "process",
                table: "ProcessTasks",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_ProcessStepId",
                schema: "process",
                table: "ProcessTasks",
                column: "ProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_TenantCompanyRelationshipId",
                schema: "process",
                table: "ProcessTasks",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTransitions_NextProcessStepId",
                schema: "process",
                table: "ProcessTransitions",
                column: "NextProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTransitions_ProcessStepId",
                schema: "process",
                table: "ProcessTransitions",
                column: "ProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipContacts_CompanyContactId",
                schema: "crm",
                table: "RelationshipContacts",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipContacts_TenantCompanyRelationshipId_CompanyContactId",
                schema: "crm",
                table: "RelationshipContacts",
                columns: new[] { "TenantCompanyRelationshipId", "CompanyContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantCompanyRelationships_CompanyId",
                schema: "crm",
                table: "TenantCompanyRelationships",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCompanyRelationships_TenantId_Status",
                schema: "crm",
                table: "TenantCompanyRelationships",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessInstances_ProcessTasks_CurrentProcessTaskId",
                schema: "process",
                table: "ProcessInstances",
                column: "CurrentProcessTaskId",
                principalSchema: "process",
                principalTable: "ProcessTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_ClientAccounts_ClientAccountId",
                schema: "crm",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_ClientAccounts_ClientAccountId",
                schema: "crm",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_ClientAccounts_ClientAccountId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_ClientAccounts_ClientAccountId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_CompanyContacts_CompanyContactId",
                schema: "crm",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_CompanyContacts_CompanyContactId",
                schema: "crm",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_RelationshipContacts_CompanyContacts_CompanyContactId",
                schema: "crm",
                table: "RelationshipContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Materials_MaterialId",
                schema: "crm",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Opportunities_OpportunityId",
                schema: "crm",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Opportunities_OpportunityId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_Opportunities_OpportunityId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Opportunities_OpportunityId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_TenantCompanyRelationships_TenantCompanyRelationshipId",
                schema: "crm",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_TenantCompanyRelationships_TenantCompanyRelationshipId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_TenantCompanyRelationships_TenantCompanyRelationshipId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_TenantCompanyRelationships_TenantCompanyRelationshipId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Addresses_RegisteredAddressId",
                schema: "masterdata",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Companies_CompanyId",
                schema: "leads",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Emails_EmailId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_Leads_LeadId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Leads_LeadId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_ProcessDefinitions_ProcessDefinitionId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_ProcessDefinitions_ProcessDefinitionId",
                schema: "process",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_ProcessSteps_CurrentProcessStepId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_ProcessSteps_ProcessStepId",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_ProcessTasks_CurrentProcessTaskId",
                schema: "process",
                table: "ProcessInstances");

            migrationBuilder.DropTable(
                name: "Activities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "EmailRecipients",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "HandoffPacks",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "LeadContacts",
                schema: "leads");

            migrationBuilder.DropTable(
                name: "ProcessTransitions",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ClientAccounts",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "CompanyContacts",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Materials",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "Opportunities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "RelationshipContacts",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "TenantCompanyRelationships",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Emails",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "Leads",
                schema: "leads");

            migrationBuilder.DropTable(
                name: "ProcessDefinitions",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ProcessSteps",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ProcessTasks",
                schema: "process");

            migrationBuilder.DropTable(
                name: "ProcessInstances",
                schema: "process");
        }
    }
}
