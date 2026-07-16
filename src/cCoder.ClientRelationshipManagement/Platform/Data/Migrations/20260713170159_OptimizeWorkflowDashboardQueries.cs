using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeWorkflowDashboardQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_State_DueOn",
                schema: "process",
                table: "ProcessTasks",
                columns: new[] { "State", "DueOn" })
                .Annotation("SqlServer:Include", new[] { "LeadId", "TenantCompanyRelationshipId", "ProcessStepId", "RenderedTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_State_ProcessDefinitionId",
                schema: "process",
                table: "ProcessInstances",
                columns: new[] { "State", "ProcessDefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_State_DueOn",
                schema: "process",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInstances_State_ProcessDefinitionId",
                schema: "process",
                table: "ProcessInstances");
        }
    }
}
