param(
    [Parameter(Mandatory = $true)][string]$MessageId,
    [Parameter(Mandatory = $true)][string]$Body
)

$payload = @{ body = $Body } | ConvertTo-Json
$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Messages/$MessageId/Entries" -BodyJson $payload | ConvertTo-Json -Depth 8
