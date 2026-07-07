param(
    [Parameter(Mandatory = $true)]
    [string]$LeadId,

    [string]$PayloadPath,
    [string]$RawCompanyName,
    [string]$RawTradingName,
    [string]$RawWebsiteUrl,
    [string]$RawContactEmailAddress,
    [string]$RawContactPhoneNumber,
    [string]$RawAddressText,
    [string]$QualificationNotes,
    [string]$QualificationNotesPath
)

if ($PayloadPath) {
    $body = Get-Content -Path $PayloadPath -Raw
}
else {
    if ($QualificationNotesPath) {
        $QualificationNotes = Get-Content -Path $QualificationNotesPath -Raw
    }

    $payload = [ordered]@{}

    if ($null -ne $RawCompanyName -and $RawCompanyName -ne '') { $payload.rawCompanyName = $RawCompanyName }
    if ($null -ne $RawTradingName -and $RawTradingName -ne '') { $payload.rawTradingName = $RawTradingName }
    if ($null -ne $RawWebsiteUrl -and $RawWebsiteUrl -ne '') { $payload.rawWebsiteUrl = $RawWebsiteUrl }
    if ($null -ne $RawContactEmailAddress -and $RawContactEmailAddress -ne '') { $payload.rawContactEmailAddress = $RawContactEmailAddress }
    if ($null -ne $RawContactPhoneNumber -and $RawContactPhoneNumber -ne '') { $payload.rawContactPhoneNumber = $RawContactPhoneNumber }
    if ($null -ne $RawAddressText -and $RawAddressText -ne '') { $payload.rawAddressText = $RawAddressText }
    if ($null -ne $QualificationNotes -and $QualificationNotes -ne '') { $payload.qualificationNotes = $QualificationNotes }

    if ($payload.Count -eq 0) {
        throw "Provide either -PayloadPath or at least one explicit lead research value."
    }

    $body = $payload | ConvertTo-Json -Depth 8
}

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Leads/$LeadId/Research" -BodyJson $body | ConvertTo-Json -Depth 8
