SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Safety gate: this reviewed script is read-only unless an operator explicitly
-- changes this value to 1 for an approved deployment.
DECLARE @ApplyChanges bit = 0;

BEGIN TRANSACTION;

SELECT
    [Id],
    [UserId],
    [LeadAiProfileKey],
    [LeadAiModel],
    [LeadAgentConcurrency],
    [OpportunityAiProfileKey],
    [OpportunityAiModel],
    [OpportunityAgentConcurrency],
    [ClientAiProfileKey],
    [ClientAiModel],
    [ClientAgentConcurrency]
FROM [crm].[AgentAutomationSettings]
ORDER BY [UserId], [Id];

IF @ApplyChanges = 1
BEGIN
    UPDATE [crm].[AgentAutomationSettings]
    SET
        [LeadAiProfileKey] = N'none',
        [LeadAiModel] = N'',
        [LeadAgentConcurrency] = 1,
        [OpportunityAiProfileKey] = N'none',
        [OpportunityAiModel] = N'',
        [OpportunityAgentConcurrency] = 1,
        [ClientAiProfileKey] = N'none',
        [ClientAiModel] = N'',
        [ClientAgentConcurrency] = 1
    WHERE
        ISNULL([LeadAiProfileKey], N'') <> N'none'
        OR ISNULL([LeadAiModel], N'') <> N''
        OR [LeadAgentConcurrency] <> 1
        OR ISNULL([OpportunityAiProfileKey], N'') <> N'none'
        OR ISNULL([OpportunityAiModel], N'') <> N''
        OR [OpportunityAgentConcurrency] <> 1
        OR ISNULL([ClientAiProfileKey], N'') <> N'none'
        OR ISNULL([ClientAiModel], N'') <> N''
        OR [ClientAgentConcurrency] <> 1;

    SELECT @@ROWCOUNT AS [RowsUpdated];
    COMMIT TRANSACTION;
END
ELSE
BEGIN
    SELECT CAST(0 AS int) AS [RowsUpdated], N'Preview only; no changes applied.' AS [Status];
    ROLLBACK TRANSACTION;
END;
