param([string] $SourceConnection = $env:CINEMACITY_SOURCE_CONNECTION, [string] $PgDumpPath, [switch] $SkipBuild)
. "$PSScriptRoot/LocalEnvironment.ps1"
$root = Get-CinemaCityRoot
$values = Read-CinemaCityEnvironment
if (!$SourceConnection -and $values.ContainsKey('CINEMACITY_SOURCE_CONNECTION')) { $SourceConnection = $values['CINEMACITY_SOURCE_CONNECTION'] }
if (!$SourceConnection) { throw 'Supply -SourceConnection for your existing CinemaCity database. The initializer will preserve its users, movies and community data.' }
if (!$SkipBuild) { & dotnet build (Join-Path $root 'CinemaCity.slnx'); if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
$values['CINEMACITY_SOURCE_CONNECTION'] = $SourceConnection
foreach ($pair in @(@('CINEMACITY_AUTH_CONNECTION', 'cinemacity_auth'), @('CINEMACITY_CATALOG_CONNECTION', 'cinemacity_catalog'), @('CINEMACITY_COMMUNITY_CONNECTION', 'cinemacity_community'))) {
    if (!$values.ContainsKey($pair[0]) -or !$values[$pair[0]]) { $values[$pair[0]] = Get-ConnectionForDatabase $SourceConnection $pair[1] }
}
if (!$values.ContainsKey('CINEMACITY_MONOLITH_CONNECTION') -or !$values['CINEMACITY_MONOLITH_CONNECTION']) { $values['CINEMACITY_MONOLITH_CONNECTION'] = $SourceConnection }
foreach ($name in @('JWT_SECRET', 'SERVICE_INTERNAL_KEY', 'LAB0_JWT_SECRET')) {
    if (!$values.ContainsKey($name) -or !$values[$name] -or $values[$name].StartsWith('replace-')) {
        $bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(48); $values[$name] = [Convert]::ToBase64String($bytes)
    }
}
if (!$values.ContainsKey('POSTGRES_PASSWORD') -or !$values['POSTGRES_PASSWORD']) { $bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(24); $values['POSTGRES_PASSWORD'] = [Convert]::ToHexString($bytes) }
if (!$values.ContainsKey('POSTGRES_PORT')) { $values['POSTGRES_PORT'] = '5433' }
if (!$values.ContainsKey('CINEMACITY_LAB0_CONNECTION') -or !$values['CINEMACITY_LAB0_CONNECTION']) { $values['CINEMACITY_LAB0_CONNECTION'] = Get-ConnectionForDatabase $SourceConnection 'cinemacity_lab0' }
$dockerSource = [System.Data.Common.DbConnectionStringBuilder]::new(); $dockerSource.set_ConnectionString($SourceConnection)
if ($dockerSource.ContainsKey('Host') -and $dockerSource['Host'] -in @('localhost', '127.0.0.1', '::1')) { $dockerSource['Host'] = 'host.docker.internal' }
$values['CINEMACITY_DOCKER_SOURCE_CONNECTION'] = $dockerSource.ConnectionString
$backupMarker = Join-Path $root '.local/transition-backup.txt'
if (!(Test-Path -LiteralPath $backupMarker)) {
    $backup = Backup-CinemaCityDatabase $SourceConnection $PgDumpPath
    [IO.File]::WriteAllText($backupMarker, $backup)
}
Write-CinemaCityEnvironment $values
Invoke-CinemaCityImport $values
# A separate, one-time baseline copy keeps the Lab 0 demonstration independent of the live cutover data.
$lab0Values = $values.Clone()
$lab0Values['CINEMACITY_MONOLITH_CONNECTION'] = $values['CINEMACITY_LAB0_CONNECTION']
$lab0Values.Remove('CINEMACITY_COMMUNITY_CONNECTION')
Invoke-CinemaCityImport $lab0Values
$sourceUploads = Join-Path $root 'backend/CinemaCity.API/wwwroot/uploads'
$catalogUploads = Join-Path $root 'backend/CinemaCity.CatalogService/wwwroot/uploads'
if (Test-Path -LiteralPath $sourceUploads) {
    foreach ($file in Get-ChildItem -LiteralPath $sourceUploads -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($sourceUploads, $file.FullName)
        $destination = Join-Path $catalogUploads $relative
        if (!(Test-Path -LiteralPath $destination)) { New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null; Copy-Item -LiteralPath $file.FullName -Destination $destination }
    }
}
Write-Host 'Initialized the extracted services from CinemaCity. Secrets are stored only in the ignored .env file.'
