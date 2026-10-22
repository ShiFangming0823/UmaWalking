param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'))
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close UmaDesktop before the visual audit.'}
$reportPath=Join-Path $BuildDirectory 'DesktopLogs\VisualAudit\report.json'
if(Test-Path -LiteralPath $reportPath){Remove-Item -LiteralPath $reportPath}
$log=Join-Path $BuildDirectory 'DesktopLogs\Player-visual-audit.log'
$p=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Normal -PassThru -ArgumentList @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-visual-audit','--desktop-exit-seconds','180','-logFile',('"'+$log+'"'))
try {
 if(-not $p.WaitForExit(210000)){throw 'Visual audit timed out.'}
 if($p.ExitCode -ne 0){throw 'Visual audit exited with an error.'}
 $report=Get-Content -Raw -LiteralPath $reportPath|ConvertFrom-Json
 if(-not $report.Passed -or $report.samples.Count -ne 96){throw 'Expected 96 successful pose samples.'}
 if([IO.File]::ReadAllText($log) -match 'Exception:|ERROR |Non-Legacy animations cannot'){throw 'Graphics error in visual audit.'}
 $report|Add-Member -NotePropertyName RuntimeAssemblySha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $BuildDirectory 'UmaDesktop_Data\Managed\umamusume.dll') -Algorithm SHA256).Hash -Force
 [IO.File]::WriteAllText($reportPath,($report|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
 Write-Output ('PASS: 96 sampled poses, 288 PNGs in '+(Split-Path $reportPath))
} finally {if(-not $p.HasExited){Stop-Process -Id $p.Id}}
