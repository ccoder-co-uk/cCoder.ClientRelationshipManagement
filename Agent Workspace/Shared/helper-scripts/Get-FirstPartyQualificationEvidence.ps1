param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [Parameter(Mandatory = $true)]
    [string]$CompanyNumber,

    [string]$TradingName,

    [string]$WebsiteUrl,

    [ValidateRange(30, 180)]
    [int]$MaxElapsedSeconds = 90
)

$ErrorActionPreference = 'Stop'
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$searchScript = Join-Path $PSScriptRoot 'Search-PublicWeb.ps1'
$pageScript = Join-Path $PSScriptRoot 'Get-PublicWebPage.ps1'
$excludedHostPattern = '(?i)(?:companieshouse|company-information\.service\.gov\.uk|companiesintheuk\.co\.uk|companycheck\.co\.uk|companypulse\.co\.uk|gbrbusiness\.com|gbcomp\.p-o\.co\.uk|synta-iq\.com|globaldatabase\.com|endole\.co\.uk|dnb\.com|craft\.co|companieshistory\.com|linkedin\.com|facebook\.com|instagram\.com|youtube\.com|wikipedia\.org|bloomberg\.com|marketscreener\.com|rocketreach\.co|contactout\.com|zoominfo\.com)'

function Test-Budget {
    param([int]$ReserveSeconds = 0)
    $stopwatch.Elapsed.TotalSeconds -lt [Math]::Max(0, $MaxElapsedSeconds - $ReserveSeconds)
}

function Convert-ToRoot {
    param([string]$Value)
    $candidate = $null
    if (-not [uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$candidate) -or
        $candidate.Scheme -notin @('http', 'https') -or
        $candidate.IsLoopback -or
        $candidate.Host -match $excludedHostPattern -or
        $candidate.Host -match '^(?:10\.|127\.|169\.254\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)') {
        return $null
    }
    $candidate.GetLeftPart([UriPartial]::Authority)
}

function Read-Page {
    param([string]$Url, [int]$MaximumCharacters = 14000)
    try {
        $page = (& $pageScript -Url $Url -MaximumCharacters $MaximumCharacters -ContextTerms $contextTerms) | ConvertFrom-Json
        [pscustomobject]@{
            url = [string]$page.url
            title = [string]$page.title
            emails = @($page.emails)
            phones = @($page.phones | Select-Object -First 8)
            links = @($page.links)
            excerpt = [string]$page.text
        }
    }
    catch { $null }
}

$legalBaseName = ($CompanyName -replace '(?i)\s+(?:LIMITED|LTD|PLC|P\s*\.?\s*L\s*\.?\s*C|LLP|INCORPORATED|INC|CORPORATION|CORP)\.?$', '').Trim()
$displayName = if ([string]::IsNullOrWhiteSpace($TradingName)) { $legalBaseName } else { $TradingName.Trim() }
$searchName = ($displayName -replace '&', ' and ' -replace '[^A-Za-z0-9]+', ' ' -replace '\s+', ' ').Trim()
$contextTerms = @($CompanyName, $legalBaseName, $TradingName, $displayName) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique
$identityTokens = @(($legalBaseName + ' ' + $TradingName).ToUpperInvariant() -split '[^A-Z0-9]+' |
    Where-Object { $_.Length -ge 3 -and $_ -notin @('THE', 'AND', 'FOR', 'LIMITED', 'LTD', 'PLC', 'GROUP', 'HOLDINGS', 'COMPANY') } |
    Select-Object -Unique)

