param(
    [Parameter(Mandatory = $true)]
    [string]$PayloadPath
)

$body = Get-Content -Path $PayloadPath -Raw
$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Messages" -BodyJson $body | ConvertTo-Json -Depth 8
