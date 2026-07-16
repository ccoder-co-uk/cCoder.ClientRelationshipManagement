using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxEvidenceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailboxMessageRecords",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    InternetMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InReplyTo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    References = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ToAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CcAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantCompanyRelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxMessageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxMessageRecords_CompanyContacts_CompanyContactId",
                        column: x => x.CompanyContactId,
                        principalSchema: "masterdata",
                        principalTable: "CompanyContacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MailboxMessageRecords_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "crm",
                        principalTable: "Opportunities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MailboxMessageRecords_TenantCompanyRelationships_TenantCompanyRelationshipId",
                        column: x => x.TenantCompanyRelationshipId,
                        principalSchema: "crm",
                        principalTable: "TenantCompanyRelationships",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_CompanyContactId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "CompanyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_ConversationId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_ExternalId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_FromAddress_ReceivedOn",
                schema: "crm",
                table: "MailboxMessageRecords",
                columns: new[] { "FromAddress", "ReceivedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_InternetMessageId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "InternetMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_OpportunityId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_ReceivedOn",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "ReceivedOn");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxMessageRecords_TenantCompanyRelationshipId",
                schema: "crm",
                table: "MailboxMessageRecords",
                column: "TenantCompanyRelationshipId");

            migrationBuilder.Sql(
                """
                INSERT INTO [process].[ProcessTransitions]
                    ([Id], [ProcessStepId], [NextProcessStepId], [OutcomeKey], [OutcomeLabel],
                     [IsDefaultOutcome], [IsTerminal], [Effect], [ResultingRelationshipStatus],
                     [ResultingSalesStage], [ResultingClientAccountStatus], [CreatedBy],
                     [LastUpdatedBy], [CreatedOn], [LastUpdated])
                SELECT NEWID(), followUp.[Id], review.[Id], N'await-response', N'Awaiting response',
                       0, 0, 0, NULL, NULL, NULL, N'migration:mailbox-evidence',
                       N'migration:mailbox-evidence', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                FROM [process].[ProcessDefinitions] definition
                INNER JOIN [process].[ProcessSteps] followUp
                    ON followUp.[ProcessDefinitionId] = definition.[Id]
                    AND followUp.[Key] = N'follow-up-call'
                INNER JOIN [process].[ProcessSteps] review
                    ON review.[ProcessDefinitionId] = definition.[Id]
                    AND review.[Key] = N'review-response'
                WHERE definition.[Name] = N'Opportunity Conversion'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [process].[ProcessTransitions] existing
                      WHERE existing.[ProcessStepId] = followUp.[Id]
                        AND existing.[OutcomeKey] = N'await-response');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [process].[ProcessTransitions]
                WHERE [CreatedBy] = N'migration:mailbox-evidence'
                  AND [OutcomeKey] = N'await-response';
                """);

            migrationBuilder.DropTable(
                name: "MailboxMessageRecords",
                schema: "crm");
        }
    }
}
