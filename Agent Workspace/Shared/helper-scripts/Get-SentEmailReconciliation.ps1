param(
    [int]$Days = 90,
    [int]$Limit = 250
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$from = [DateTimeOffset]::UtcNow.AddDays(-[Math]::Max(1, $Days)).ToString("O")
$encodedFrom = [Uri]::EscapeDataString($from)
& $scriptPath -Method GET -Path "/Api/AgentWorkflow/Mailbox/Sent/Reconciliation?from=$encodedFrom&limit=$Limit" |
    ConvertTo-Json -Depth 10
