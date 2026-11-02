param([ValidateSet('Services', 'Monolith')] [string] $Mode = 'Services', [switch] $NoFrontend, [switch] $SkipBuild)
. "$PSScriptRoot/LocalEnvironment.ps1"
$root = Get-CinemaCityRoot; $values = Read-CinemaCityEnvironment
foreach ($key in @('CINEMACITY_MONOLITH_CONNECTION', 'JWT_SECRET', 'SERVICE_INTERNAL_KEY')) { if (!$values.ContainsKey($key) -or !$values[$key]) { throw 'Run scripts/Initialize-Local.ps1 first.' } }
$manifest = Join-Path $root '.local/processes.json'
if (Test-Path -LiteralPath $manifest) {
    foreach ($item in @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)) {
        if (Get-Process -Id $item.Id -ErrorAction SilentlyContinue) { throw 'The local stack is already running. Use scripts/Stop-Local.ps1 before restarting.' }
    }
}
if (!$SkipBuild) { & dotnet build (Join-Path $root 'CinemaCity.slnx'); if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
$logs = Join-Path $root '.logs/local'; New-Item -ItemType Directory -Force -Path $logs | Out-Null
$common = @{ 'Jwt__Secret' = $values['JWT_SECRET']; 'Jwt__Issuer' = 'CinemaCity'; 'Jwt__Audience' = 'CinemaCity.Client'; 'Services__InternalKey' = $values['SERVICE_INTERNAL_KEY']; 'ASPNETCORE_ENVIRONMENT' = 'Development'; 'DOTNET_ENVIRONMENT' = 'Development'; 'Services__AuthUrl' = 'http://localhost:5001'; 'Services__CatalogUrl' = 'http://localhost:5002'; 'Services__MonolithUrl' = 'http://localhost:5100'; 'Services__CommunityUrl' = 'http://localhost:5003'; 'Google__ClientId' = ''; 'Google__ClientSecret' = '' }
if ($Mode -eq 'Monolith') {
    if (!$values.ContainsKey('CINEMACITY_LAB0_CONNECTION') -or !$values.ContainsKey('LAB0_JWT_SECRET')) { throw 'Run Initialize-Local.ps1 to prepare the isolated Lab 0 baseline.' }
    $common['Jwt__Secret'] = $values['LAB0_JWT_SECRET']
}
foreach ($pair in @(@('GOOGLE_CLIENT_ID', 'Google__ClientId'), @('GOOGLE_CLIENT_SECRET', 'Google__ClientSecret'), @('TMDB_API_TOKEN', 'Tmdb__ApiToken'))) { if ($values.ContainsKey($pair[0])) { $common[$pair[1]] = $values[$pair[0]] } }
$processes = [Collections.Generic.List[object]]::new()
function Start-CinemaCityApp([string] $Project, [int] $Port, [hashtable] $Extra) {
    $environment = $common.Clone(); $environment['ASPNETCORE_URLS'] = "http://localhost:$Port"
    foreach ($key in $Extra.Keys) { $environment[$key] = [string]$Extra[$key] }
    $folder = Join-Path $root "backend/$Project"; $dll = Join-Path $folder ".build/bin/Debug/net10.0/$Project.dll"
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @("`"$dll`"") -WorkingDirectory $folder -Environment $environment -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs "$Project.log") -RedirectStandardError (Join-Path $logs "$Project.err.log")
    $processes.Add(@{ Id = $process.Id; Name = $Project; StartedAt = $process.StartTime.ToUniversalTime().ToString('o'); StartedAtUtcTicks = $process.StartTime.ToUniversalTime().Ticks })
}
try {
    if ($Mode -eq 'Services') {
        Start-CinemaCityApp 'CinemaCity.AuthService' 5001 @{ 'ConnectionStrings__DefaultConnection' = $values['CINEMACITY_AUTH_CONNECTION']; 'Google__CallbackPath' = '/api/v1/auth/google/signin' }
        Start-CinemaCityApp 'CinemaCity.CatalogService' 5002 @{ 'ConnectionStrings__DefaultConnection' = $values['CINEMACITY_CATALOG_CONNECTION'] }
    }
    $monolithConnection = if ($Mode -eq 'Monolith') { $values['CINEMACITY_LAB0_CONNECTION'] } else { $values['CINEMACITY_MONOLITH_CONNECTION'] }
    if ($Mode -eq 'Monolith') {
        Start-CinemaCityApp 'CinemaCity.API' 5100 @{ 'ConnectionStrings__DefaultConnection' = $monolithConnection; 'Services__Enabled' = 'false' }
    } else {
        if (!$values['CINEMACITY_COMMUNITY_CONNECTION']) { throw 'Set CINEMACITY_COMMUNITY_CONNECTION to a separate database and run the offline importer before starting Services.' }
        Start-CinemaCityApp 'CinemaCity.CommunityService' 5003 @{ 'ConnectionStrings__DefaultConnection' = $values['CINEMACITY_COMMUNITY_CONNECTION'] }
    }
    Start-CinemaCityApp 'CinemaCity.ApiGateway' 5000 @{ 'Gateway__Mode' = $Mode }
    foreach ($port in $(if ($Mode -eq 'Services') { @(5000,5001,5002,5003) } else { @(5000,5100) })) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            try { $response = Invoke-WebRequest "http://localhost:$port/health" -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { }
            Start-Sleep -Milliseconds 250
        }
        if (!$ready) { throw "Service on port $port did not become ready. Check .logs/local." }
    }
    if (!$NoFrontend) {
        $frontend = Join-Path $root 'frontend'
        if (!(Test-Path -LiteralPath (Join-Path $frontend 'node_modules/vite/bin/vite.js'))) { Push-Location $frontend; try { & npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } } finally { Pop-Location } }
        $vite = Join-Path $frontend 'node_modules/vite/bin/vite.js'
        $process = Start-Process -FilePath (Get-Command node).Source -ArgumentList @("`"$vite`"", '--host', '127.0.0.1', '--port', '5173', '--strictPort') -WorkingDirectory $frontend -Environment @{ 'VITE_API_URL' = 'http://localhost:5000' } -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'frontend.log') -RedirectStandardError (Join-Path $logs 'frontend.err.log')
        $processes.Add(@{ Id = $process.Id; Name = 'frontend'; StartedAt = $process.StartTime.ToUniversalTime().ToString('o'); StartedAtUtcTicks = $process.StartTime.ToUniversalTime().Ticks })
    }
    $processes | ConvertTo-Json -AsArray | Set-Content -LiteralPath $manifest -Encoding utf8
    Write-Host "CinemaCity mode: $Mode. Frontend: http://localhost:5173; Gateway: http://localhost:5000."
} catch {
    $processes | ConvertTo-Json -AsArray | Set-Content -LiteralPath $manifest -Encoding utf8
    & "$PSScriptRoot/Stop-Local.ps1"
    throw
}
