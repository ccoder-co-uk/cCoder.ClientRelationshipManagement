param(
    [Parameter(Mandatory = $true)]
    [Alias("MessageId")]
    [Guid]$ConversationId
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Messages/$ConversationId/RefreshRelatedDraftEmails" | ConvertTo-Json -Depth 10
