using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Emails",
                schema: "CRM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentToContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FromDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FromEmailAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReplyToAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ToAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CcAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BccAddresses = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApprovedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ScheduledSendTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSendAttemptOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SendFailureCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Emails_ClientContacts_SentToContactId",
                        column: x => x.SentToContactId,
                        principalSchema: "CRM",
                        principalTable: "ClientContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_ClientMaterials_ClientMaterialId",
                        column: x => x.ClientMaterialId,
                        principalSchema: "CRM",
                        principalTable: "ClientMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Emails_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "CRM",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Emails_ClientId",
                schema: "CRM",
                table: "Emails",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_ClientMaterialId",
                schema: "CRM",
                table: "Emails",
                column: "ClientMaterialId",
                unique: true,
                filter: "[ClientMaterialId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_ScheduledSendTimeUtc",
                schema: "CRM",
                table: "Emails",
                column: "ScheduledSendTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_SentToContactId",
                schema: "CRM",
                table: "Emails",
                column: "SentToContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_State",
                schema: "CRM",
                table: "Emails",
                column: "State");

            migrationBuilder.Sql(
                """
                INSERT INTO [CRM].[Emails]
                (
                    [Id],
                    [ClientId],
                    [ClientMaterialId],
                    [SentToContactId],
                    [SenderUserId],
                    [FromDisplayName],
                    [FromEmailAddress],
                    [ReplyToAddresses],
                    [ToAddresses],
                    [CcAddresses],
                    [BccAddresses],
                    [Subject],
                    [BodyHtml],
                    [BodyText],
                    [IsBodyHtml],
                    [State],
                    [ApprovedOn],
                    [ApprovedBy],
                    [ScheduledSendTimeUtc],
                    [LastSendAttemptOn],
                    [SentOn],
                    [ExternalMessageId],
                    [LastError],
                    [SendFailureCount],
                    [CreatedBy],
                    [LastUpdatedBy],
                    [CreatedOn],
                    [LastUpdated]
                )
                SELECT
                    NEWID(),
                    [material].[ClientId],
                    [material].[Id],
                    [material].[SentToContactId],
                    COALESCE([material].[LastUpdatedBy], [material].[CreatedBy]),
                    NULL,
                    NULL,
                    NULL,
                    [contact].[EmailAddress],
                    NULL,
                    NULL,
                    COALESCE(NULLIF([material].[Name], N''), N'CRM Email Draft'),
                    [material].[Notes],
                    [material].[Notes],
                    CAST(0 AS bit),
                    CASE
                        WHEN [material].[Status] = N'Sent' THEN N'Sent'
                        WHEN [material].[Status] = N'Ready' THEN N'Approved'
                        ELSE N'Draft'
                    END,
                    CASE
                        WHEN [material].[Status] IN (N'Ready', N'Sent') THEN [material].[LastUpdated]
                        ELSE NULL
                    END,
                    CASE
                        WHEN [material].[Status] IN (N'Ready', N'Sent') THEN [material].[LastUpdatedBy]
                        ELSE NULL
                    END,
                    NULL,
                    [material].[SentOn],
                    [material].[SentOn],
                    NULL,
                    NULL,
                    0,
                    [material].[CreatedBy],
                    [material].[LastUpdatedBy],
                    [material].[CreatedOn],
                    [material].[LastUpdated]
                FROM [CRM].[ClientMaterials] AS [material]
                LEFT JOIN [CRM].[ClientContacts] AS [contact]
                    ON [contact].[Id] = [material].[SentToContactId]
                WHERE [material].[Type] = N'Email'
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM [CRM].[Emails] AS [email]
                        WHERE [email].[ClientMaterialId] = [material].[Id]
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Emails",
                schema: "CRM");
        }
    }
}
