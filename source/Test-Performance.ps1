param([int]$FrameRate=30,[double]$RenderScale=1,[int]$Seconds=65,[int]$MaxActors=6,[int]$Msaa=4,[string]$OutputFile,[string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'))
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close existing UmaDesktop first.'}
$log=Join-Path $BuildDirectory ('DesktopLogs\Player-performance-'+$FrameRate+'.log')
$argsList=@('-force-d3d11','-force-d3d11-bitblt-model','--desktop-fps',$FrameRate,'--desktop-render-scale',$RenderScale.ToString([Globalization.CultureInfo]::InvariantCulture),'--desktop-max-actors',$MaxActors,'--desktop-msaa',$Msaa,'--desktop-exit-seconds',($Seconds+15),'-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Normal -PassThru -ArgumentList $argsList
try {
    Start-Sleep -Seconds 8
    & (Join-Path $PSScriptRoot 'Measure-Desktop.ps1') -TargetProcessId $process.Id -Seconds $Seconds -OutputFile $OutputFile
    $report=Get-Content -Raw -LiteralPath $OutputFile | ConvertFrom-Json
    $report | Add-Member -NotePropertyName Profile -NotePropertyValue @{FrameRate=$FrameRate;RenderScale=$RenderScale;MaxActors=$MaxActors;Msaa=$Msaa;DefaultCharacterPool=$true;DefaultTiming=$true} -Force
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),($report|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $process.WaitForExit(25000)|Out-Null
    if(-not $process.HasExited){throw 'Performance player did not exit.'}
    if($process.ExitCode -ne 0){throw 'Performance player exited with error.'}
    $state=Get-Content -Raw -LiteralPath (Join-Path $BuildDirectory 'DesktopLogs\state.json')|ConvertFrom-Json
    if($state.error){throw $state.error}
    $runtimeLog=[IO.File]::ReadAllText($log)
    if($runtimeLog -match 'Non-Legacy animations cannot|Exception:|ERROR |No body,'){throw 'Runtime errors/warnings detected in performance player log.'}
    $report=Get-Content -Raw -LiteralPath $OutputFile|ConvertFrom-Json
    $assembly=Join-Path $BuildDirectory 'UmaDesktop_Data\Managed\umamusume.dll'
    $report|Add-Member -NotePropertyName RuntimeFinalState -NotePropertyValue $state -Force
    if(Test-Path -LiteralPath $assembly){$report|Add-Member -NotePropertyName RuntimeAssemblySha256 -NotePropertyValue (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -Force}
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),($report|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
} finally {if(-not $process.HasExited){Stop-Process -Id $process.Id}}