$identityQueries = @(
    '"{0}" "{1}" official website' -f $searchName, $CompanyNumber
    '"{0}" official corporate website' -f $searchName
) | Select-Object -Unique
$identitySearches = @(
    foreach ($query in $identityQueries) {
        if (-not (Test-Budget -ReserveSeconds 30)) { break }
        try {
            $payload = (& $searchScript -Query $query -Limit 8 -PreferHighQuality) | ConvertFrom-Json
            [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
        }
        catch {
            [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
        }
    }
)

$providedRoot = Convert-ToRoot $WebsiteUrl
$candidateRoots = @(
    $providedRoot
    foreach ($search in $identitySearches) {
        foreach ($result in @($search.results)) {
            Convert-ToRoot ([string]$result.url)
        }
    }
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique -First 6

$probeUrls = @(
    foreach ($root in $candidateRoots) {
        $root
        "$root/about"
        "$root/about-us"
        "$root/contact"
        "$root/privacy"
        "$root/legal"
    }
) | Select-Object -Unique -First 30
$probePages = @(
    if (Test-Budget -ReserveSeconds 22) {
        $probeUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 9000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 8)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 10 | Where-Object { $null -ne $_ }
    }
)

$preferredDiscoveryRoots = @(
    foreach ($search in $identitySearches) {
        foreach ($result in @($search.results)) {
            $resultRoot = Convert-ToRoot ([string]$result.url)
            if (-not [string]::IsNullOrWhiteSpace($resultRoot)) {
                $resultRoot
                break
            }
        }
    }
) | Select-Object -Unique

$rootScores = @(
    foreach ($root in $candidateRoots) {
        $rootUri = [uri]$root
        $pages = @($probePages | Where-Object {
            $pageUri = $null
            [uri]::TryCreate([string]$_.url, [UriKind]::Absolute, [ref]$pageUri) -and
                (($pageUri.Host -replace '^www\.', '') -eq ($rootUri.Host -replace '^www\.', ''))
        })
        $searchEvidence = @(
            foreach ($search in $identitySearches) {
                foreach ($result in @($search.results)) {
                    $resultUri = $null
                    if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$resultUri) -and
                        (($resultUri.Host -replace '^www\.', '') -eq ($rootUri.Host -replace '^www\.', ''))) {
                        [string]$result.title + ' ' + [string]$result.snippet
                    }
                }
            }
        )
        $pageHaystack = (@($pages | ForEach-Object { $_.title + ' ' + $_.excerpt }) -join ' ').ToUpperInvariant()
        $searchHaystack = ($searchEvidence -join ' ').ToUpperInvariant()
        $haystack = "$pageHaystack $searchHaystack"
        $compactPageHaystack = $pageHaystack -replace '[^A-Z0-9]', ''
        $compactLegalName = $legalBaseName.ToUpperInvariant() -replace '[^A-Z0-9]', ''
        $compactHost = ($rootUri.Host -replace '^www\.', '').ToUpperInvariant() -replace '[^A-Z0-9]', ''
        $hasCompanyNumber = -not [string]::IsNullOrWhiteSpace($CompanyNumber) -and $pageHaystack.Contains($CompanyNumber.ToUpperInvariant())
        $hasLegalName = $compactLegalName.Length -ge 5 -and $compactPageHaystack.Contains($compactLegalName)
        $matchedTokens = @($identityTokens | Where-Object { $pageHaystack -match ('\b' + [regex]::Escape($_) + '\b') }).Count
        $searchMatchedTokens = @($identityTokens | Where-Object { $searchHaystack -match ('\b' + [regex]::Escape($_) + '\b') }).Count
        $hostTokens = @($identityTokens | Where-Object { $compactHost.Contains($_) }).Count
        $operational = $pageHaystack -match '(?i)\b(?:products?|services?|customers?|suppliers?|investors?|manufactur\w*|retail|contact us|about us|our business)\b'
        $searchOperational = $searchHaystack -match '(?i)\b(?:company|corporate|products?|services?|customers?|suppliers?|investors?|manufactur\w*|retail|our business)\b'
        $isPreferredDiscoveryRoot = $preferredDiscoveryRoots -contains $root
        $isRecruitmentRoot = $rootUri.Host -match '(?i)^(?:jobs?|careers?|recruitment)\.'
        $strongIdentity = $hasCompanyNumber -or $hasLegalName -or
            ($hostTokens -gt 0 -and $matchedTokens -gt 0 -and $operational) -or
            ($isPreferredDiscoveryRoot -and $hostTokens -gt 0 -and $searchMatchedTokens -gt 0 -and $searchOperational)
        $score = $(if ($hasCompanyNumber) { 200 } else { 0 }) +
            $(if ($hasLegalName) { 140 } else { 0 }) +
            ($matchedTokens * 15) + ($hostTokens * 25) +
            $(if ($operational) { 30 } else { 0 }) +
            $(if ($root -eq $providedRoot) { 25 } else { 0 }) +
            $(if ($isPreferredDiscoveryRoot) { 300 } else { 0 }) -
            $(if ($isRecruitmentRoot) { 250 } else { 0 })
        [pscustomobject]@{ root = $root; score = $score; strongIdentity = $strongIdentity }
    }
)
$verifiedRoot = @($rootScores | Where-Object strongIdentity | Sort-Object score -Descending | Select-Object -First 1 -ExpandProperty root)[0]

