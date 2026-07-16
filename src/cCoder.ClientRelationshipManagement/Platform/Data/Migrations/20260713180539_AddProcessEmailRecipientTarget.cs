using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessEmailRecipientTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailRecipientTarget",
                schema: "process",
                table: "ProcessSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailRecipientTarget",
                schema: "process",
                table: "ProcessSteps");
        }
    }
}
