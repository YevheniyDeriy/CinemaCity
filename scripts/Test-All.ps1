param([string] $PostgresConnection = $env:CINEMACITY_TEST_POSTGRES)
. "$PSScriptRoot/LocalEnvironment.ps1"
$root = Get-CinemaCityRoot
if (!$PostgresConnection) {
    $values = Read-CinemaCityEnvironment
    if ($values.ContainsKey('CINEMACITY_SOURCE_CONNECTION')) { $PostgresConnection = $values['CINEMACITY_SOURCE_CONNECTION'] }
}
if (!$PostgresConnection) { throw 'Set CINEMACITY_TEST_POSTGRES or initialize the local environment first.' }
$saved = $env:CINEMACITY_TEST_POSTGRES
try {
    $env:CINEMACITY_TEST_POSTGRES = $PostgresConnection
    & dotnet test (Join-Path $root 'CinemaCity.slnx') --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    Push-Location (Join-Path $root 'frontend')
    try {
        & npm test; if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
        & npm run build; if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    } finally { Pop-Location }
} finally { $env:CINEMACITY_TEST_POSTGRES = $saved }
