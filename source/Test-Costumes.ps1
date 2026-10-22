param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'))
$ErrorActionPreference='Stop'
if (Get-Process UmaDesktop -ErrorAction SilentlyContinue) { throw 'Close existing UmaDesktop first.' }
$logs=Join-Path $BuildDirectory 'DesktopLogs'
$playerLog=Join-Path $logs 'Player-costume-audit.log'
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Normal -PassThru -ArgumentList @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-costume-audit','-logFile',('"'+$playerLog+'"'))
$timer=[Diagnostics.Stopwatch]::StartNew()
try {
    while (-not $process.HasExited -and $timer.Elapsed.TotalSeconds -lt 350) { Start-Sleep -Milliseconds 1000 }
    if (-not $process.HasExited) { throw 'Costume audit timed out.' }
    $state=Get-Content -Raw -LiteralPath (Join-Path $logs 'state.json') | ConvertFrom-Json
    $stamp=if($state.utc -is [DateTime]){$state.utc.ToUniversalTime()}else{[DateTimeOffset]::Parse([string]$state.utc).UtcDateTime}
    $passed=$process.ExitCode -eq 0 -and $stamp -ge $started -and $state.costumeAuditComplete -and $state.auditFailures -eq 0 -and $state.auditCompleted -eq $state.auditPlanned -and $state.auditPlanned -gt 20 -and -not $state.error
    $report=[ordered]@{Passed=$passed;ExitCode=$process.ExitCode;ElapsedSeconds=$timer.Elapsed.TotalSeconds;Planned=$state.auditPlanned;Completed=$state.auditCompleted;Failed=$state.auditFailures;VerifiedFades=$state.fadeCount;Coverage='Every available common costume on 1001; every personal mini costume on 1001 and 1028; uniform and other shared costumes also on 1028.'}
    $report|ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'costume-audit-report.json') -Encoding UTF8
    $report|ConvertTo-Json
    if(-not $passed){throw 'Costume audit failed; inspect runtime state and player log.'}
} finally { if(-not $process.HasExited){Stop-Process -Id $process.Id} }
