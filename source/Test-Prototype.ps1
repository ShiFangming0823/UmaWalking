param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\build\UmaWalking'),
    [int]$Seconds = 120,
    [int]$FrameRate = 30,
    [double]$RenderScale = 1,
    [switch]$CostumeSweep
)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$executable = Join-Path $build 'UmaDesktop.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the prototype first.' }
if (Get-Process UmaDesktop -ErrorAction SilentlyContinue) { throw 'Close the existing UmaDesktop instance before the smoke test.' }
$logs = Join-Path $build 'DesktopLogs'
[IO.Directory]::CreateDirectory($logs) | Out-Null
$playerLog = Join-Path $logs 'Player-smoke.log'
$testStarted = [DateTime]::UtcNow
$arguments = @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-smoke-seconds',$Seconds,'-logFile',('"' + $playerLog + '"'))
$arguments += @('--desktop-fps',$FrameRate,'--desktop-render-scale',$RenderScale.ToString([Globalization.CultureInfo]::InvariantCulture))
if ($CostumeSweep) { $arguments += '--desktop-costume-sweep' }
# This is the user-requested visible prototype, not a hidden background helper.
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $build -WindowStyle Normal -PassThru
$observations = [System.Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    while (-not $process.HasExited -and $timer.Elapsed.TotalSeconds -lt ($Seconds + 45)) {
        Start-Sleep -Milliseconds 1000
        $statePath = Join-Path $logs 'state.json'
        if (Test-Path -LiteralPath $statePath) {
            try {
                $state = [IO.File]::ReadAllText($statePath) | ConvertFrom-Json
                $stamp = if ($state.utc -is [DateTime]) { $state.utc.ToUniversalTime() } else { [DateTimeOffset]::Parse([string]$state.utc).UtcDateTime }
                if ($stamp -ge $testStarted) { $observations.Add($state) }
            } catch { }
        }
    }
    if (-not $process.HasExited) { throw 'The prototype did not exit within its smoke-test timeout.' }
    if ($observations.Count -eq 0) { throw ('No runtime status was received. See ' + $playerLog) }
    $last = $observations[$observations.Count - 1]
    $failures = [System.Collections.Generic.List[string]]::new()
    if ($process.ExitCode -ne 0) { $failures.Add('Unexpected player exit code: ' + $process.ExitCode) }
    if ($last.error) { $failures.Add([string]$last.error) }
    if ($last.characterCount -lt 3) { $failures.Add('Fewer than three character loads completed.') }
    if ($last.specialCount -lt 3) { $failures.Add('Fewer than three exclusive actions started.') }
    $states = @($observations | ForEach-Object state | Select-Object -Unique)
    foreach ($required in @('Walk','Run','Special')) {
        if ($required -notin $states) { $failures.Add('Missing observed state: ' + $required) }
    }
    if ($last.trimmedSpecialCount -lt 2) { $failures.Add('Special cutoff did not execute twice.') }
    if ($last.fadeCount -lt 5) { $failures.Add('Insufficient verified native alpha fades.') }
    if (-not ($observations | Where-Object { $_.characterId -eq 1028 -and $_.characterHeightPixels -eq 570 })) { $failures.Add('Hishi Akebono height/costume not verified.') }
    if (-not ($observations | Where-Object { $_.characterId -ne 1028 -and $_.characterHeightPixels -eq 380 })) { $failures.Add('Normal character height not verified.') }
    $costumeCategories = @($observations | Where-Object { -not $_.loading } | ForEach-Object costumeCategory | Select-Object -Unique)
    if ($CostumeSweep -and $costumeCategories.Count -lt 3) { $failures.Add('Missing costume category coverage.') }
    $runtimeText = [IO.File]::ReadAllText($playerLog)
    if ($runtimeText -match 'Non-Legacy animations cannot|Exception:|ERROR |No body,') { $failures.Add('Runtime animation or asset errors found in player log.') }
    if (-not ($observations | Where-Object { $_.facialEvaluations -gt 0 })) { $failures.Add('No facial/ear track evaluated through the PlayableGraph.') }
    $positions = $observations | Measure-Object screenX -Minimum -Maximum
    if (($positions.Maximum - $positions.Minimum) -lt 20) { $failures.Add('Insufficient screen movement.') }
    $observations | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $logs 'smoke-observations.json') -Encoding UTF8
    $report = [ordered]@{
        Passed = ($failures.Count -eq 0); StartedUtc = $testStarted.ToString('o');
        ElapsedSeconds = $timer.Elapsed.TotalSeconds; ExitCode = $process.ExitCode;
        Observations = $observations.Count; States = $states;
        CostumeSweep = [bool]$CostumeSweep; FrameRate = $FrameRate; RenderScale = $RenderScale; CostumeCategories = $costumeCategories;
        TrimmedSpecials = $last.trimmedSpecialCount; VerifiedFades = $last.fadeCount;
        CharactersLoaded = $last.characterCount; SpecialsStarted = $last.specialCount;
        MovementPixels = ($positions.Maximum - $positions.Minimum); Failures = @($failures)
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logs 'smoke-report.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 5
    if ($failures.Count -gt 0) { throw 'Runtime smoke test failed; inspect smoke-report.json and Player-smoke.log.' }
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}
