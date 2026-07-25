param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyNumber,

    [string]$CompanyName
)

$ErrorActionPreference = 'Stop'
$mutualsCsvUrl = 'https://fcastoragemprprod.blob.core.windows.net/societylist/SocietyList.csv'
$mutualsRegisterUrl = 'https://mutuals.fca.org.uk/home/'
$companiesHouseBaseUrl = 'https://find-and-update.company-information.service.gov.uk/company/'
$requestHeaders = @{
    'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126 Safari/537.36'
    'Accept-Language' = 'en-GB,en;q=0.9'
}

function Normalize-CompanyName([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    $normalized = $Value.ToUpperInvariant() -replace '\bLTD\b', 'LIMITED'
    return ($normalized -replace '[^A-Z0-9]', '')
}

function ConvertTo-Text([object]$Content) {
    if ($Content -is [byte[]]) { return [Text.Encoding]::UTF8.GetString($Content) }
    return [string]$Content
}

$normalizedNumber = ($CompanyNumber -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
$numericPartText = ([regex]::Match($normalizedNumber, '\d+')).Value
$numericPart = if ([string]::IsNullOrWhiteSpace($numericPartText)) { $null } else { [int64]$numericPartText }
$normalizedName = Normalize-CompanyName $CompanyName
$looksLikeMutual = $normalizedNumber -match '^(IP|RS|SP|NP|NO|CU|F|FS|WS|WI)'

if (-not $looksLikeMutual) {
    try {
        $companiesHouseUrl = $companiesHouseBaseUrl + [uri]::EscapeDataString($normalizedNumber)
        $response = Invoke-WebRequest -Uri $companiesHouseUrl -UseBasicParsing -Headers $requestHeaders -TimeoutSec 20
        $title = [System.Net.WebUtility]::HtmlDecode((([regex]::Match($response.Content, '<title[^>]*>(?<value>[\s\S]*?)</title>', 'IgnoreCase')).Groups['value'].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
        $text = [System.Net.WebUtility]::HtmlDecode(($response.Content -replace '<script[\s\S]*?</script>', ' ' -replace '<style[\s\S]*?</style>', ' ' -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
        if ($text -match 'Company type\s+Registered society') {
            $looksLikeMutual = $true
        }
        elseif ($text -match 'Company status\s+(?<status>Active|Dissolved|Liquidation|Closed|Converted / Closed|Removed)\b') {
            $status = $Matches['status']
            $dissolvedOn = $null
            if ($text -match 'Dissolved on\s+(?<date>\d{1,2}\s+[A-Za-z]+\s+\d{4})') {
                $parsedDate = [DateTimeOffset]::MinValue
                if ([DateTimeOffset]::TryParse($Matches['date'], [Globalization.CultureInfo]::GetCultureInfo('en-GB'), [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$parsedDate)) {
                    $dissolvedOn = $parsedDate.ToString('o')
                }
            }

            [ordered]@{
                matched = $true
                companyNumber = $normalizedNumber
                companyName = $title -replace '\s+overview.*$', ''
                status = if ($status -eq 'Active') { 'active' } else { 'inactive' }
                registryStatus = $status
                deregisteredOn = $dissolvedOn
                sourceUrl = $companiesHouseUrl
                authority = 'Companies House'
            } | ConvertTo-Json -Depth 4
            exit 0
        }
    }
    catch {
        # Fall through to the Mutuals register only for identifiers that may be mutuals.
    }
}

if ($looksLikeMutual -or $numericPart -ne $null) {
    $response = Invoke-WebRequest -Uri $mutualsCsvUrl -UseBasicParsing -Headers $requestHeaders -TimeoutSec 30
    $rows = ConvertTo-Text $response.Content | ConvertFrom-Csv
    $matches = @($rows | Where-Object {
        $numberMatches = $numericPart -ne $null -and [int64]$_['Society Number'] -eq $numericPart
        $nameMatches = $normalizedName -ne '' -and (Normalize-CompanyName $_.'Society Name') -eq $normalizedName
        $numberMatches -and ($normalizedName -eq '' -or $nameMatches)
    })

    if ($matches.Count -eq 0 -and $normalizedName -ne '') {
        $matches = @($rows | Where-Object { (Normalize-CompanyName $_.'Society Name') -eq $normalizedName })
    }

    if ($matches.Count -eq 1) {
        $match = $matches[0]
        $registryStatus = [string]$match.'Society Status'
        [ordered]@{
            matched = $true
            companyNumber = [string]$match.'Full Registation Number'
            companyName = [string]$match.'Society Name'
            status = if ($registryStatus -match '^(Registered|Active)$') { 'active' } elseif ($registryStatus -match 'Deregistered|Cancelled|Dissolved|Closed|Inactive') { 'inactive' } else { 'unconfirmed' }
            registryStatus = $registryStatus
            deregisteredOn = if ([string]::IsNullOrWhiteSpace([string]$match.'Deregistration Date')) { $null } else { ([DateTimeOffset]::Parse([string]$match.'Deregistration Date')).ToString('o') }
            registeredAddress = [string]$match.'Society Address'
            sourceUrl = $mutualsCsvUrl
            authorityUrl = $mutualsRegisterUrl
            authority = 'Financial Conduct Authority Mutuals Public Register'
        } | ConvertTo-Json -Depth 4
        exit 0
    }
}

[ordered]@{
    matched = $false
    companyNumber = $normalizedNumber
    companyName = $CompanyName
    status = 'unconfirmed'
    sourceUrl = $null
    authority = $null
} | ConvertTo-Json -Depth 4
