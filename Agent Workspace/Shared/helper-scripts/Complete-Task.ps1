param(
    [Parameter(Mandatory = $true)]
    [string]$ProcessTaskId,

    [string]$PayloadPath,
    [string]$OutcomeKey,
    [string]$CompletionNote,
    [string]$CompletionNotePath
)

if ($PayloadPath) {
    $body = Get-Content -Path $PayloadPath -Raw
}
else {
    if ($CompletionNotePath) {
        $CompletionNote = Get-Content -Path $CompletionNotePath -Raw
    }

    if ([string]::IsNullOrWhiteSpace($OutcomeKey) -or [string]::IsNullOrWhiteSpace($CompletionNote)) {
        throw "Provide either -PayloadPath or both -OutcomeKey and a completion note value."
    }

    $body = @{
        outcomeKey = $OutcomeKey
        completionNote = $CompletionNote
    } | ConvertTo-Json -Depth 8
}

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Tasks/$ProcessTaskId/Complete" -BodyJson $body | ConvertTo-Json -Depth 8
