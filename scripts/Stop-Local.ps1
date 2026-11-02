. "$PSScriptRoot/LocalEnvironment.ps1"
$manifest = Join-Path (Get-CinemaCityRoot) '.local/processes.json'
if (!(Test-Path -LiteralPath $manifest)) { Write-Host 'No managed local stack.'; return }
$items = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
$allEntriesHandled = $true
foreach ($item in $items) {
    $expectedTicks = $null
    $ticksProperty = $item.PSObject.Properties['StartedAtUtcTicks']
    if ($ticksProperty -and $null -ne $ticksProperty.Value) {
        try { $expectedTicks = [long]::Parse([string]$ticksProperty.Value, [Globalization.CultureInfo]::InvariantCulture) } catch { }
    }
    if ($null -eq $expectedTicks) {
        $startedAtProperty = $item.PSObject.Properties['StartedAt']
        $startedAtValue = if ($startedAtProperty) { $startedAtProperty.Value } else { $null }
        while ($startedAtValue -is [array] -and $startedAtValue.Count -eq 1) { $startedAtValue = $startedAtValue[0] }
        if ($startedAtValue -is [string]) {
            try { $expectedTicks = [DateTimeOffset]::Parse($startedAtValue, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).UtcDateTime.Ticks } catch { }
        }
    }
    $process = Get-Process -Id $item.Id -ErrorAction SilentlyContinue
    if (!$process) { continue }
    if ($null -eq $expectedTicks) {
        Write-Warning "Could not safely read the recorded start time for PID $($item.Id) ($($item.Name)); leaving it running."
        $allEntriesHandled = $false
        continue
    }
    if ([Math]::Abs($process.StartTime.ToUniversalTime().Ticks - $expectedTicks) -lt [TimeSpan]::TicksPerSecond) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
        Write-Host "Stopped $($item.Name)."
    } else {
        Write-Warning "PID $($item.Id) no longer matches the recorded $($item.Name) process; leaving it running."
        $allEntriesHandled = $false
    }
}
if ($allEntriesHandled) { Remove-Item -LiteralPath $manifest }
else { Write-Warning "The process manifest was kept at $manifest because at least one entry needs manual review." }
