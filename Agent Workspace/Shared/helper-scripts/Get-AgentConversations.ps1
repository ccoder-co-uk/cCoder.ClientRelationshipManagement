param(
    [int]$Limit = 25,
    [Guid]$ConversationId
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$path = "/Api/AgentWorkflow/Messages?limit=$Limit"
if ($ConversationId -ne [Guid]::Empty) {
    $path += "&conversationId=$ConversationId"
}
& $scriptPath -Method GET -Path $path | ConvertTo-Json -Depth 10
