param(
    [Parameter(Mandatory = $true)][Guid]$EmailId,
    [Parameter(Mandatory = $true)][string]$MailboxExternalId,
    [Guid]$OpportunityId
)

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
$body = @{
    mailboxExternalId = $MailboxExternalId
    opportunityId = if ($OpportunityId -eq [Guid]::Empty) { $null } else { $OpportunityId }
} | ConvertTo-Json
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Emails/$EmailId/ReconcileSent" -BodyJson $body |
    ConvertTo-Json -Depth 10
