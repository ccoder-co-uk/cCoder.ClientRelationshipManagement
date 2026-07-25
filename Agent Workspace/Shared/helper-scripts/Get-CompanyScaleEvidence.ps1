param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [Parameter(Mandatory = $true)]
    [string]$CompanyNumber,

    [string]$TradingName
)

$ErrorActionPreference = 'Stop'
$searchScript = Join-Path $PSScriptRoot 'Search-PublicWeb.ps1'
$pageScript = Join-Path $PSScriptRoot 'Get-PublicWebPage.ps1'
$searchName = if ([string]::IsNullOrWhiteSpace($TradingName)) { $CompanyName } else { $TradingName }
$cleanName = ($searchName -replace '[^A-Za-z0-9]+', ' ' -replace '\s+', ' ').Trim()
$cleanBaseName = ($cleanName -replace '(?i)\s+(?:LIMITED|LTD|PLC|LLP|INCORPORATED|INC|CORPORATION|CORP)$', '').Trim()
if ([string]::IsNullOrWhiteSpace($cleanBaseName)) { $cleanBaseName = $cleanName }

$queries = @(
    # This marker routes one bounded identity-and-scale lookup through the
    # reliable public provider. Search-PublicWeb removes the boilerplate words
    # before sending the terms, while retaining the original query in evidence.
    '"{0}" "{1}" turnover employees official website' -f $cleanBaseName, $CompanyNumber
    '"{0}" "{1}" turnover employees' -f $cleanBaseName, $CompanyNumber
    '"{0}" revenue employees' -f $cleanBaseName
    '"{0}" turnover headcount' -f $cleanBaseName
)
$searchResponses = @($queries | ForEach-Object -Parallel {
    try {
        $payload = (& $using:searchScript -Query $_ -Limit 10) | ConvertFrom-Json
        [pscustomobject]@{ query = $_; results = @($payload.results); error = $null }
    }
    catch {
        [pscustomobject]@{ query = $_; results = @(); error = $_.Exception.Message }
    }
} -ThrottleLimit 3)

$identityTokens = @($cleanBaseName.ToUpperInvariant() -split '[^A-Z0-9]+' |
    Where-Object { $_.Length -ge 3 -and $_ -notin @('THE', 'AND', 'COMPANY', 'GROUP') } |
    Select-Object -Unique)
