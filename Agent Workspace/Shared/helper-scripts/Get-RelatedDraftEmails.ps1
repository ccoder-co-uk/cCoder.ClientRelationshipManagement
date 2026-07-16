param(
    [Parameter(Mandatory = $true)]
    [Alias("MessageId")]
    [Guid]$ConversationId
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method GET -Path "/Api/AgentWorkflow/Messages/$ConversationId/RelatedDraftEmails" | ConvertTo-Json -Depth 10
