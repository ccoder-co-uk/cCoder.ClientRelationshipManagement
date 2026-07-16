param(
    [Parameter(Mandatory = $true)][Guid]$EmailId,
    [Parameter(Mandatory = $true)][string]$Reason
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$body = @{ reason = $Reason } | ConvertTo-Json
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Emails/$EmailId/CancelUnverified" -BodyJson $body |
    ConvertTo-Json -Depth 10
