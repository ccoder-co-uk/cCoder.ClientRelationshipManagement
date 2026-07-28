param(
    [Parameter(Mandatory = $true)]
    [string]$Query,

    [ValidateRange(1, 10)]
    [int]$Limit = 8,

    [switch]$PreferHighQuality,

    [ValidateRange(5, 60)]
    [int]$MaxElapsedSeconds = 24
)

$searchStopwatch = [Diagnostics.Stopwatch]::StartNew()
function Test-SearchBudget {
    $searchStopwatch.Elapsed.TotalSeconds -lt $MaxElapsedSeconds
}

$effectiveQuery = ($Query -replace '\(\s*\d+\s*/\s*\d+\s*\)', ' ' -replace '\s+', ' ').Trim()
$encodedQuery = [uri]::EscapeDataString($effectiveQuery)
$requestHeaders = @{
    'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126 Safari/537.36'
    'Accept-Language' = 'en-GB,en;q=0.9'
}
$providerNotes = [System.Collections.Generic.List[string]]::new()
$results = @()

# Identity discovery is the one search that must work before any role or email
# query can be meaningful. Startpage currently returns useful UK company
# results when the older HTML/RSS providers degrade or ignore the query. Keep
# this to the single official-site lookup so a contact run does not fan out a
# burst of requests to the provider.
if ((Test-SearchBudget) -and ($PreferHighQuality -or $effectiveQuery -match '(?i)\bofficial\s+website\b')) {
    try {
        # Search engines tend to treat the literal phrase "official website"
        # as low-value boilerplate, and quoted registered names can trigger an
        # anti-bot interstitial. The caller still receives its original query;
        # only the provider terms are simplified for identity discovery.
        $identityTerms = ($effectiveQuery -replace '(?i)\bofficial\s+website\b', ' ' -replace '["'']', ' ' -replace '\s+', ' ').Trim()
        $startpageQuery = [uri]::EscapeDataString($identityTerms)
        $startpageUri = "https://www.startpage.com/sp/search?query=$startpageQuery"
        $startpageResponse = Invoke-WebRequest -Uri $startpageUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 8
        if ($startpageResponse.Content -notmatch '(?i)<title[^>]*>\s*Startpage (?:Blocked|Captcha)\s*</title>') {
            $resultAnchors = [regex]::Matches(
                $startpageResponse.Content,
                '<a(?<attrs>[^>]*class="[^"]*\bresult-title\b[^"]*"[^>]*)>(?<body>[\s\S]*?)</a>',
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            $results = @(
                foreach ($anchor in $resultAnchors) {
                    $urlMatch = [regex]::Match($anchor.Groups['attrs'].Value, 'href="(?<url>https?://[^"]+)"', 'IgnoreCase')
                    if (-not $urlMatch.Success) { continue }
                    $resultUrl = [System.Net.WebUtility]::HtmlDecode($urlMatch.Groups['url'].Value)
                    if ($resultUrl -match '^https?://(?:[^/]+\.)?startpage\.com(?:/|$)') { continue }
                    $title = [System.Net.WebUtility]::HtmlDecode(
                        ($anchor.Groups['body'].Value -replace '(?is)<style[^>]*>.*?</style>', ' ' -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
                    if ([string]::IsNullOrWhiteSpace($title)) { continue }
                    [ordered]@{ title = $title; url = $resultUrl; snippet = $title }
                    if ($results.Count -ge $Limit) { break }
                }
            ) | Select-Object -First $Limit
        }
    }
    catch {
        $providerNotes.Add("Startpage unavailable: $($_.Exception.Message)")
    }
}

# A preferred Startpage result set is already the higher-quality answer the
# caller requested. Do not then spend up to several provider timeouts merging
# lower-quality HTML/RSS results into it.
if ($PreferHighQuality -and $results.Count -gt 0) {
    [ordered]@{ query = $effectiveQuery; results = @($results | Select-Object -First $Limit) } |
        ConvertTo-Json -Depth 5
    return
}

# The direct DuckDuckGo HTML endpoint can silently return a stale result set
# that only reflects the first query token.  The read-only text proxy already
# used by Get-PublicWebPage preserves the real DuckDuckGo result blocks and
# redirect targets, so use it whenever the preferred provider produced no
# parseable answer. "Prefer high quality" is an ordering rule, not permission
# to turn a transient provider miss into an empty research result.
if ($results.Count -eq 0 -and (Test-SearchBudget)) {
    try {
        $jinaDuckDuckGoUri = "https://r.jina.ai/http://html.duckduckgo.com/html/?q=$encodedQuery"
        $jinaResponse = Invoke-WebRequest -Uri $jinaDuckDuckGoUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 10
        $jinaMatches = [regex]::Matches(
            $jinaResponse.Content,
            '(?ms)^## \[(?<title>.*?)\]\((?<redirect>.*?)\)\s*(?<body>.*?)(?=^## |\z)')
        $jinaResults = @(
            foreach ($match in $jinaMatches) {
                $redirectUrl = [System.Net.WebUtility]::HtmlDecode($match.Groups['redirect'].Value)
                $resultUrl = $redirectUrl
                if ($redirectUrl -match '[?&]uddg=(?<target>[^&]+)') {
                    $resultUrl = [uri]::UnescapeDataString($Matches['target'])
                }
                if ($resultUrl -notmatch '^https?://' -or
                    $resultUrl -match '^https?://(?:[^/]+\.)?duckduckgo\.com(?:/|$)') {
                    continue
                }
                $title = [System.Net.WebUtility]::HtmlDecode(
                    ($match.Groups['title'].Value -replace '\*', '' -replace '\s+', ' ').Trim())
                $snippet = [System.Net.WebUtility]::HtmlDecode(
                    ($match.Groups['body'].Value `
                        -replace '!\[[^\]]*\]\([^)]*\)', ' ' `
                        -replace '\[(?<text>[^\]]+)\]\([^)]*\)', '${text}' `
                        -replace '[*_`]', '' `
                        -replace '\s+', ' ').Trim())
                if ([string]::IsNullOrWhiteSpace($title)) { continue }
                [ordered]@{ title = $title; url = $resultUrl; snippet = $snippet }
            }
        ) | Select-Object -First $Limit
        $jinaMergeSeen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $results = @(@($jinaResults) + @($results) |
            Where-Object { $jinaMergeSeen.Add([string]$_.url) } |
            Select-Object -First $Limit)
    }
    catch {
        $providerNotes.Add("DuckDuckGo text proxy unavailable: $($_.Exception.Message)")
    }
}

# A few relevant results are enough for a bounded evidence pack. Continuing
# through every provider after a useful answer only reshuffles the same pages
# and makes total research time depend on remote provider health.
if ($results.Count -ge [Math]::Min(3, $Limit)) {
    [ordered]@{ query = $effectiveQuery; results = @($results | Select-Object -First $Limit) } |
        ConvertTo-Json -Depth 5
    return
}

if ($results.Count -eq 0 -and (Test-SearchBudget)) {
    try {
        $duckDuckGoUri = "https://html.duckduckgo.com/html/?q=$encodedQuery"
        $response = Invoke-WebRequest -Uri $duckDuckGoUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 6
        $linkMatches = [regex]::Matches($response.Content, '<a rel="nofollow" class="result__a" href="(?<url>[^"]+)">(?<title>[\s\S]*?)</a>')
        $snippetMatches = [regex]::Matches($response.Content, '<a class="result__snippet"[^>]*>(?<snippet>[\s\S]*?)</a>')
        $results = @(
            for ($index = 0; $index -lt [Math]::Min($Limit, $linkMatches.Count); $index++) {
                $match = $linkMatches[$index]
                $resultUrl = [System.Net.WebUtility]::HtmlDecode($match.Groups['url'].Value)
                if ($resultUrl -match '[?&]uddg=(?<target>[^&]+)') {
                    $resultUrl = [uri]::UnescapeDataString($Matches['target'])
                }
                [ordered]@{
                    title = [System.Net.WebUtility]::HtmlDecode(($match.Groups['title'].Value -replace '<[^>]+>', '').Trim())
                    url = $resultUrl
                    snippet = if ($index -lt $snippetMatches.Count) {
                        [System.Net.WebUtility]::HtmlDecode(($snippetMatches[$index].Groups['snippet'].Value -replace '<[^>]+>', '').Trim())
                    } else { '' }
                }
            }
        )
    }
    catch {
        $providerNotes.Add("DuckDuckGo unavailable: $($_.Exception.Message)")
    }
}


if ($results.Count -ge [Math]::Min(3, $Limit)) {
    [ordered]@{ query = $effectiveQuery; results = @($results | Select-Object -First $Limit) } |
        ConvertTo-Json -Depth 5
    return
}

if ($results.Count -eq 0 -and (Test-SearchBudget)) {
    try {
        $braveUri = "https://search.brave.com/search?q=$encodedQuery&source=web"
        $braveResponse = Invoke-WebRequest -Uri $braveUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 6
        $seenUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $braveMatches = [regex]::Matches(
            $braveResponse.Content,
            '<a[^>]+href="(?<url>https?[^" ]+)"[^>]*>(?<body>[\s\S]*?)</a>',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $results = @(
            foreach ($match in $braveMatches) {
                $resultUrl = [System.Net.WebUtility]::HtmlDecode($match.Groups['url'].Value)
                $title = [System.Net.WebUtility]::HtmlDecode(
                    ($match.Groups['body'].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
                $isSearchProviderUrl = $resultUrl -match '^https?://(?:[^/]+\.)?brave\.(?:com|app)/'
                $isDuplicate = -not $seenUrls.Add($resultUrl)
                if ($isSearchProviderUrl -or [string]::IsNullOrWhiteSpace($title) -or $isDuplicate) {
                    continue
                }
                [ordered]@{ title = $title; url = $resultUrl; snippet = '' }
                if ($seenUrls.Count -ge $Limit) { break }
            }
        )
    }
    catch {
        $providerNotes.Add("Brave unavailable: $($_.Exception.Message)")
    }
}

# A complete text-proxy result set is already sufficient for bounded research.
# Returning here avoids paying for several slower providers merely to reshuffle
# an answer that has already reached the caller's requested limit.
if ($results.Count -ge $Limit) {
    [ordered]@{ query = $effectiveQuery; results = @($results | Select-Object -First $Limit) } |
        ConvertTo-Json -Depth 5
    return
}

if (Test-SearchBudget) { try {
    $yahooResponse = $null
    $yahooErrors = [System.Collections.Generic.List[string]]::new()
    foreach ($attempt in 1..1) {
        try {
            $requestNonce = [guid]::NewGuid().ToString('N')
            $yahooUri = "https://search.yahoo.com/search?p=$encodedQuery&ei=UTF-8&fr=yfp-t&x=$requestNonce"
            $yahooResponse = Invoke-WebRequest -Uri $yahooUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 6
            break
        }
        catch {
            $yahooErrors.Add($_.Exception.Message)
        }
    }
    if ($null -eq $yahooResponse) { throw ($yahooErrors -join ' | ') }
    $yahooSeenUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $yahooMatches = [regex]::Matches(
        $yahooResponse.Content,
        '<a[^>]+href="(?<url>[^"]+)"[^>]*>(?<body>[\s\S]*?)</a>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $yahooResults = @(
        foreach ($match in $yahooMatches) {
            $redirectUrl = [System.Net.WebUtility]::HtmlDecode($match.Groups['url'].Value)
            if ($redirectUrl -notmatch '/RU=(?<target>https?%3a.*?)/RK=') { continue }
            $resultUrl = [uri]::UnescapeDataString($Matches['target'])
            if ($resultUrl -match '^https?://(?:[^/]+\.)?yahoo\.com(?:/|$)' -or -not $yahooSeenUrls.Add($resultUrl)) { continue }
            $title = [System.Net.WebUtility]::HtmlDecode(
                ($match.Groups['body'].Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
            if ([string]::IsNullOrWhiteSpace($title) -or $title -match '^(?:Yahoo|Home|Mail|News|Finance|Sports|Weather|Shopping)$') { continue }
            [ordered]@{ title = $title; url = $resultUrl; snippet = '' }
            if ($yahooSeenUrls.Count -ge $Limit) { break }
        }
    )

    $leadershipQuery = $effectiveQuery -match '(?i)\b(?:CFO|Chief Financial Officer|Finance Director|Financial Controller|Head of Finance|Procurement Director|Managing Director)\b'
    $mergeCandidates = if ($leadershipQuery) { @($yahooResults) + @($results) } else { @($results) + @($yahooResults) }
    $mergeSeenUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $results = @($mergeCandidates | Where-Object { $mergeSeenUrls.Add([string]$_.url) } | Select-Object -First $Limit)
}
catch {
    $providerNotes.Add("Yahoo unavailable: $($_.Exception.Message)")
} }

if ($results.Count -lt $Limit -and (Test-SearchBudget)) {
    $bingUri = "https://www.bing.com/search?q=$encodedQuery&format=rss&count=$Limit"
    try {
        $bingResponse = Invoke-WebRequest -Uri $bingUri -UseBasicParsing -Headers $requestHeaders -TimeoutSec 6
        [xml]$feed = $bingResponse.Content
        $allSeenUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($existingResult in @($results)) { $null = $allSeenUrls.Add([string]$existingResult.url) }
        $bingResults = @($feed.rss.channel.item) | ForEach-Object {
            $resultUrl = [string]$_.link
            if ([string]::IsNullOrWhiteSpace($resultUrl) -or -not $allSeenUrls.Add($resultUrl)) { return }
            [ordered]@{ title = [string]$_.title; url = $resultUrl; snippet = ([string]$_.description -replace '<[^>]+>', '').Trim() }
        }
        $results = @(@($results) + @($bingResults)) | Select-Object -First $Limit
    }
    catch {
        $providerNotes.Add("Bing unavailable: $($_.Exception.Message)")
    }
}

$results = @($results | Where-Object {
    $resultUrl = [string]$_.url
    -not [string]::IsNullOrWhiteSpace($resultUrl) -and
    $resultUrl -notmatch '^https?://(?:[^/]+\.)?(?:bing\.com/aclick|googleadservices\.com|duckduckgo\.com/y\.js|search\.yahoo\.com)(?:/|\?|$)'
}) | Select-Object -First $Limit

if ($results.Count -eq 0) {
    $note = if ($providerNotes.Count -eq 0) {
        'No parseable public search results were returned from DuckDuckGo, Brave, Yahoo, or Bing.'
    } else {
        'No parseable public search results were returned. ' + ($providerNotes -join ' ')
    }
    [ordered]@{ query = $effectiveQuery; results = @(); note = $note } | ConvertTo-Json -Depth 5
}
else {
    [ordered]@{ query = $effectiveQuery; results = @($results) } | ConvertTo-Json -Depth 5
}
