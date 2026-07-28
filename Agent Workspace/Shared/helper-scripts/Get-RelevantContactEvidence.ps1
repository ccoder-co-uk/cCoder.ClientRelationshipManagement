param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [Parameter(Mandatory = $true)]
    [string]$CompanyNumber,

    [string]$TradingName,

    [string]$WebsiteUrl,

    [string]$KnownResourceUrlsJson
)

$ErrorActionPreference = 'Stop'
$resourcePackScript = Join-Path $PSScriptRoot 'Get-CompanyResourcePack.ps1'
$payload = & $resourcePackScript `
    -CompanyName $CompanyName `
    -CompanyNumber $CompanyNumber `
    -TradingName $TradingName `
    -WebsiteUrl $WebsiteUrl `
    -KnownResourceUrlsJson $KnownResourceUrlsJson `
    -ResearchGoal contact
$payloadText = (@($payload) | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
$safePayload = $payloadText -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]', ' '
'base64:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($safePayload))
