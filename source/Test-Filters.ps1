param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'))
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close UmaDesktop first.'}
$settingsPath=Join-Path $BuildDirectory 'DesktopSettings.json';$backup=[IO.File]::ReadAllText($settingsPath)
$statePath=Join-Path $BuildDirectory 'DesktopLogs\state.json';$directory=Join-Path $BuildDirectory 'DesktopCommands'
$playerLog=Join-Path $BuildDirectory 'DesktopLogs\Player-v3-filters.log'
$start=[DateTime]::UtcNow
$process=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Normal -PassThru -ArgumentList @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-exit-seconds','200','-logFile',('"'+$playerLog+'"'))
$checks=[Collections.Generic.List[string]]::new();$states=[Collections.Generic.List[object]]::new()
function Send-Command($value){
 $id=[DateTime]::UtcNow.Ticks.ToString('D19')+'-'+[Guid]::NewGuid().ToString('N');$tmp=Join-Path $directory ($id+'.tmp')
 [IO.File]::WriteAllText($tmp,($value|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false));Move-Item -LiteralPath $tmp -Destination (Join-Path $directory ($id+'.json'))
}
function Await-State([scriptblock]$Predicate,[int]$Timeout=45){
 $timer=[Diagnostics.Stopwatch]::StartNew()
 while(-not $process.HasExited -and $timer.Elapsed.TotalSeconds -lt $Timeout){
  Start-Sleep -Milliseconds 250
  try{$s=Get-Content -Raw -LiteralPath $statePath|ConvertFrom-Json}catch{continue}
  $stamp=if($s.utc -is [DateTime]){$s.utc.ToUniversalTime()}else{[DateTimeOffset]::Parse([string]$s.utc).UtcDateTime}
  if($stamp -lt $start){continue};$states.Add($s)
  if($s.error){throw $s.error}
  if(& $Predicate $s){return $s}
 }
 throw 'Timed out waiting for filter / costume state.'
}
try{
 $s=Await-State {$args[0].count -ge 2 -and -not $args[0].loading}
 Send-Command @{action='filter';mode='Include';includeIds=@(1001,1028);excludeIds=@()}
 $s=Await-State {$args[0].mode -eq 'Include' -and $args[0].count -eq 6 -and $args[0].costumesUnique -and @($args[0].actors|Where-Object {$_.characterId -notin @(1001,1028)}).Count -eq 0} 60
 $checks.Add('Include allows only 1001/1028 and fills six distinct costume instances.')
 $actor=$s.actors[0];$oldCostume=$actor.costumeId;$characterId=$actor.characterId;$oldToken=$actor.token
 Send-Command @{action='costume';token=$actor.token}
 $s=Await-State {@($args[0].actors|Where-Object {$_.token -eq $oldToken}).Count -eq 0 -and @($args[0].actors|Where-Object {$_.characterId -eq $characterId -and $_.costumeId -ne $oldCostume}).Count -ge 1 -and -not $args[0].loading}
 $checks.Add('Changing costume retains the character and selects a different outfit.')
 Send-Command @{action='filter';mode='Include';includeIds=@();excludeIds=@()}
 $s=Await-State {$args[0].mode -eq 'Include' -and $args[0].count -eq 0 -and $args[0].targetCount -eq 0} 60
 Start-Sleep -Seconds 3
 $s=Get-Content -Raw -LiteralPath $statePath|ConvertFrom-Json
 if($s.count -ne 0){throw 'Empty Include incorrectly fell back to random.'}
 $checks.Add('Empty Include leaves the screen empty without a random fallback.')
 Send-Command @{action='filter';mode='Exclude';includeIds=@();excludeIds=@(1001,1002)}
 $s=Await-State {$args[0].mode -eq 'Exclude' -and $args[0].count -ge 3} 45
 if(@($s.actors|Where-Object {$_.characterId -in @(1001,1002)}).Count){throw 'Excluded character appeared.'}
 $checks.Add('Exclude prevents selected IDs from appearing.')
 Send-Command @{action='filter';mode='All';includeIds=@();excludeIds=@()}
 $s=Await-State {$args[0].mode -eq 'All' -and $args[0].targetCount -eq 6}
 $checks.Add('All mode restores the full available pool.')
 Send-Command @{action='exit'}
 $process.WaitForExit(15000)|Out-Null
 if(-not $process.HasExited -or $process.ExitCode -ne 0){throw 'Exit command failed.'}
 $checks.Add('Controller exit command closes the player.')
 $report=[ordered]@{Passed=$true;Checks=@($checks);ExitCode=$process.ExitCode;StatesCollected=$states.Count}
 $report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $BuildDirectory 'DesktopLogs\filter-report.json') -Encoding UTF8
 $states|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $BuildDirectory 'DesktopLogs\filter-observations.json') -Encoding UTF8
 $report|ConvertTo-Json -Depth 5
}finally{
 if(-not $process.HasExited){Stop-Process -Id $process.Id}
 [IO.File]::WriteAllText($settingsPath,$backup,[Text.UTF8Encoding]::new($false))
}
