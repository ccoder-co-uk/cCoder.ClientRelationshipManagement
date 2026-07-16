param([int]$Limit = 25)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method GET -Path "/Api/AgentWorkflow/Messages?limit=$Limit" | ConvertTo-Json -Depth 10
