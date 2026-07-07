$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method GET -Path "/Api/AgentWorkflow/Processes/Metrics" | ConvertTo-Json -Depth 8
