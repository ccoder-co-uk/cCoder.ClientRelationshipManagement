param(
    [Parameter(Mandatory = $true)]
    [string]$LeadId,

    [Parameter(Mandatory = $true)]
    [string]$ProcessTaskId,

    [Parameter(Mandatory = $true)]
    [string]$SectionKey,

    [Parameter(Mandatory = $true)]
    [string]$OutcomeKey,

    [string]$Finding,
    [string]$FindingPath
)

if ($FindingPath) {
    $Finding = Get-Content -Path $FindingPath -Raw
}

if ([string]::IsNullOrWhiteSpace($Finding)) {
    throw "Provide either -Finding or -FindingPath."
}

$invokeApi = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$findingBody = @{
    sectionKey = $SectionKey
    finding = $Finding
} | ConvertTo-Json -Depth 8

$findingResult = & $invokeApi -Method POST -Path "/Api/AgentWorkflow/Leads/$LeadId/ResearchFindings" -BodyJson $findingBody

$completionBody = @{
    outcomeKey = $OutcomeKey
    completionNote = $Finding
} | ConvertTo-Json -Depth 8

$completionResult = & $invokeApi -Method POST -Path "/Api/AgentWorkflow/Tasks/$ProcessTaskId/Complete" -BodyJson $completionBody

[ordered]@{
    finding = $findingResult
    completion = $completionResult
} | ConvertTo-Json -Depth 8
