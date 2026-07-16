param(
    [Parameter(Mandatory = $true)]
    [Guid]$MessageId,

    [Parameter(Mandatory = $true)]
    [string]$Subject,

    [Parameter(Mandatory = $true)]
    [string]$Body,

    [string]$ToAddresses,
    [string]$ApprovalTitle,
    [string]$ApprovalBody
)

$payload = @{
    subject = $Subject
    body = $Body
    toAddresses = $ToAddresses
    approvalTitle = $ApprovalTitle
    approvalBody = $ApprovalBody
} | ConvertTo-Json -Depth 6

$scriptPath = Join-Path $PSScriptRoot "Invoke-CrmApi.ps1"
& $scriptPath -Method POST -Path "/Api/AgentWorkflow/Messages/$MessageId/ReplacementEmailDraft" -BodyJson $payload | ConvertTo-Json -Depth 8
