param(
    [int]$Limit = 25,
    [Guid]$ProcessTaskId = [Guid]::Empty
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$path = "/Api/AgentWorkflow/Tasks/Due?limit=$Limit"
if ($ProcessTaskId -ne [Guid]::Empty) {
    $path += "&processTaskId=$ProcessTaskId"
}

& $scriptPath -Method GET -Path $path | ConvertTo-Json -Depth 8
