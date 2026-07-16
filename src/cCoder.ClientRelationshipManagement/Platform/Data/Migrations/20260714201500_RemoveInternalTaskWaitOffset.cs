using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cCoder.ClientRelationshipManagement.Platform.Data.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260714201500_RemoveInternalTaskWaitOffset")]
public sealed class RemoveInternalTaskWaitOffset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [process].[ProcessSteps]
            SET [DueAfterDays] = 0,
                [DueAfterHours] = 0,
                [LastUpdatedBy] = N'system',
                [LastUpdated] = SYSUTCDATETIME()
            WHERE [Key] = N'follow-up-call';

            UPDATE task
            SET task.[DueOn] = task.[CreatedOn],
                task.[LastUpdatedBy] = N'system',
                task.[LastUpdated] = SYSUTCDATETIME()
            FROM [process].[ProcessTasks] AS task
            INNER JOIN [process].[ProcessSteps] AS step
                ON step.[Id] = task.[ProcessStepId]
            WHERE task.[State] = 0
              AND step.[Key] = N'follow-up-call';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is an operational scheduling correction. Restoring an artificial wait
        // could delay live work, so rollback deliberately leaves the corrected data.
    }
}
