SET NOCOUNT ON;

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
    [ClientAgentConcurrency],
    [LastUpdated]
FROM [crm].[AgentAutomationSettings]
ORDER BY [UserId], [Id];
