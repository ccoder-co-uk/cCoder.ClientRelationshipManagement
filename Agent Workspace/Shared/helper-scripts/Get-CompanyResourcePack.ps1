param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [Parameter(Mandatory = $true)]
    [string]$CompanyNumber,

    [string]$TradingName,

    [string]$WebsiteUrl,

    [string]$KnownResourceUrlsJson,

    [ValidateSet('general', 'contact', 'scale', 'address')]
    [string]$ResearchGoal = 'general',

    [ValidateRange(30, 300)]
    [int]$MaxElapsedSeconds = 150
)

$ErrorActionPreference = 'Stop'
$script:resourcePackStopwatch = [Diagnostics.Stopwatch]::StartNew()
$script:resourcePackMaxElapsedSeconds = $MaxElapsedSeconds

function Test-ResourcePackBudget {
    param([int]$ReserveSeconds = 0)

    $script:resourcePackStopwatch.Elapsed.TotalSeconds -lt
        [Math]::Max(0, $script:resourcePackMaxElapsedSeconds - $ReserveSeconds)
}

$searchScript = Join-Path $PSScriptRoot 'Search-PublicWeb.ps1'
$pageScript = Join-Path $PSScriptRoot 'Get-PublicWebPage.ps1'
$legalBaseName = ($CompanyName -replace '(?i)\s+(?:LIMITED|LTD|PLC|P\s*\.?\s*L\s*\.?\s*C|LLP|INCORPORATED|INC|CORPORATION|CORP)\.?$', '').Trim()
$displayName = if ([string]::IsNullOrWhiteSpace($TradingName)) { $legalBaseName } else { $TradingName.Trim() }
$searchDisplayName = ($displayName -replace '&', ' and ' -replace '[^A-Za-z0-9]+', ' ' -replace '\s+', ' ').Trim()
$companyAcronym = -join @($CompanyName -split '[^A-Za-z0-9]+' | Where-Object { $_ } | ForEach-Object { $_[0] })
$contextTerms = @($CompanyName, $legalBaseName, $TradingName, $displayName, $(if ($companyAcronym.Length -ge 2 -and $companyAcronym.Length -le 8) { $companyAcronym })) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique
$identityTokenExclusions = @(
    'THE', 'AND', 'FOR', 'LTD', 'PLC', 'LLP', 'INC',
    'LIMITED', 'COMPANY', 'HOLDINGS', 'GROUP', 'CONTRACT', 'CONTRACTS', 'CONTRACTOR', 'CONTRACTORS', 'SERVICES', 'SOLUTIONS', 'INTERNATIONAL',
    'ENGINEERING', 'MANUFACTURING', 'INDUSTRIES', 'INDUSTRIAL', 'CONSTRUCTION', 'LOGISTICS', 'TRANSPORT',
    'TRADING', 'ENTERPRISES', 'TECHNOLOGIES', 'TECHNOLOGY', 'SYSTEMS', 'PRODUCTS', 'CLEANING'
)
$identityTokens = @(($legalBaseName + ' ' + $TradingName).ToUpperInvariant() -split '[^A-Z0-9]+' |
    Where-Object { $_.Length -ge 3 -and $_ -notin @(
        $identityTokenExclusions) } |
    Select-Object -Unique)
$excludedHostPattern = '(?i)(?:linkedin\.com|facebook\.com|instagram\.com|rocketreach\.co|contactout\.com|leadiq\.com|signalhire\.com|aeroleads\.com|prospeo\.io|hunter\.io|apollo\.io|lusha\.com|zoominfo\.com)'
$nonOfficialHostPattern = '(?i)(?:gov\.uk|companieshouse|companycheck\.co\.uk|pappers\.co\.uk|companieshub\.co\.uk|companieslist\.co\.uk|companiesintheuk\.co\.uk|opengovuk\.com|clarity-project\.co\.uk|companypulse\.co\.uk|globaldatabase\.com|bizstats\.co\.uk|bizdb\.co\.uk|checkdirector\.co\.uk|firstreport\.co\.uk|bizseek\.co\.uk|callupcontact\.com|findglocal\.com|kompass\.com|cylex-uk\.co\.uk|192\.com|panjiva\.com|endole\.co\.uk|indeed\.com|indeed\.co\.uk|dnb\.com|bloomberg\.com|marketscreener\.com|wikipedia\.org|biblegateway\.com)'
$knownResourceUrls = @(
    if (-not [string]::IsNullOrWhiteSpace($KnownResourceUrlsJson)) {
        try {
            foreach ($knownUrl in @($KnownResourceUrlsJson | ConvertFrom-Json)) {
                $knownUri = $null
                if ([uri]::TryCreate([string]$knownUrl, [UriKind]::Absolute, [ref]$knownUri) -and
                    $knownUri.Scheme -in @('http', 'https') -and
                    -not $knownUri.IsLoopback -and
                    $knownUri.Host -notmatch '^(?:10\.|127\.|169\.254\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)' -and
                    $knownUri.Host -notmatch $excludedHostPattern) {
                    $knownUri.AbsoluteUri
                }
            }
        }
        catch {
            throw 'KnownResourceUrlsJson must be a JSON array of public HTTP or HTTPS URLs.'
        }
    }
) | Select-Object -Unique -First 12

$identityQuery = '{0} {1} official website' -f $searchDisplayName, $CompanyNumber
$identityResponse = try {
    $payload = (& $searchScript -Query $identityQuery -Limit 6 -PreferHighQuality) | ConvertFrom-Json
    [pscustomobject]@{ query = $identityQuery; results = @($payload.results); error = $null }
}
catch {
    [pscustomobject]@{ query = $identityQuery; results = @(); error = $_.Exception.Message }
}

