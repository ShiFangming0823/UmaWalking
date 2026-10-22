param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'))
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close UmaDesktop before expression audit.'}
$directory=Join-Path $BuildDirectory 'DesktopLogs\ExpressionAudit';$reportPath=Join-Path $directory 'expression-report.json'
if(Test-Path -LiteralPath $reportPath){Remove-Item -LiteralPath $reportPath}
$log=Join-Path $BuildDirectory 'DesktopLogs\Player-expression-audit.log'
$process=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Hidden -PassThru -ArgumentList @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-expression-audit','--desktop-test-ignore-occlusion','--desktop-exit-seconds','180','-logFile',('"'+$log+'"'))
try {
 if(-not $process.WaitForExit(180000)){throw 'Expression audit timed out.'}
 if($process.ExitCode -ne 0){throw 'Expression audit process failed.'}
 if(-not(Test-Path -LiteralPath $reportPath)){throw 'No expression report generated.'}
 $report=Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath|ConvertFrom-Json
 if(-not $report.Passed){$report.failures;throw 'Expression audit failed.'}
 [ordered]@{Passed=$true;Models=$report.models;StateTransitions=$report.stateTransitions;PresetPortraits=$report.presetPortraits;RecordedCases=@($report.cases).Count;Directory=$directory;RuntimeAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $BuildDirectory 'UmaDesktop_Data\Managed\umamusume.dll')).Hash}|ConvertTo-Json
} finally {if(-not $process.HasExited){Stop-Process -Id $process.Id}}
