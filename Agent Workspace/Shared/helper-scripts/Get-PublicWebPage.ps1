param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [ValidateRange(1000, 30000)]
    [int]$MaximumCharacters = 15000,

    [string]$Highlight,

    [string[]]$ContextTerms
)

$uri = $null
if (-not [uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http', 'https')) {
    throw 'Provide an absolute public HTTP or HTTPS URL.'
}
if ($uri.IsLoopback -or $uri.Host -eq 'localhost' -or $uri.Host -match '^(10\.|127\.|169\.254\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)') {
    throw 'Private and loopback web addresses are not permitted.'
}

$isPdf = $uri.AbsolutePath -match '(?i)\.pdf$'
$requestUrl = if ($isPdf) {
    'https://r.jina.ai/http://' + $uri.Authority + $uri.PathAndQuery
} else {
    $uri.AbsoluteUri
}

function Invoke-BoundedPublicRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RequestUrl,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,

        [int]$MaximumResponseBytes = 6291456
    )

    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $true
    $handler.MaxAutomaticRedirections = 5
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $RequestUrl)
    [void]$request.Headers.UserAgent.ParseAdd('Mozilla/5.0 (compatible; CorporateLinXResearch/1.0)')
    $response = $null
    $stream = $null
    $memory = $null
    try {
        $response = $client.Send($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead)
        $response.EnsureSuccessStatusCode()
        if ($response.Content.Headers.ContentLength.HasValue -and
            $response.Content.Headers.ContentLength.Value -gt $MaximumResponseBytes) {
            throw "Public page response exceeded the $MaximumResponseBytes-byte resource-pack limit."
        }

        $stream = $response.Content.ReadAsStream()
        $memory = [IO.MemoryStream]::new()
        $buffer = [byte[]]::new(16384)
        $totalBytes = 0
        while (($bytesRead = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            if ($totalBytes + $bytesRead -gt $MaximumResponseBytes) {
                throw "Public page response exceeded the $MaximumResponseBytes-byte resource-pack limit."
            }
            $memory.Write($buffer, 0, $bytesRead)
            $totalBytes += $bytesRead
        }

        $charset = [string]$response.Content.Headers.ContentType.CharSet
        $encoding = [Text.Encoding]::UTF8
        if (-not [string]::IsNullOrWhiteSpace($charset)) {
            try { $encoding = [Text.Encoding]::GetEncoding($charset.Trim('"')) } catch { }
        }
        [pscustomobject]@{
            Content = $encoding.GetString($memory.ToArray())
            EffectiveUri = $response.RequestMessage.RequestUri
        }
    }
    finally {
        if ($null -ne $memory) { $memory.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
        $client.Dispose()
        $handler.Dispose()
    }
}

try {
    $response = Invoke-BoundedPublicRequest -RequestUrl $requestUrl -TimeoutSeconds $(if ($isPdf) { 15 } else { 6 })
}
catch {
    if ($isPdf) {
        throw "Public PDF retrieval failed through the read-only text extractor: $($_.Exception.Message)"
    }
    try {
        $proxyUrl = 'https://r.jina.ai/http://' + $uri.Authority + $uri.PathAndQuery
        $response = Invoke-BoundedPublicRequest -RequestUrl $proxyUrl -TimeoutSeconds 10
    }
    catch {
        throw "Public page retrieval failed directly and through the read-only text fallback: $($_.Exception.Message)"
    }
}

$content = [string]$response.Content
$effectiveUri = $uri
if ($null -ne $response.EffectiveUri -and $response.EffectiveUri.Host -notmatch '(?i)^r\.jina\.ai$') {
    $effectiveUri = $response.EffectiveUri
}
$titleMatch = [regex]::Match($content, '<title[^>]*>(?<title>[\s\S]*?)</title>', 'IgnoreCase')
$pageTitle = if ($titleMatch.Success) {
    [System.Net.WebUtility]::HtmlDecode(($titleMatch.Groups['title'].Value -replace '\s+', ' ').Trim())
} else {
    [regex]::Match($content, '(?im)^Title:\s*(?<title>[^\r\n]+)').Groups['title'].Value.Trim()
}
$decodedProtectedEmails = @(
    foreach ($match in [regex]::Matches(
        $content,
        '(?i)(?:email-protection#|data-cfemail=["''])(?<hex>[0-9a-f]{6,})')) {
        try {
            $hex = $match.Groups['hex'].Value
            $key = [Convert]::ToByte($hex.Substring(0, 2), 16)
            $bytes = for ($index = 2; $index -lt $hex.Length; $index += 2) {
                [Convert]::ToByte($hex.Substring($index, 2), 16) -bxor $key
            }
            $decoded = [Text.Encoding]::UTF8.GetString([byte[]]$bytes)
            [regex]::Match($decoded, '(?i)[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}').Value
        }
        catch {
            continue
        }
    }
)
$emailScanContent = $content `
    -replace '(?i)(?<=[a-z0-9])\s*-\s*(?=[a-z0-9])', '-' `
    -replace '(?i)(?<=[a-z0-9])\s*\.\s*(?=[a-z])', '.' `
    -replace '(?i)(?<=[a-z0-9._%+\-])\s*@\s*(?=[a-z0-9])', '@'
$allEmails = @(
    [regex]::Matches($emailScanContent, '(?i)(?:mailto:)?[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}') |
    ForEach-Object { $_.Value -replace '^(?i)mailto:', '' } |
    Where-Object { $_ -notmatch '(?i)\.(?:webp|png|jpe?g|gif|svg|css|js)$' -and $_ -notmatch '(?i)^(?:you|name|user)@example\.' }
    $decodedProtectedEmails
) | Select-Object -Unique
$highlightEmail = [regex]::Match([string]$Highlight, '(?i)[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}').Value
$phones = [regex]::Matches($content, '(?<!\d)(?:\+44\s?\d{2,4}|0\d{2,4})[\s()\-]*\d{3,4}[\s\-]*\d{3,4}(?!\d)') |
    ForEach-Object { $_.Value.Trim() } | Select-Object -Unique -First 20
$links = @(
    $linkCandidates = @(
        [regex]::Matches($content, '(?i)href\s*=\s*["''](?<url>[^"'']+)["'']') |
            ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Groups['url'].Value) }
        [regex]::Matches($content, '(?i)\[[^\]]*\]\((?<url>https?://[^\s)]+|/[^\s)]+)\)') |
            ForEach-Object { $_.Groups['url'].Value }
    )
    foreach ($candidate in $linkCandidates) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or
            $candidate -match '^(?i)(?:mailto|tel|javascript|data):' -or
            $candidate.StartsWith('#')) {
            continue
        }
        try {
            $resolved = [uri]::new($effectiveUri, $candidate)
            if ($resolved.Scheme -in @('http', 'https')) {
                $resolved.AbsoluteUri
            }
        }
        catch {
            continue
        }
    }
) | Select-Object -Unique -First 500
$text = $content -replace '(?is)<script[^>]*>.*?</script>', ' ' -replace '(?is)<style[^>]*>.*?</style>', ' '
$text = $text `
    -replace '(?i)(?<=[a-z0-9])\s*-\s*(?=[a-z0-9])', '-' `
    -replace '(?i)(?<=[a-z0-9])\s*\.\s*(?=[a-z])', '.' `
    -replace '(?i)(?<=[a-z0-9._%+\-])\s*@\s*(?=[a-z0-9])', '@'
$text = [System.Net.WebUtility]::HtmlDecode(($text -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim())
$fullText = $text

# Build source-agnostic evidence passages from the complete normalized page or
# document before applying the output limit. Long annual reports and survey
# PDFs often place the useful company/person/email passage far beyond the first
# few pages. Rank bounded contextual windows instead of assuming a fixed layout.
$normalizedContextTerms = @(
    foreach ($term in @($ContextTerms)) {
        $cleanTerm = ([string]$term -replace '(?i)\s+(?:LIMITED|LTD|PLC|P\.L\.C|LLP)\.?$', '' -replace '\s+', ' ').Trim()
        if ($cleanTerm.Length -ge 3) { $cleanTerm }
        @($cleanTerm -split '[^A-Za-z0-9]+' | Where-Object { $_.Length -ge 4 })
    }
) | Select-Object -Unique
$evidenceSignalPattern = '(?i)chief financial officer|finance director|financial controller|head of finance|procurement director|chief executive officer|managing director|\bowner\b|\bcfo\b|\bceo\b|[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}'
$passageCandidates = @(
    $signalMatches = [regex]::Matches($fullText, $evidenceSignalPattern)
    $focusLocations = @(
        foreach ($contextTerm in $normalizedContextTerms) {
            [regex]::Matches($fullText, [regex]::Escape($contextTerm), [Text.RegularExpressions.RegexOptions]::IgnoreCase) |
                Select-Object -First 40 |
                ForEach-Object Index
        }
        for ($signalIndex = 0; $signalIndex -lt [Math]::Min(300, $signalMatches.Count); $signalIndex++) {
            $signalMatches[$signalIndex].Index
        }
    ) | Sort-Object -Unique
    foreach ($focusLocation in $focusLocations) {
        $start = [Math]::Max(0, $focusLocation - 700)
        $length = [Math]::Min(2200, $fullText.Length - $start)
        $passageText = ($fullText.Substring($start, $length) -replace '\s+', ' ').Trim()
        $hasRole = [regex]::IsMatch($passageText, '(?i)chief financial officer|finance director|financial controller|head of finance|procurement director|chief executive officer|managing director|\bowner\b|\bcfo\b|\bceo\b')
        $hasEmail = [regex]::IsMatch($passageText, '(?i)[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}')
        $contextMatches = @($normalizedContextTerms | Where-Object { $passageText.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count
        $score = ($contextMatches * 60) + $(if ($hasRole) { 80 } else { 0 }) + $(if ($hasEmail) { 40 } else { 0 }) + $(if ($hasRole -and $hasEmail) { 120 } else { 0 })
        if (-not [string]::IsNullOrWhiteSpace($Highlight) -and
            $passageText.IndexOf($Highlight, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $score += 500 }
        [pscustomobject]@{ text = $passageText; score = $score; index = $start }
    }
) | Group-Object { [Math]::Floor($_.index / 700) } |
    ForEach-Object { $_.Group | Sort-Object score -Descending | Select-Object -First 1 } |
    Sort-Object @{ Expression = 'score'; Descending = $true }, @{ Expression = 'index'; Ascending = $true } |
    Select-Object -First 12
$passages = @($passageCandidates | Select-Object text, score)
$passageEmails = @(
    foreach ($passage in $passages) {
        [regex]::Matches([string]$passage.text, '(?i)[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}') | ForEach-Object Value
    }
) | Select-Object -Unique
$emails = @(
    if (-not [string]::IsNullOrWhiteSpace($highlightEmail)) {
        @($allEmails | Where-Object { $_ -eq $highlightEmail })
    }
    @($passageEmails)
    @($allEmails)
) | Select-Object -Unique -First $(if ($isPdf) { 100 } else { 40 })

if (-not [string]::IsNullOrWhiteSpace($Highlight)) {
    $highlightIndex = $text.IndexOf($Highlight.Trim(), [StringComparison]::OrdinalIgnoreCase)
    if ($highlightIndex -ge 0 -and $text.Length -gt $MaximumCharacters) {
        $contextStart = [Math]::Max(0, $highlightIndex - [Math]::Floor($MaximumCharacters / 3))
        $contextLength = [Math]::Min($MaximumCharacters, $text.Length - $contextStart)
        $text = $text.Substring($contextStart, $contextLength)
    }
}
elseif (@($ContextTerms | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -and $passages.Count -gt 0) {
    $text = (@($passages | ForEach-Object { $_.text }) -join ' ... ')
}
if ($text.Length -gt $MaximumCharacters) { $text = $text.Substring(0, $MaximumCharacters) }

[ordered]@{
    url = $effectiveUri.AbsoluteUri
    title = $pageTitle
    emails = @($emails)
    phones = @($phones)
    links = @($links)
    passages = @($passages)
    text = $text
} | ConvertTo-Json -Depth 5