if ([string]::IsNullOrWhiteSpace($verifiedRoot)) {
    $payload = [ordered]@{
        companyName = $CompanyName
        companyNumber = $CompanyNumber
        websiteUrl = $null
        identityVerified = $false
        pages = @()
        searches = @($identitySearches | Select-Object query, error)
        elapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
    } | ConvertTo-Json -Depth 6
    'base64:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($payload))
    exit 0
}

$verifiedUri = [uri]$verifiedRoot
$verifiedHost = ($verifiedUri.Host -replace '^www\.', '').ToLowerInvariant()
$hostParts = @($verifiedHost -split '\.')
$officialBaseDomain = if ($verifiedHost -match '(?i)\.(?:co|org|gov|ac)\.uk$' -and $hostParts.Count -ge 3) {
    ($hostParts | Select-Object -Last 3) -join '.'
} elseif ($hostParts.Count -ge 2) {
    ($hostParts | Select-Object -Last 2) -join '.'
} else {
    $verifiedHost
}
$officialPages = @($probePages | Where-Object {
    $pageUri = $null
    [uri]::TryCreate([string]$_.url, [UriKind]::Absolute, [ref]$pageUri) -and
        ((($pageUri.Host -replace '^www\.', '').ToLowerInvariant() -eq $officialBaseDomain) -or
            (($pageUri.Host -replace '^www\.', '').ToLowerInvariant().EndsWith('.' + $officialBaseDomain)))
})

