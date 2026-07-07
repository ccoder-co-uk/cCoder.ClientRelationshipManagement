param(
    [Parameter(Mandatory = $true)]
    [string]$Method,

    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter()]
    [string]$BodyJson
)

$baseUrl = $env:CRM_AGENT_API_BASE_URL
$token = $env:CRM_AGENT_EXECUTION_TOKEN
if ([string]::IsNullOrWhiteSpace($baseUrl)) {
    throw "CRM_AGENT_API_BASE_URL is not set."
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw "CRM_AGENT_EXECUTION_TOKEN is not set."
}

$headers = @{
    "Authorization" = "Bearer $token"
}

$uri = [System.Uri]::new([System.Uri]::new($baseUrl.TrimEnd('/') + '/'), $Path.TrimStart('/'))

if ([string]::IsNullOrWhiteSpace($BodyJson)) {
    Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
}
else {
    Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $BodyJson
}
