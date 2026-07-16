param(
    [Parameter(Mandatory = $true)]
    [string]$ProcessTaskId
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method GET -Path "/Api/AgentWorkflow/Tasks/$ProcessTaskId/EmailEvidence" | ConvertTo-Json -Depth 10
