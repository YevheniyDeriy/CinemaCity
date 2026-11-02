Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CinemaCityRoot { Split-Path -Parent $PSScriptRoot }

function Read-CinemaCityEnvironment {
    $values = @{}
    $file = Join-Path (Get-CinemaCityRoot) '.env'
    if (Test-Path -LiteralPath $file) {
        foreach ($line in Get-Content -LiteralPath $file -Encoding utf8) {
            if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
                $value = $Matches[2].Trim()
                if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2) }
                $values[$Matches[1]] = $value
            }
        }
    }
    return $values
}

function Write-CinemaCityEnvironment([hashtable] $Values) {
    $path = Join-Path (Get-CinemaCityRoot) '.env'
    $lines = if (Test-Path -LiteralPath $path) { @(Get-Content -LiteralPath $path -Encoding utf8) } else { @() }
    $written = @{}
    $result = @(foreach ($line in $lines) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') {
            $key = $Matches[1]
            if ($Values.ContainsKey($key)) { $written[$key] = $true; "$key=$($Values[$key])" } else { $line }
        } else { $line }
    })
    foreach ($key in @($Values.Keys | Sort-Object)) { if (!$written.ContainsKey($key)) { $result += "$key=$($Values[$key])" } }
    [IO.File]::WriteAllLines($path, $result, [Text.UTF8Encoding]::new($false))
}

function Get-ConnectionForDatabase([string] $Connection, [string] $Database) {
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.set_ConnectionString($Connection)
    if ($builder.ContainsKey('Initial Catalog')) { $builder.Remove('Initial Catalog') | Out-Null }
    $builder['Database'] = $Database
    return $builder.ConnectionString
}

function Invoke-CinemaCityImport([hashtable] $Values) {
    $names = @('CINEMACITY_SOURCE_CONNECTION', 'CINEMACITY_AUTH_CONNECTION', 'CINEMACITY_CATALOG_CONNECTION', 'CINEMACITY_MONOLITH_CONNECTION', 'CINEMACITY_COMMUNITY_CONNECTION')
    $saved = @{}
    try {
        foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name); [Environment]::SetEnvironmentVariable($name, $Values[$name]) }
        $root = Get-CinemaCityRoot
        & dotnet (Join-Path $root 'tools/CinemaCity.DataMigration/.build/bin/Debug/net10.0/CinemaCity.DataMigration.dll')
        if ($LASTEXITCODE -ne 0) { throw 'CinemaCity data import failed. See the error above; existing target data is never overwritten.' }
    } finally { foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
}

function Backup-CinemaCityDatabase([string] $Connection, [string] $PgDumpPath) {
    if (!$PgDumpPath) {
        $command = Get-Command pg_dump -ErrorAction SilentlyContinue
        if ($command) { $PgDumpPath = $command.Source }
    }
    if (!$PgDumpPath -or !(Test-Path -LiteralPath $PgDumpPath)) { throw 'Pass -PgDumpPath with the PostgreSQL pg_dump executable before the first transition.' }
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new(); $builder.set_ConnectionString($Connection)
    $backupDirectory = Join-Path (Get-CinemaCityRoot) '.local/backups'
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    $file = Join-Path $backupDirectory ('cinemacity-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.dump')
    $mapping = @{ 'Host' = 'PGHOST'; 'Port' = 'PGPORT'; 'Username' = 'PGUSER'; 'User ID' = 'PGUSER'; 'Password' = 'PGPASSWORD'; 'Database' = 'PGDATABASE' }
    $saved = @{}
    try {
        foreach ($key in $mapping.Keys) {
            if ($builder.ContainsKey($key)) { $name = $mapping[$key]; if (!$saved.ContainsKey($name)) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }; [Environment]::SetEnvironmentVariable($name, [string]$builder[$key]) }
        }
        & $PgDumpPath --format=custom --file=$file --no-password
        if ($LASTEXITCODE -ne 0) { throw 'Database backup failed. The transition has not started.' }
    } finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
    Write-Host "Original CinemaCity backup: $file"
    return $file
}
