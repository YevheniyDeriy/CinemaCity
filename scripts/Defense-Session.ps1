# Dot-source from E:\CinemaCity while DefenseDemo is running. PowerShell 7+.
$defense = Get-Content (Join-Path $PSScriptRoot '../.local/defense-demo.json') -Raw | ConvertFrom-Json
$base = 'http://127.0.0.1:5000'
$movieId = $defense.movieId
$seriesId = $defense.seriesId
function Send-DefenseRequest {
    param([string] $Method = 'GET', [Parameter(Mandatory)] [string] $Path, [hashtable] $Headers = @{}, [object] $Body, [switch] $PassThru)
    $request = @{ Uri = "$base$Path"; Method = $Method; Headers = $Headers; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $request.Body = ConvertTo-Json $Body -Depth 20 -Compress; $request.ContentType = 'application/json' }
    $response = Invoke-WebRequest @request
    Write-Host "$Method $Path -> $($response.StatusCode)"
    foreach ($key in @('Location','Retry-After','Cache-Control','X-Correlation-Id')) {
        if ($response.Headers.ContainsKey($key)) { Write-Host "$key : $($response.Headers[$key] -join ', ')" }
    }
    if ($response.Content) { Write-Host $response.Content }
    if ($PassThru) { return $response }
}
$demoPassword = 'OriginalCinemaCity123!'
$ownerSession = Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email='original.owner@cinemacity.tests.local'; password=$demoPassword } | ConvertTo-Json)
$adminSession = Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email='original.admin@cinemacity.tests.local'; password=$demoPassword } | ConvertTo-Json)
$ownerHeaders = @{ Authorization = "Bearer $($ownerSession.accessToken)" }
$adminHeaders = @{ Authorization = "Bearer $($adminSession.accessToken)" }
Write-Host "Defense session ready. Movie: $movieId; Source DB: $($defense.sourceDatabase); Community DB: $($defense.communityDatabase)."