$isGroupEntity = $cleanBaseName -match '(?i)\bGROUP\b'
$distinctiveIdentityToken = @($identityTokens | Where-Object { $_.Length -ge 5 } | Select-Object -First 1)[0]
$scoredResults = @(
    foreach ($response in $searchResponses) {
        foreach ($result in @($response.results)) {
            $url = [string]$result.url
            if ($url -notmatch '^https?://' -or $url -match 'linkedin\.com|facebook\.com|instagram\.com|tiktok\.com|company-information\.service\.gov\.uk') { continue }
            $haystack = (([string]$result.title) + ' ' + ([string]$result.snippet) + ' ' + $url).ToUpperInvariant()
            $score = 0
            foreach ($token in $identityTokens) { if ($haystack.Contains($token)) { $score += 12 } }
            if ($url -match 'globaldatabase\.com') { $score += 80 }
            elseif ($url -match 'endole\.co\.uk') { $score += 50 }
            elseif ($url -match 'dnb\.com|northdata\.com') { $score += 30 }
            if ($haystack -match 'TURNOVER|REVENUE|EMPLOY|HEADCOUNT|STAFF') { $score += 25 }
            [pscustomobject]@{ query = $response.query; title = [string]$result.title; url = $url; snippet = [string]$result.snippet; score = $score }
        }
    }
)
$seenUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$rankedResults = @($scoredResults | Sort-Object score -Descending | Where-Object { $seenUrls.Add($_.url) } | Select-Object -First 8)
$companySlug = (($cleanName.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-'))
$pageUrls = @(
    if (-not [string]::IsNullOrWhiteSpace($CompanyNumber) -and -not [string]::IsNullOrWhiteSpace($companySlug)) {
        "https://paymentcheck.co.uk/company-search/$CompanyNumber/$companySlug"
        "https://uk.globaldatabase.com/company/$companySlug"
    }
    $rankedResults.url
) | Select-Object -Unique -First 10
$pages = @($pageUrls | ForEach-Object -Parallel {
    try {
        (& $using:pageScript -Url $_ -MaximumCharacters 24000) | ConvertFrom-Json
    }
    catch { $null }
} -ThrottleLimit 8 | Where-Object { $_ -ne $null })

function Convert-ScaleNumber {
    param([string]$Amount, [string]$Unit)
    $number = [decimal]::Parse(($Amount -replace ',', ''), [Globalization.CultureInfo]::InvariantCulture)
    switch -Regex ($Unit) {
        '^(?i:k|thousand)$' { return $number * 1000 }
        '^(?i:m|million)$' { return $number * 1000000 }
        '^(?i:bn|billion)$' { return $number * 1000000000 }
        default { return $number }
    }
}

$annualRevenue = $null
$revenueCurrency = $null
$turnoverSourceUrl = $null
$employeeCount = $null
$employeeSourceUrl = $null
foreach ($page in $pages) {
    $text = [string]$page.text
    if ([string]::IsNullOrWhiteSpace($text)) { continue }
    $identityHaystack = ($text + ' ' + [string]$page.url).ToUpperInvariant()
    $matchedTokens = @($identityTokens | Where-Object { $identityHaystack.Contains($_) }).Count
    $hasExactCompanyNumber = -not [string]::IsNullOrWhiteSpace($CompanyNumber) -and
        $identityHaystack -match [regex]::Escape($CompanyNumber.ToUpperInvariant())
    # A group-level lead may buy and operate under the public group brand while
    # its registry subsidiary number is absent from press and profile pages.
    # Permit that bounded fallback only for an explicitly named GROUP entity
    # and a distinctive brand token; ordinary companies still require their
    # exact registration number.
    $hasGroupBrandIdentity = $isGroupEntity -and
        -not [string]::IsNullOrWhiteSpace($distinctiveIdentityToken) -and
        $identityHaystack -match ('\b' + [regex]::Escape($distinctiveIdentityToken) + '\b')
    if (-not $hasExactCompanyNumber -and -not $hasGroupBrandIdentity) { continue }
    if ($identityTokens.Count -gt 0 -and $matchedTokens -eq 0) { continue }

    $companyText = [regex]::Split($text, '(?i)\b(?:competitors|similar companies)\s*:')[0]
    if ($null -eq $employeeCount) {
        $employeeMatch = [regex]::Match(
            $companyText,
            '(?i)(?:which|currently|now)?\s*employs?\s+(?<count>\d[\d,]{0,9})\s+(?:people|employees)|(?:total|number\s+of)\s+employees\s+(?<count2>\d[\d,]{0,9})|there\s+are\s+currently\s+(?<count3>\d[\d,]{0,9})\s+(?:of\s+)?employees|employee\s+count\s+(?<count4>\d[\d,]{0,9})')
        $employeeValue = @($employeeMatch.Groups['count'].Value, $employeeMatch.Groups['count2'].Value, $employeeMatch.Groups['count3'].Value, $employeeMatch.Groups['count4'].Value) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($employeeValue)) {
            $parsedEmployeeCount = 0
            if ([int]::TryParse(($employeeValue -replace ',', ''), [ref]$parsedEmployeeCount) -and $parsedEmployeeCount -ge 0) {
                $employeeCount = $parsedEmployeeCount
                $employeeSourceUrl = [string]$page.url
            }
        }
    }

    if ($null -eq $annualRevenue) {
        $revenueMatch = [regex]::Match(
            $companyText,
            '(?i)(?:annual\s+)?(?:turnover|revenue)(?:\s+(?:of|is|was|reported|reached|stands\s+at))?\s*[:=]?\s*(?<currency>£|GBP|\$|USD|€|EUR)?\s*(?<amount>\d[\d,]*(?:\.\d+)?)\s*(?<unit>thousand|million|billion|k|m|bn)?\b')
        if ($revenueMatch.Success) {
            $parsedRevenue = Convert-ScaleNumber -Amount $revenueMatch.Groups['amount'].Value -Unit $revenueMatch.Groups['unit'].Value
            $grossProfitMatch = [regex]::Match(
                $companyText,
                '(?i)gross[\s-]+profit(?:\s+(?:of|is|was|reported|stands\s+at))?\s*[:=]?\s*(?<currency>£|GBP|\$|USD|€|EUR)?\s*(?<amount>\d[\d,]*(?:\.\d+)?)\s*(?<unit>thousand|million|billion|k|m|bn)?\b')
            if ($grossProfitMatch.Success) {
                $parsedGrossProfit = Convert-ScaleNumber -Amount $grossProfitMatch.Groups['amount'].Value -Unit $grossProfitMatch.Groups['unit'].Value
                # Gross profit materially above reported turnover indicates a
                # malformed/partial directory field. Do not let that bad value
                # force a large employer through the micro-company route.
                if ($parsedGrossProfit -gt ($parsedRevenue * 2)) { continue }
            }
            if ($parsedRevenue -ge 100000) {
                $annualRevenue = $parsedRevenue
                $currencyMarker = $revenueMatch.Groups['currency'].Value.ToUpperInvariant()
                $revenueCurrency = if ($currencyMarker -in @('$', 'USD')) { 'USD' } elseif ($currencyMarker -in @('€', 'EUR')) { 'EUR' } else { 'GBP' }
                $turnoverSourceUrl = [string]$page.url
            }
        }
    }
}

$payload = [ordered]@{
    companyName = $CompanyName
    companyNumber = $CompanyNumber
    annualRevenue = $annualRevenue
    revenueCurrency = $revenueCurrency
    turnoverSourceUrl = $turnoverSourceUrl
    employeeCount = $employeeCount
    employeeSourceUrl = $employeeSourceUrl
    searches = @($searchResponses | Select-Object query, error)
    pagesInspected = @($pages.url)
} | ConvertTo-Json -Depth 7
$safePayload = $payload -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]', ' '
'base64:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($safePayload))
