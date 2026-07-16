using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260714205012_AddAgentImprovementConversations")]
public sealed class AddAgentImprovementConversations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentMessageEntries",
            schema: "crm",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgentMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                LastUpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentMessageEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_AgentMessageEntries_AgentMessages_AgentMessageId",
                    column: x => x.AgentMessageId,
                    principalSchema: "crm",
                    principalTable: "AgentMessages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentMessageEntries_AgentMessageId_CreatedOn",
            schema: "crm",
            table: "AgentMessageEntries",
            columns: new[] { "AgentMessageId", "CreatedOn" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AgentMessageEntries", schema: "crm");
}