$canonicalUrls = @(
    "$verifiedRoot/contact-us"
    "$verifiedRoot/investors"
    "$verifiedRoot/investors/contact"
    "$verifiedRoot/investors/contacts"
    "$verifiedRoot/investors/ir-contacts"
    "$verifiedRoot/investors/shareholder-centre/shareholder-contacts"
    "$verifiedRoot/investor-relations"
    "$verifiedRoot/investors/contact-us"
    "$verifiedRoot/suppliers"
    "$verifiedRoot/procurement"
    "$verifiedRoot/about-us/leadership"
    "$($verifiedUri.Scheme)://investors.$officialBaseDomain/"
    "$($verifiedUri.Scheme)://corporate.$officialBaseDomain/"
) | Where-Object { $_ -notin @($officialPages.url) } | Select-Object -Unique
$canonicalPages = @(
    if (Test-Budget -ReserveSeconds 18) {
        $canonicalUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 8)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 6 | Where-Object { $null -ne $_ }
    }
)
$canonicalHasEmail = @($canonicalPages.emails | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0
if (-not $canonicalHasEmail -and (Test-Budget -ReserveSeconds 10)) {
    $retryUrls = @(
        "$verifiedRoot/investors/ir-contacts"
        "$verifiedRoot/investors/contacts"
        "$($verifiedUri.Scheme)://investors.$officialBaseDomain/"
        "$verifiedRoot/investors"
        "$verifiedRoot/contact-us"
    ) | Select-Object -Unique
    foreach ($retryUrl in $retryUrls) {
        if (-not (Test-Budget -ReserveSeconds 8)) { break }
        $retryPage = Read-Page -Url $retryUrl -MaximumCharacters 16000
        if ($null -eq $retryPage) { continue }
        $canonicalPages += $retryPage
        if (@($retryPage.emails | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) { break }
    }
}
$officialPages = @($officialPages) + @($canonicalPages)

$siteQueries = @(
    "site:$officialBaseDomain email investor finance contact"
    "site:$verifiedHost annual report revenue employees suppliers"
    "site:$verifiedHost leadership finance procurement contact"
    "site:$verifiedHost customers partners case studies"
)
$siteSearches = @(
    foreach ($query in $siteQueries) {
        if (-not (Test-Budget -ReserveSeconds 16)) { break }
        try {
            $payload = (& $searchScript -Query $query -Limit 8) | ConvertFrom-Json
            [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
        }
        catch {
            [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
        }
    }
)

$officialSearchUrls = @(
    foreach ($search in $siteSearches) {
        foreach ($result in @($search.results)) {
            $resultUri = $null
            if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$resultUri) -and
                (((($resultUri.Host -replace '^www\.', '').ToLowerInvariant() -eq $officialBaseDomain)) -or
                    (($resultUri.Host -replace '^www\.', '').ToLowerInvariant().EndsWith('.' + $officialBaseDomain)))) {
                [string]$result.url
            }
        }
    }
) | Select-Object -Unique -First 12

$linkedUrls = @(
    foreach ($page in $officialPages) {
        foreach ($link in @($page.links)) {
            $linkUri = $null
            if (-not [uri]::TryCreate([string]$link, [UriKind]::Absolute, [ref]$linkUri)) { continue }
            $normalizedLinkHost = ($linkUri.Host -replace '^www\.', '').ToLowerInvariant()
            $sameHost = $normalizedLinkHost -eq $officialBaseDomain -or $normalizedLinkHost.EndsWith('.' + $officialBaseDomain)
            $usefulPath = $linkUri.AbsolutePath -match '(?i)(?:annual|report|results|investor|leadership|management|board|contact|supplier|procurement|customer|partner|case-stud|governance|about|who-we-are)'
            $usefulSubdomain = $normalizedLinkHost -match '(?i)(?:investor|annual|corporate|supplier|procurement)'
            if ($sameHost -and ($usefulPath -or $usefulSubdomain)) { $linkUri.AbsoluteUri }
        }
    }
) | Select-Object -Unique -First 16

$additionalUrls = @($officialSearchUrls) + @($linkedUrls) |
    Where-Object { $_ -notin @($officialPages.url) } |
    Select-Object -Unique -First 20
$additionalPages = @(
    if (Test-Budget -ReserveSeconds 8) {
        $additionalUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 8)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 8 | Where-Object { $null -ne $_ }
    }
)

$seenUrls = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$pages = @(
    foreach ($page in @($officialPages) + @($additionalPages)) {
        if ([string]::IsNullOrWhiteSpace([string]$page.url) -or -not $seenUrls.Add([string]$page.url)) { continue }
        $pageUri = $null
        if (-not [uri]::TryCreate([string]$page.url, [UriKind]::Absolute, [ref]$pageUri) -or
            -not ((($pageUri.Host -replace '^www\.', '').ToLowerInvariant() -eq $officialBaseDomain) -or
                (($pageUri.Host -replace '^www\.', '').ToLowerInvariant().EndsWith('.' + $officialBaseDomain)))) { continue }
        [ordered]@{
            url = [string]$page.url
            title = [string]$page.title
            emails = @($page.emails)
            phones = @($page.phones)
            excerpt = [string]$page.excerpt
        }
    }
) | Select-Object -First 24

$payload = [ordered]@{
    companyName = $CompanyName
    companyNumber = $CompanyNumber
    websiteUrl = "$verifiedRoot/"
    identityVerified = $true
    pages = @($pages)
    searches = @(@($identitySearches) + @($siteSearches) | Select-Object query, error)
    elapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
} | ConvertTo-Json -Depth 7
$safePayload = $payload -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]', ' '
'base64:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($safePayload))