$discoveryQueries = @(
    '"{0}" finance director managing director email' -f $searchDisplayName
    '"{0}" leadership team contact' -f $searchDisplayName
    '"{0}" filetype:pdf supplier annual report contact' -f $searchDisplayName
) | Select-Object -Unique
$discoveryResponses = @($discoveryQueries | ForEach-Object -Parallel {
    $query = $_
    try {
        $payload = (& $using:searchScript -Query $query -Limit 5) | ConvertFrom-Json
        [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
    }
    catch {
        [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
    }
} -ThrottleLimit 3)
$searchResponses = @($identityResponse) + @($discoveryResponses)

$providedRoot = $null
if (-not [string]::IsNullOrWhiteSpace($WebsiteUrl)) {
    $providedUri = $null
    if ([uri]::TryCreate($WebsiteUrl, [UriKind]::Absolute, [ref]$providedUri) -and
        $providedUri.Scheme -in @('http', 'https') -and
        $providedUri.Host -notmatch $excludedHostPattern -and
        $providedUri.Host -notmatch $nonOfficialHostPattern) {
        $providedRoot = $providedUri.GetLeftPart([System.UriPartial]::Authority)
    }
}
$identityCandidateRoots = @(
    $providedRoot
    foreach ($result in @($identityResponse.results)) {
        $candidateUri = $null
        if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$candidateUri) -and
            $candidateUri.Host -notmatch $excludedHostPattern -and $candidateUri.Host -notmatch $nonOfficialHostPattern) {
            $candidateUri.GetLeftPart([System.UriPartial]::Authority)
        }
    }
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique -First 5

$identityVerificationUrls = @(
    # Keep the authoritative registry pages inside the bound even when the
    # search returns several candidate hosts. They provide the legal identity,
    # registered address and current officers used by every later stage.
    if ($CompanyNumber -match '^[A-Za-z0-9]+$') {
        "https://find-and-update.company-information.service.gov.uk/company/$CompanyNumber"
        "https://find-and-update.company-information.service.gov.uk/company/$CompanyNumber/officers"
    }
    foreach ($root in $identityCandidateRoots) {
        foreach ($path in @('', '/about', '/contact', '/privacy', '/legal', '/terms-and-conditions')) {
            "$root$path"
        }
    }
) | Select-Object -Unique -First 24
$identityPages = @($identityVerificationUrls | ForEach-Object -Parallel {
    try {
        $page = (& $using:pageScript -Url $_ -MaximumCharacters 8000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
        [pscustomobject]@{
            url = [string]$page.url
            title = [string]$page.title
            emails = @($page.emails)
            phones = @($page.phones | Select-Object -First 5)
            links = @($page.links)
            excerpt = [string]$page.text
        }
    }
    catch { $null }
} -ThrottleLimit 12 | Where-Object { $null -ne $_ })

# Companies often publish under an earlier trading name while the imported
# record contains only the current legal entity. Preserve aliases and the
# registered address from the authoritative overview so that identity
# discovery can bridge rebrands without trusting a directory as evidence.
$registryAliases = @(
    foreach ($page in @($identityPages | Where-Object { $_.url -match ('company-information\.service\.gov\.uk/company/' + [regex]::Escape($CompanyNumber) + '/?$') })) {
        $previousNamesSection = [regex]::Match(
            [string]$page.excerpt,
            '(?is)Previous company names(?:\s+Previous company names)?\s+Name\s+Period\s+(?<names>.+?)(?:\s+Tell us what you think|\s+Support links)')
        if (-not $previousNamesSection.Success) { continue }
        foreach ($match in [regex]::Matches(
            $previousNamesSection.Groups['names'].Value,
            "(?<name>[A-Z][A-Z0-9 &'().,\-]{3,}?)\s+\d{1,2}\s+[A-Z][a-z]{2}\s+\d{4}\s*[-–—]\s*\d{1,2}\s+[A-Z][a-z]{2}\s+\d{4}")) {
            ($match.Groups['name'].Value -replace '\s+', ' ').Trim()
        }
    }
) | Select-Object -Unique -First 8
$registeredAddresses = @(
    foreach ($page in @($identityPages | Where-Object { $_.url -match ('company-information\.service\.gov\.uk/company/' + [regex]::Escape($CompanyNumber) + '/?$') })) {
        foreach ($match in [regex]::Matches([string]$page.excerpt, '(?is)Registered office address\s+(?<address>.+?)\s+Company status\b')) {
            ($match.Groups['address'].Value -replace '\s+', ' ').Trim()
        }
    }
) | Select-Object -Unique -First 2
$registeredStreetPhrases = @(
    foreach ($address in $registeredAddresses) {
        $street = @($address -split ',' | Select-Object -First 1) -join ', '
        if ($street.Length -ge 6) { $street.Trim() }
    }
) | Select-Object -Unique -First 2
$registryAliasBaseNames = @(
    foreach ($alias in $registryAliases) {
        ($alias -replace '(?i)\s+(?:LIMITED|LTD|PLC|P\s*\.?\s*L\s*\.?\s*C|LLP|INCORPORATED|INC|CORPORATION|CORP)\.?$', '').Trim()
    }
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
$contextTerms = @($contextTerms) + @($registryAliases) + @($registryAliasBaseNames) | Select-Object -Unique
$knownResourcePages = @(
    if (Test-ResourcePackBudget -ReserveSeconds 45) {
        $knownResourceUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 5)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 4 | Where-Object { $null -ne $_ }
    }
)
$identityPages = @($identityPages) + @($knownResourcePages)
$knownRoleSignal = @($knownResourcePages | Where-Object {
    ([string]$_.title + ' ' + [string]$_.excerpt) -match
        '(?i)chief financial officer|finance director|financial controller|head of finance|procurement director|chief executive officer|managing director|\bowner\b|\bcfo\b|\bceo\b'
}).Count -gt 0
$identityTokens = @(((@($legalBaseName, $TradingName) + @($registryAliasBaseNames)) -join ' ').ToUpperInvariant() -split '[^A-Z0-9]+' |
    Where-Object { $_.Length -ge 3 -and $_ -notin $identityTokenExclusions } |
    Select-Object -Unique)
$currentIdentityTokens = @(($legalBaseName + ' ' + $TradingName).ToUpperInvariant() -split '[^A-Z0-9]+' |
    Where-Object { $_.Length -ge 3 -and $_ -notin $identityTokenExclusions } |
    Select-Object -Unique)

# The registered-office postcode on the authoritative company overview is a
# stable bridge from a legal entity to the public trading site. Only fall back
# to repeated officer correspondence postcodes when the overview is unavailable.
# Search it with
# the most distinctive legal-name token, then promote only a non-directory
# host whose opened page repeats that address-bound identity.
$overviewPostcodes = @(
    foreach ($page in @($identityPages | Where-Object { $_.url -match ('company-information\.service\.gov\.uk/company/' + [regex]::Escape($CompanyNumber) + '/?$') })) {
        [regex]::Matches(
            [string]$page.excerpt,
            '(?i)\b(?:GIR\s?0AA|(?:[A-PR-UWYZ][0-9][0-9A-HJKSTUW]?|[A-PR-UWYZ][A-HK-Y][0-9][0-9ABEHMNPRV-Y]?)[ ]?[0-9][ABD-HJLNP-UW-Z]{2})\b') |
            ForEach-Object { ($_.Value -replace '\s+', ' ').ToUpperInvariant() }
    }
) | Select-Object -Unique -First 2
$officerPostcodes = @(
    foreach ($page in @($identityPages | Where-Object { $_.url -match 'company-information\.service\.gov\.uk/.+/officers' })) {
        [regex]::Matches(
            [string]$page.excerpt,
            '(?i)\b(?:GIR\s?0AA|(?:[A-PR-UWYZ][0-9][0-9A-HJKSTUW]?|[A-PR-UWYZ][A-HK-Y][0-9][0-9ABEHMNPRV-Y]?)[ ]?[0-9][ABD-HJLNP-UW-Z]{2})\b') |
            ForEach-Object { ($_.Value -replace '\s+', ' ').ToUpperInvariant() }
    }
) | Group-Object | Sort-Object Count -Descending | Select-Object -First 2 -ExpandProperty Name
$registryPostcodes = if ($overviewPostcodes.Count -gt 0) { @($overviewPostcodes) } else { @($officerPostcodes) }
$identityAnchors = @(
    @($currentIdentityTokens | Sort-Object Length -Descending)
    @($identityTokens | Sort-Object Length -Descending)
) | Select-Object -Unique -First 5
$identityAnchor = @($identityAnchors | Select-Object -First 1)[0]
$bridgeQueries = @(
    # Street plus a single distinctive token is robust to brands that differ
    # from the legal entity (and avoids over-constraining search with the full
    # historical name). Results still have to repeat the registry postcode and
    # an identity token before their opened page can be promoted.
    foreach ($street in $registeredStreetPhrases) {
        foreach ($anchor in $identityAnchors) { '"{0}" {1}' -f $street, $anchor }
    }
    foreach ($postcode in $registryPostcodes) {
        foreach ($anchor in $identityAnchors) { '"{0}" {1}' -f $postcode, $anchor }
        foreach ($alias in @($legalBaseName) + @($registryAliasBaseNames)) {
            $aliasSearchName = ($alias -replace '&', ' and ' -replace '[^A-Za-z0-9]+', ' ' -replace '\s+', ' ').Trim()
            if (-not [string]::IsNullOrWhiteSpace($aliasSearchName)) { '"{0}" "{1}"' -f $postcode, $aliasSearchName }
        }
    }
) | Select-Object -Unique -First 12
$priorityBridgeQueries = @(
    foreach ($street in $registeredStreetPhrases) {
        foreach ($anchor in @($identityAnchors | Select-Object -First 2)) { '"{0}" {1}' -f $street, $anchor }
    }
) | Select-Object -Unique -First 2
$priorityBridgeResponses = @(
    foreach ($query in $priorityBridgeQueries) {
        if (-not (Test-ResourcePackBudget -ReserveSeconds 45)) { break }
        try {
            $payload = (& $searchScript -Query $query -Limit 10) | ConvertFrom-Json
            [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
        }
        catch {
            [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
        }
    }
)
$fallbackBridgeQueries = @($bridgeQueries | Where-Object { $_ -notin $priorityBridgeQueries })
$fallbackBridgeResponses = @(
    if (Test-ResourcePackBudget -ReserveSeconds 40) {
        @($fallbackBridgeQueries | Select-Object -First 4) | ForEach-Object -Parallel {
            $query = $_
            try {
                $payload = (& $using:searchScript -Query $query -Limit 10) | ConvertFrom-Json
                [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
            }
            catch {
                [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
            }
        } -ThrottleLimit 4
    }
)
$bridgeResponses = @($priorityBridgeResponses) + @($fallbackBridgeResponses)
$searchResponses = @($searchResponses) + @($bridgeResponses)
$seenBridgeResultUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$bridgeCandidateResults = @(
    foreach ($response in $bridgeResponses) {
        foreach ($result in @($response.results)) {
            $candidateUri = $null
            if (-not [uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$candidateUri) -or
                $candidateUri.Host -match $excludedHostPattern) { continue }
            $resultText = ([string]$result.title + ' ' + [string]$result.snippet).ToUpperInvariant()
            $hasPostcode = @($registryPostcodes | Where-Object { $resultText.Contains($_) }).Count -gt 0
            $hasAnchor = @($identityAnchors | Where-Object { $resultText -match ('\b' + [regex]::Escape($_) + '\b') }).Count -gt 0
            if ($hasPostcode -and $hasAnchor -and $seenBridgeResultUrls.Add([string]$result.url)) { $result }
        }
    }
) | Select-Object -First 12
$bridgeWebsiteRoots = @(
    foreach ($result in $bridgeCandidateResults) {
        $resultUri = $null
        if (-not [uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$resultUri)) { continue }
        $resultText = [string]$result.title + ' ' + [string]$result.snippet
        foreach ($match in [regex]::Matches($resultText, '(?i)\b(?:https?://)?(?:www\.)?(?<host>[a-z0-9][a-z0-9.-]+\.(?:com|co\.uk|org|net|io|uk))\b')) {
            $candidateWebsiteHost = $match.Groups['host'].Value.ToLowerInvariant()
            if ($candidateWebsiteHost -eq ($resultUri.Host -replace '^www\.', '') -or $candidateWebsiteHost -match $excludedHostPattern -or $candidateWebsiteHost -match $nonOfficialHostPattern) { continue }
            "https://$candidateWebsiteHost"
        }
    }
) | Select-Object -Unique -First 4
$bridgeCandidateUrls = @(
    @($bridgeCandidateResults | Select-Object -ExpandProperty url -Unique)
    @($bridgeWebsiteRoots)
) | Select-Object -Unique
$bridgePages = @(
    if (Test-ResourcePackBudget -ReserveSeconds 35) {
        $bridgeCandidateUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 8000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 5)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 6 | Where-Object { $null -ne $_ }
    }
)
$identityPages = @($identityPages) + @($bridgePages)
$identityCandidateRoots = @(
    @($identityCandidateRoots)
    @($bridgeWebsiteRoots)
    foreach ($result in $bridgeCandidateResults) {
        $candidateUri = $null
        if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$candidateUri) -and
            $candidateUri.Host -notmatch $nonOfficialHostPattern) {
            $candidateUri.GetLeftPart([System.UriPartial]::Authority)
        }
    }
) | Select-Object -Unique -First 8
$derivedAliases = @(
    foreach ($result in $bridgeCandidateResults) {
        foreach ($segment in @(([string]$result.title) -split '\s+(?:[-–—|])\s+')) {
            $alias = ($segment -replace '\s+', ' ').Trim()
            $matchesAnchor = @($identityAnchors | Where-Object { $alias -match ('(?i)\b' + [regex]::Escape($_) + '\b') }).Count -gt 0
            if ($alias.Length -ge 4 -and $alias.Length -le 60 -and $matchesAnchor) { $alias }
        }
    }
) | Select-Object -Unique -First 3
$contextTerms = @($contextTerms) + @($derivedAliases) | Select-Object -Unique

$resolvedRoot = $null
$scoredRoots = @(
    foreach ($root in $identityCandidateRoots) {
            $rootUri = [uri]$root
            $rootPages = @($identityPages | Where-Object {
                $pageUri = $null
                [uri]::TryCreate([string]$_.url, [UriKind]::Absolute, [ref]$pageUri) -and $pageUri.Host -eq $rootUri.Host
            })
            $rootIdentityResults = @($identityResponse.results | Where-Object {
                $resultUri = $null
                [uri]::TryCreate([string]$_.url, [UriKind]::Absolute, [ref]$resultUri) -and $resultUri.Host -eq $rootUri.Host
            })
            $haystack = ((
                @($rootPages | ForEach-Object { $_.title + ' ' + $_.excerpt }) +
                @($rootIdentityResults | ForEach-Object { [string]$_.title + ' ' + [string]$_.snippet })
            ) -join ' ').ToUpperInvariant()
            $compactHaystack = $haystack -replace '[^A-Z0-9]', ''
            $compactLegalName = $legalBaseName.ToUpperInvariant() -replace '[^A-Z0-9]', ''
            $score = 0
            $hasCompanyNumber = -not [string]::IsNullOrWhiteSpace($CompanyNumber) -and $haystack.Contains($CompanyNumber.ToUpperInvariant())
            $hasLegalName = $haystack.Contains($legalBaseName.ToUpperInvariant()) -or
                (-not [string]::IsNullOrWhiteSpace($compactLegalName) -and $compactHaystack.Contains($compactLegalName))
            $matchedIdentityTokens = @($identityTokens | Where-Object { $haystack -match ('\b' + [regex]::Escape($_) + '\b') }).Count
            $hasRegistryPostcode = @($registryPostcodes | Where-Object { $haystack.Contains($_) }).Count -gt 0
            if ($hasCompanyNumber) { $score += 100 }
            if ($hasLegalName) { $score += 80 }
            $score += 12 * $matchedIdentityTokens
            if ($bridgeWebsiteRoots -contains $root) { $score += 200 }
            $hostText = ($rootUri.Host -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
            $hostIdentityTokens = @($identityTokens | Where-Object { $hostText.Contains($_) }).Count
            $score += 10 * $hostIdentityTokens
            $hostContainsCompactLegalName = $compactLegalName.Length -ge 5 -and $hostText.Contains($compactLegalName)
            if ($hostContainsCompactLegalName) { $score += 90 }
            $hasOperationalSignal = $haystack -match '(?i)\b(?:products?|services?|customers?|suppliers?|manufactur\w*|shop|contact us|about us|our (?:company|team|story)|what we do)\b'
            $wasIdentitySearchResult = $rootIdentityResults.Count -gt 0
            if ($hasOperationalSignal) { $score += 30 }
            if ($wasIdentitySearchResult) { $score += 20 }
            $strongIdentity = ($bridgeWebsiteRoots -contains $root) -or
                ($hostIdentityTokens -ge 1 -and (
                    $hasCompanyNumber -or
                    $hasLegalName -or
                    ($matchedIdentityTokens -ge 1 -and $hasOperationalSignal)))
            [pscustomobject]@{ root = $root; score = $score; strongIdentity = $strongIdentity }
    }
)
foreach ($candidate in @($scoredRoots | Sort-Object score -Descending)) {
    if ($candidate.strongIdentity) {
        $resolvedRoot = [string]$candidate.root
        break
    }
}

# Once identity is resolved, gather documents against that verified host. This
# deliberately happens after alias resolution: searching documents for a legal
# shell name before discovering the trading brand is both noisy and incomplete.
$postIdentityResponses = @()
$officialResourceResultUrls = @()
$earlyOfficialResourcePages = @()
$discoveredRoleSignal = $false
$associationIdentityAnchors = @($currentIdentityTokens)
$verifiedOfficialRoots = @(
    $resolvedRoot
    @($scoredRoots | Where-Object { $_.strongIdentity } | Sort-Object score -Descending | Select-Object -ExpandProperty root)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique -First 3
$verifiedOfficialHosts = @(
    foreach ($root in $verifiedOfficialRoots) {
        (([uri]$root).Host -replace '^www\.', '').ToLowerInvariant()
    }
) | Select-Object -Unique
if (-not [string]::IsNullOrWhiteSpace($resolvedRoot)) {
    $resolvedHost = ([uri]$resolvedRoot).Host -replace '^www\.', ''
    $resolvedHostCompact = $resolvedHost.ToUpperInvariant() -replace '[^A-Z0-9]', ''
    $hostMatchedIdentityTokens = @($identityTokens | Where-Object { $resolvedHostCompact.Contains($_) } | Sort-Object Length -Descending)
    $associationIdentityAnchors = @($hostMatchedIdentityTokens) + @($currentIdentityTokens) | Select-Object -Unique -First 5
    # The verified host is a far more discriminating search anchor than a
    # legal-name token. Tokens such as GROUP, ASSETS or ENGINEERING flood the
    # person/email stage with unrelated companies, whereas the host also lets
    # public documents and government correspondence bridge the exact domain.
    $contactSearchAnchor = $resolvedHost
    $preferredAlias = @($derivedAliases | Select-Object -First 1)[0]
    $postIdentityQueries = @(
        foreach ($officialHost in $verifiedOfficialHosts) {
            'site:{0} filetype:pdf supplier contact' -f $officialHost
        }
        if (-not [string]::IsNullOrWhiteSpace($preferredAlias)) { '"{0}" supplier information pdf' -f $preferredAlias }
    ) | Select-Object -Unique -First 4
    $documentDiscoveryResponses = @(
        if (Test-ResourcePackBudget -ReserveSeconds 30) {
            $postIdentityQueries | ForEach-Object -Parallel {
                $query = $_
                try {
                    $payload = (& $using:searchScript -Query $query -Limit 8) | ConvertFrom-Json
                    [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
                }
                catch {
                    [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
                }
            } -ThrottleLimit 2
        }
    )
    $earlyOfficialResourceUrls = @(
        foreach ($response in $documentDiscoveryResponses) {
            foreach ($result in @($response.results)) {
                $resultUri = $null
                if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$resultUri) -and
                    $verifiedOfficialHosts -contains (($resultUri.Host -replace '^www\.', '').ToLowerInvariant())) {
                    [string]$result.url
                }
            }
        }
    ) | Select-Object -Unique -First 6
    # Open official documents before broad role searches. Large public search
    # result sets are variable, while a signed supplier/policy/report PDF often
    # contains the highest-value current leadership evidence directly.
    $earlyOfficialResourcePages = @(
        if (Test-ResourcePackBudget -ReserveSeconds 35) {
            $earlyOfficialResourceUrls | ForEach-Object -Parallel {
                try {
                    $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                    [pscustomobject]@{
                        url = [string]$page.url
                        title = [string]$page.title
                        emails = @($page.emails)
                        phones = @($page.phones | Select-Object -First 5)
                        links = @($page.links)
                        excerpt = [string]$page.text
                    }
                }
                catch { $null }
            } -ThrottleLimit 6 | Where-Object { $null -ne $_ }
        }
    )
    $discoveredRoleSignal = @($earlyOfficialResourcePages | Where-Object {
        ([string]$_.title + ' ' + [string]$_.excerpt) -match
            '(?i)chief financial officer|finance director|financial controller|head of finance|procurement director|chief executive officer|managing director|\bowner\b|\bcfo\b|\bceo\b'
    }).Count -gt 0
    $roleDiscoveryQueries = @(
        if ($ResearchGoal -eq 'contact' -and -not $knownRoleSignal -and -not $discoveredRoleSignal -and -not [string]::IsNullOrWhiteSpace($contactSearchAnchor)) {
            '{0} company UK Chief Financial Officer email' -f $contactSearchAnchor
            '{0} company UK Finance Director email' -f $contactSearchAnchor
            '{0} company UK Procurement Director email' -f $contactSearchAnchor
            '{0} company UK Managing Director email' -f $contactSearchAnchor
        }
    ) | Select-Object -Unique -First 4
    # Keep the high-value role queries sequential. Bursting several near-
    # identical searches at a public provider produces nondeterministic result
    # sets and can omit the one page that names the decision-maker.
    $roleDiscoveryResponses = @(
        foreach ($query in $roleDiscoveryQueries) {
            if (-not (Test-ResourcePackBudget -ReserveSeconds 25)) { break }
            try {
                $payload = (& $searchScript -Query $query -Limit 8) | ConvertFrom-Json
                [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
            }
            catch {
                [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
            }
        }
    )
    $postIdentityResponses = @($documentDiscoveryResponses) + @($roleDiscoveryResponses)
    $searchResponses = @($searchResponses) + @($postIdentityResponses)
    $officialResourceResultUrls = @(
        foreach ($response in $postIdentityResponses) {
            foreach ($result in @($response.results)) {
                $resultUri = $null
                if ([uri]::TryCreate([string]$result.url, [UriKind]::Absolute, [ref]$resultUri) -and
                    $verifiedOfficialHosts -contains (($resultUri.Host -replace '^www\.', '').ToLowerInvariant())) { [string]$result.url }
            }
        }
    ) | Select-Object -Unique -First 8
}

$resultUrls = @(
    foreach ($response in $searchResponses) {
        foreach ($result in @($response.results)) {
            $url = [string]$result.url
            if ($url -notmatch '^https?://' -or $url -match $excludedHostPattern) { continue }
            $resultText = ([string]$result.title + ' ' + [string]$result.snippet).ToUpperInvariant()
            $matchedResultTokens = @($identityTokens | Where-Object { $resultText -match ('\b' + [regex]::Escape($_) + '\b') }).Count
            $matchesAddressBridge = @($registryPostcodes | Where-Object { $resultText.Contains($_) }).Count -gt 0 -and $matchedResultTokens -ge 1
            $isAssociated = $resultText.Contains($CompanyNumber.ToUpperInvariant()) -or
                $resultText.Contains($displayName.ToUpperInvariant()) -or
                $matchedResultTokens -ge 2 -or $matchesAddressBridge
            if ($isAssociated) { $url }
        }
    }
) | Select-Object -Unique -First 12
$officialPageUrls = @(
    foreach ($officialRoot in $verifiedOfficialRoots) {
        foreach ($path in @('', '/about', '/about-us', '/team', '/our-team', '/leadership', '/management', '/contact', '/privacy')) {
            "$officialRoot$path"
        }
    }
) | Select-Object -Unique
$resourceUrls = @($officialResourceResultUrls) + @($officialPageUrls) + @($resultUrls) |
    Where-Object { $_ -notin $knownResourceUrls -and $_ -notin $earlyOfficialResourceUrls } |
    Select-Object -Unique -First 24
$resourcePages = @(
    @($earlyOfficialResourcePages)
    if (Test-ResourcePackBudget -ReserveSeconds 20) {
        $resourceUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 12000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 5)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 6 | Where-Object { $null -ne $_ }
    }
)
$resourcePages = @($identityPages) + @($resourcePages)

$supportingUrls = @()
$documentCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($resolvedRoot)) {
    foreach ($page in @($resourcePages)) {
        foreach ($link in @($page.links)) {
            $linkUri = $null
            if (-not [uri]::TryCreate([string]$link, [UriKind]::Absolute, [ref]$linkUri) -or
                $verifiedOfficialHosts -notcontains (($linkUri.Host -replace '^www\.', '').ToLowerInvariant())) { continue }
            if ($linkUri.AbsolutePath -match '(?i)\.pdf$') {
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($linkUri.AbsolutePath)
                $revision = 0
                if ($baseName -match '(?i)(?:rev(?:ision)?|ver(?:sion)?|v)[-_ ]*0*(?<revision>\d+)') { $revision = [int]$Matches['revision'] }
                $family = $baseName -replace '(?i)(?:rev(?:ision)?|ver(?:sion)?|v)[-_ ]*0*\d+', ''
                $family = (($family -replace '(?i)20\d{2}[-_ ](?:0?[1-9]|1[0-2])(?:[-_ ](?:0?[1-9]|[12]\d|3[01]))?', '') -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
                $rank = if ($baseName -match '(?i)(?:supplier|contact|profile|brochure|annual|report|accounts|modern-slavery|gender-pay)') { 0 }
                    elseif ($baseName -match '(?i)(?:quality|policy|governance)') { 1 }
                    else { 2 }
                $documentCandidates += [pscustomobject]@{ url = $linkUri.AbsoluteUri; family = $family; revision = $revision; rank = $rank }
            }
            elseif ($linkUri.AbsolutePath -match '(?i)(?:about|company|team|leadership|management|governance|contact|supplier|annual|report|accounts|modern-slavery|gender-pay)') {
                $supportingUrls += $linkUri.AbsoluteUri
            }
        }
    }
}
$supportingUrls = @($supportingUrls | Select-Object -Unique -First 8)
$documentUrls = @(
    $seenFamilies = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($document in @($documentCandidates | Sort-Object rank, @{ Expression = 'revision'; Descending = $true })) {
        $family = if ([string]::IsNullOrWhiteSpace([string]$document.family)) { [string]$document.url } else { [string]$document.family }
        if ($seenFamilies.Add($family)) { [string]$document.url }
    }
) | Select-Object -First 4
$linkedUrls = @($supportingUrls) + @($documentUrls) | Select-Object -Unique
$linkedPages = @(
    if (Test-ResourcePackBudget -ReserveSeconds 25) {
        $linkedUrls | ForEach-Object -Parallel {
            try {
                $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                [pscustomobject]@{
                    url = [string]$page.url
                    title = [string]$page.title
                    emails = @($page.emails)
                    phones = @($page.phones | Select-Object -First 5)
                    links = @($page.links)
                    excerpt = [string]$page.text
                }
            }
            catch { $null }
        } -ThrottleLimit 8 | Where-Object { $null -ne $_ }
    }
)
$resourcePages = @($resourcePages) + @($linkedPages)

$rolePattern = '(?i:Chief Financial Officer|Finance Director|Financial Controller|Head of Finance|Procurement Director|Chief Executive Officer|Managing Director|Owner|CFO|CEO)'
$nonPersonNamePattern = '(?i)\b(?:Assets?|Board|Chair(?:man|woman)?|ConnectionResponseContent|Engineering|Group|Http|Maritime|Timber|Sawmills?|Policy|Global|Supply|Director|Finance|Officer|Controller|Chief|Procurement|Management|Leadership|Operations|Services?|Support|Contact|Quality|Sales|Company|Limited|Department|Team|Profile|Email|Address|Phone)\b'
$roleCandidateInputs = @(
    @($resourcePages)
    foreach ($response in @($searchResponses)) {
        foreach ($result in @($response.results)) {
            $resultText = ([string]$result.title + ' ' + [string]$result.snippet + ' ' + [string]$result.url).ToUpperInvariant()
            $compactResultText = $resultText -replace '[^A-Z0-9]', ''
            $compactDisplayName = $displayName.ToUpperInvariant() -replace '[^A-Z0-9]', ''
            $resolvedHostText = if ([string]::IsNullOrWhiteSpace($resolvedRoot)) { '' } else { (([uri]$resolvedRoot).Host -replace '^www\.', '').ToUpperInvariant() }
            $hasAnchorCompanyAssociation = @($associationIdentityAnchors | Where-Object {
                $anchorAssociationPattern = '(?:\b{0}\b.{{0,35}}\b(?:COMPANY|COMPANIES|GROUP|TIMBER|SAWMILLS?)\b|\b(?:COMPANY|COMPANIES|GROUP|TIMBER|SAWMILLS?)\b.{{0,35}}\b{0}\b)' -f [regex]::Escape($_)
                $resultText -match $anchorAssociationPattern
            }).Count -gt 0
            $isAssociatedDiscovery = $resultText.Contains($CompanyNumber.ToUpperInvariant()) -or
                (-not [string]::IsNullOrWhiteSpace($compactDisplayName) -and $compactResultText.Contains($compactDisplayName)) -or
                (-not [string]::IsNullOrWhiteSpace($resolvedHostText) -and $resultText.Contains($resolvedHostText)) -or
                $hasAnchorCompanyAssociation
            if (-not $isAssociatedDiscovery) { continue }
            # Search snippets may suggest whom to investigate, but they never
            # become acceptance evidence. A candidate still has to survive the
            # later opened-page email and role validation.
            [pscustomobject]@{
                url = [string]$result.url
                title = [string]$result.title
                excerpt = [string]$result.snippet
            }
        }
    }
)
$roleCandidates = @(
    foreach ($page in $roleCandidateInputs) {
        $text = ([string]$page.title + ' ' + [string]$page.excerpt) -replace '\s+', ' '
        $text = $text `
            -replace '(?i)ChiefFinancialOfficer', 'Chief Financial Officer' `
            -replace '(?i)ChiefExecutiveOfficer', 'Chief Executive Officer' `
            -replace '(?i)FinanceDirector', 'Finance Director' `
            -replace '(?i)FinancialController', 'Financial Controller' `
            -replace '(?i)HeadofFinance', 'Head of Finance' `
            -replace '(?i)ProcurementDirector', 'Procurement Director' `
            -replace '(?i)ManagingDirector', 'Managing Director'
        $candidateSourceRank = 2
        $candidatePageUri = $null
        if (-not [string]::IsNullOrWhiteSpace($resolvedRoot) -and
            [uri]::TryCreate([string]$page.url, [UriKind]::Absolute, [ref]$candidatePageUri) -and
            (($candidatePageUri.Host -replace '^www\.', '') -eq (([uri]$resolvedRoot).Host -replace '^www\.', ''))) {
            $candidateSourceRank = 0
        }
        elseif ((-not [string]::IsNullOrWhiteSpace($CompanyNumber) -and $text -match [regex]::Escape($CompanyNumber)) -or
            $text -match [regex]::Escape($displayName)) {
            $candidateSourceRank = 1
        }
        # Prefer the two tokens immediately adjacent to a role. A greedy
        # two-to-four-token expression consumes company headings or the end of
        # the preceding sentence (for example, "Assets Kevin Hobbs") and then
        # never gives the regex engine a chance to evaluate the real name.
        foreach ($pattern in @(
            "(?<name>(?:(?:Mr|Mrs|Ms|Miss|Dr|Sir|Dame)\s+)?[A-Z][A-Za-z'’\-]+\s+[A-Z][A-Za-z'’\-]+)\s*(?:[-–—|:,]\s*|\b(?:Position|Role)\s*:\s*)?(?<role>$rolePattern)\b",
            "(?<role>$rolePattern)\s*(?:[-–—|:,]\s*)+(?<name>(?:(?:Mr|Mrs|Ms|Miss|Dr|Sir|Dame)\s+)?[A-Z][A-Za-z'’\-]+\s+[A-Z][A-Za-z'’\-]+)\b")) {
            foreach ($match in [regex]::Matches($text, $pattern)) {
                $name = $match.Groups['name'].Value.Trim()
                do {
                    $cleanedName = $name `
                        -replace '^(?i)(?:Mr|Mrs|Ms|Miss|Dr|Sir|Dame)\s+', '' `
                        -replace '^(?i)(?:United\s+Kingdom|Kingdom|Email|View|Contact|Team|Leadership|Management)\s+', '' `
                        -replace '\s+(?i:Chair|Chairman|Chairwoman)$', ''
                    $changed = $cleanedName -ne $name
                    $name = $cleanedName
                } while ($changed)
                if (($name -split '\s+').Count -ge 2 -and $name -notmatch $nonPersonNamePattern) {
                    $role = $match.Groups['role'].Value
                    $roleRank = if ($role -match '(?i)Chief Financial Officer|CFO') { 0 }
                        elseif ($role -match '(?i)Finance Director|Financial Controller|Head of Finance') { 1 }
                        elseif ($role -match '(?i)Procurement Director') { 2 }
                        elseif ($role -match '(?i)Chief Executive Officer|CEO') { 3 }
                        else { 4 }
                    [pscustomobject]@{
                        name = $name
                        role = $role
                        url = [string]$page.url
                        sourceRank = $candidateSourceRank
                        roleRank = $roleRank
                    }
                }
            }
        }
    }
) | Sort-Object sourceRank, roleRank
$seenRoleCandidateNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$roleCandidates = @(
    foreach ($candidate in $roleCandidates) {
        if ($seenRoleCandidateNames.Add([string]$candidate.name)) { $candidate }
    }
) | Select-Object -First 8
$officerCandidates = @(
    foreach ($page in @($resourcePages | Where-Object { $_.url -match 'company-information\.service\.gov\.uk/.+/officers' })) {
        foreach ($match in [regex]::Matches([string]$page.excerpt, "(?<last>[A-Z][A-Z'’\-]+),\s+(?<first>[A-Z][A-Za-z'’\-]+(?:\s+[A-Z][A-Za-z'’\-]+){0,2})\s+Correspondence address.{0,450}?Role Active (?:Director|Secretary)")) {
            $last = $match.Groups['last'].Value.ToLowerInvariant()
            $last = $last.Substring(0, 1).ToUpperInvariant() + $last.Substring(1)
            "$($match.Groups['first'].Value) $last"
        }
    }
) | Select-Object -Unique -First 8
$candidateNames = @(
    @($roleCandidates | Select-Object -ExpandProperty name)
    @($officerCandidates)
) | Select-Object -Unique -First 8

$secondaryResponses = @()
if ($ResearchGoal -eq 'contact' -and $candidateNames.Count -gt 0) {
    $officialEmailDomains = @(
        if (-not [string]::IsNullOrWhiteSpace($resolvedRoot)) {
            $officialHost = ([uri]$resolvedRoot).Host -replace '^www\.', ''
            foreach ($page in @($resourcePages)) {
                $pageUri = $null
                if (-not [uri]::TryCreate([string]$page.url, [UriKind]::Absolute, [ref]$pageUri) -or
                    (($pageUri.Host -replace '^www\.', '') -ne $officialHost)) { continue }
                foreach ($email in @($page.emails)) {
                    $domain = ([string]$email -split '@')[-1].ToLowerInvariant().TrimEnd('.')
                    if ($domain -match '^[a-z0-9.-]+\.[a-z]{2,}$') { $domain }
                }
            }
            $officialHost
        }
    ) | Where-Object { $_ -notmatch '^(?:gmail|googlemail|hotmail|outlook|yahoo|icloud)\.' } | Select-Object -Unique -First 2
    $secondaryQueries = @(
        foreach ($candidateName in @($candidateNames | Select-Object -First 4)) {
            $nameParts = @($candidateName -split '\s+')
            $searchNames = @($candidateName)
            if ($nameParts.Count -gt 2) { $searchNames += "$($nameParts[0]) $($nameParts[-1])" }
            foreach ($searchCandidate in @($searchNames | Select-Object -Unique)) {
                '"{0}" "{1}" email' -f $searchCandidate, $displayName
                if (-not [string]::IsNullOrWhiteSpace($contactSearchAnchor)) { '"{0}" {1} email' -f $searchCandidate, $contactSearchAnchor }
                foreach ($domain in $officialEmailDomains) { '"{0}" "{1}"' -f $searchCandidate, $domain }
            }
            if ($nameParts.Count -ge 2) {
                $firstName = $nameParts[0].ToLowerInvariant() -replace '[^a-z0-9]', ''
                $lastName = $nameParts[-1].ToLowerInvariant() -replace '[^a-z0-9]', ''
                foreach ($domain in $officialEmailDomains) {
                    if (-not [string]::IsNullOrWhiteSpace($firstName) -and -not [string]::IsNullOrWhiteSpace($lastName)) {
                        '"{0}.{1}@{2}"' -f $firstName, $lastName, $domain
                    }
                }
            }
        }
    ) | Select-Object -Unique -First 14
    $prioritySecondaryQueries = @(
        foreach ($candidateName in @($candidateNames | Select-Object -First 2)) {
            $nameParts = @($candidateName -split '\s+')
            $priorityName = if ($nameParts.Count -gt 2) { "$($nameParts[0]) $($nameParts[-1])" } else { $candidateName }
            foreach ($domain in @($officialEmailDomains | Select-Object -First 1)) {
                # Public authorities, councils and trade bodies routinely
                # publish tender, FOI and board documents containing exact
                # company-domain addresses. These are published evidence, not
                # guessed address patterns, and work across source layouts.
                'site:gov.uk "{0}" "{1}"' -f $priorityName, $domain
                'site:gov.scot "{0}" "{1}"' -f $priorityName, $domain
            }
        }
    ) | Select-Object -Unique -First 4
    $prioritySecondaryResponses = @(
        foreach ($query in $prioritySecondaryQueries) {
            if (-not (Test-ResourcePackBudget -ReserveSeconds 25)) { break }
            try {
                $payload = (& $searchScript -Query $query -Limit 10) | ConvertFrom-Json
                [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
            }
            catch {
                [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
            }
        }
    )
    $fallbackSecondaryQueries = @($secondaryQueries | Where-Object { $_ -notin $prioritySecondaryQueries })
    $fallbackSecondaryResponses = @(
        if (Test-ResourcePackBudget -ReserveSeconds 20) {
            $fallbackSecondaryQueries | ForEach-Object -Parallel {
                $query = $_
                try {
                    $payload = (& $using:searchScript -Query $query -Limit 5) | ConvertFrom-Json
                    [pscustomobject]@{ query = $query; results = @($payload.results); error = $null }
                }
                catch {
                    [pscustomobject]@{ query = $query; results = @(); error = $_.Exception.Message }
                }
            } -ThrottleLimit 5
        }
    )
    $secondaryResponses = @($prioritySecondaryResponses) + @($fallbackSecondaryResponses)
    $prioritySecondaryUrls = @(
        foreach ($response in $prioritySecondaryResponses) {
            $queryName = [regex]::Match([string]$response.query, '^"(?<name>[^"]+)"').Groups['name'].Value
            if ([string]::IsNullOrWhiteSpace($queryName)) {
                $queryName = [regex]::Match([string]$response.query, 'site:\S+\s+"(?<name>[^"]+)"').Groups['name'].Value
            }
            $requiredSiteHost = [regex]::Match([string]$response.query, '(?i)\bsite:(?<host>[a-z0-9.-]+)').Groups['host'].Value
            $eligibleResultCount = 0
            foreach ($result in @($response.results)) {
                $url = [string]$result.url
                $resultText = [string]$result.title + ' ' + [string]$result.snippet + ' ' + $url
                $resultUri = $null
                $matchesRequiredSite = [string]::IsNullOrWhiteSpace($requiredSiteHost) -or
                    ([uri]::TryCreate($url, [UriKind]::Absolute, [ref]$resultUri) -and
                        ($resultUri.Host -eq $requiredSiteHost -or $resultUri.Host.EndsWith('.' + $requiredSiteHost, [StringComparison]::OrdinalIgnoreCase)))
                if ($url -match '^https?://' -and $url -notmatch $excludedHostPattern -and
                    $matchesRequiredSite -and
                    ([string]::IsNullOrWhiteSpace($queryName) -or $resultText -match [regex]::Escape($queryName))) {
                    $url
                    $eligibleResultCount++
                    if ($eligibleResultCount -ge 3) { break }
                }
            }
        }
    ) | Select-Object -Unique -First 8
    $secondaryUrls = @(
        @($prioritySecondaryUrls)
        foreach ($response in $secondaryResponses) {
            $queryName = [regex]::Match([string]$response.query, '^"(?<name>[^"]+)"').Groups['name'].Value
            $eligibleResultCount = 0
            foreach ($result in @($response.results)) {
                $url = [string]$result.url
                $resultText = [string]$result.title + ' ' + [string]$result.snippet + ' ' + $url
                if ($url -match '^https?://' -and $url -notmatch $excludedHostPattern -and
                    ([string]::IsNullOrWhiteSpace($queryName) -or $resultText -match [regex]::Escape($queryName))) {
                    $url
                    $eligibleResultCount++
                    if ($eligibleResultCount -ge 3) { break }
                }
            }
        }
    ) | Select-Object -Unique -First 16
    $prioritySecondaryPages = @(
        if (Test-ResourcePackBudget -ReserveSeconds 12) {
            @($prioritySecondaryUrls | Select-Object -First 6) | ForEach-Object -Parallel {
                try {
                    $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                    [pscustomobject]@{
                        url = [string]$page.url
                        title = [string]$page.title
                        emails = @($page.emails)
                        phones = @($page.phones | Select-Object -First 5)
                        links = @($page.links)
                        excerpt = [string]$page.text
                    }
                }
                catch { $null }
            } -ThrottleLimit 3 | Where-Object { $null -ne $_ }
        }
    )
    $fallbackSecondaryUrls = @($secondaryUrls | Where-Object { $_ -notin $prioritySecondaryUrls })
    $fallbackSecondaryPages = @(
        if (Test-ResourcePackBudget -ReserveSeconds 8) {
            $fallbackSecondaryUrls | ForEach-Object -Parallel {
                try {
                    $page = (& $using:pageScript -Url $_ -MaximumCharacters 16000 -ContextTerms $using:contextTerms) | ConvertFrom-Json
                    [pscustomobject]@{
                        url = [string]$page.url
                        title = [string]$page.title
                        emails = @($page.emails)
                        phones = @($page.phones | Select-Object -First 5)
                        links = @($page.links)
                        excerpt = [string]$page.text
                    }
                }
                catch { $null }
            } -ThrottleLimit 4 | Where-Object { $null -ne $_ }
        }
    )
    $secondaryPages = @($prioritySecondaryPages) + @($fallbackSecondaryPages)
    $resourcePages = @($resourcePages) + @($secondaryPages)
}
$searchResponses = @($searchResponses) + @($secondaryResponses)

$nonDocumentPages = @($resourcePages | Where-Object { ([string]$_.url) -notmatch '(?i)\.pdf(?:$|\?)' })
$documentPageGroups = @(
    foreach ($page in @($resourcePages | Where-Object { ([string]$_.url) -match '(?i)\.pdf(?:$|\?)' })) {
        $pageUri = $null
        if (-not [uri]::TryCreate([string]$page.url, [UriKind]::Absolute, [ref]$pageUri)) { continue }
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($pageUri.AbsolutePath)
        $revision = 0
        if ($baseName -match '(?i)(?:rev(?:ision)?|ver(?:sion)?|v)[-_ ]*0*(?<revision>\d+)') { $revision = [int]$Matches['revision'] }
        $family = $baseName -replace '(?i)(?:rev(?:ision)?|ver(?:sion)?|v)[-_ ]*0*\d+', ''
        $family = (($family -replace '(?i)20\d{2}[-_ ](?:0?[1-9]|1[0-2])(?:[-_ ](?:0?[1-9]|[12]\d|3[01]))?', '') -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
        [pscustomobject]@{ page = $page; family = "$($pageUri.Host)|$family"; revision = $revision }
    }
)
$latestDocumentPages = @($documentPageGroups |
    Group-Object family |
    ForEach-Object { $_.Group | Sort-Object revision -Descending | Select-Object -First 1 -ExpandProperty page })
$resourcePages = @($nonDocumentPages) + @($latestDocumentPages)
$seenPageUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$finalPages = @(
    foreach ($page in @($resourcePages)) {
        if ([string]::IsNullOrWhiteSpace([string]$page.url) -or -not $seenPageUrls.Add([string]$page.url)) { continue }
        $pageExcerpt = [string]$page.excerpt
        [ordered]@{
            url = [string]$page.url
            title = [string]$page.title
            emails = @($page.emails)
            phones = @($page.phones)
            excerpt = if ($pageExcerpt.Length -le 10000) { $pageExcerpt } else { $pageExcerpt.Substring(0, 10000) }
        }
    }
)
$officialPhones = @($finalPages | ForEach-Object { @($_.phones) } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique -First 5)
$allResults = @($searchResponses | ForEach-Object { @($_.results) })
$verifiedWebsiteRoot = $null
foreach ($candidate in @($scoredRoots | Sort-Object score -Descending)) {
    if ($candidate.strongIdentity) {
        $verifiedWebsiteRoot = [string]$candidate.root
        break
    }
}

[ordered]@{
    companyName = $CompanyName
    companyNumber = $CompanyNumber
    tradingName = $TradingName
    websiteUrl = if ([string]::IsNullOrWhiteSpace($verifiedWebsiteRoot)) { $null } else { "$verifiedWebsiteRoot/" }
    officialPhones = @($officialPhones)
    companyAliases = @($contextTerms)
    researchGoal = $ResearchGoal
    searches = @($searchResponses | Select-Object query, error)
    identityDiagnostics = [ordered]@{
        registryAliases = @($registryAliases)
        registeredAddresses = @($registeredAddresses)
        registeredPostcodes = @($registryPostcodes)
        identityAnchor = $identityAnchor
        bridgeSearches = @($bridgeResponses | ForEach-Object {
            [ordered]@{
                query = [string]$_.query
                error = [string]$_.error
                resultUrls = @($_.results | Select-Object -ExpandProperty url -First 5)
            }
        })
        bridgeResults = @($bridgeCandidateResults | Select-Object title, url, snippet)
        bridgeWebsiteRoots = @($bridgeWebsiteRoots)
        candidateRoots = @($identityCandidateRoots)
        scoredRoots = @($scoredRoots)
    }
    candidateNames = @($candidateNames)
    contactDiagnostics = [ordered]@{
        maxElapsedSeconds = $MaxElapsedSeconds
        elapsedSeconds = [Math]::Round($script:resourcePackStopwatch.Elapsed.TotalSeconds, 2)
        budgetExhausted = -not (Test-ResourcePackBudget)
        knownResourceUrls = @($knownResourceUrls)
        openedKnownResourceUrls = @($knownResourcePages | Select-Object -ExpandProperty url)
        priorityQueries = @($prioritySecondaryQueries)
        prioritySearches = @($prioritySecondaryResponses | ForEach-Object {
            [ordered]@{
                query = [string]$_.query
                error = [string]$_.error
                resultUrls = @($_.results | Select-Object -ExpandProperty url -First 10)
            }
        })
        priorityUrls = @($prioritySecondaryUrls)
        secondaryUrls = @($secondaryUrls)
        openedSecondaryUrls = @($secondaryPages | Select-Object -ExpandProperty url)
        roleCandidates = @($roleCandidates | Select-Object name, role, url, sourceRank, roleRank)
    }
    results = @($allResults | Select-Object title, url, snippet)
    pages = @($finalPages)
} | ConvertTo-Json -Depth 7
