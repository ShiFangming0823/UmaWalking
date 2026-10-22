param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'),[int]$Seconds=120)
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close existing UmaDesktop before automated fleet test.'}
$log=Join-Path $BuildDirectory 'DesktopLogs\Player-v3-fleet.log'
$statePath=Join-Path $BuildDirectory 'DesktopLogs\state.json'
$commands=Join-Path $BuildDirectory 'DesktopCommands'
$start=[DateTime]::UtcNow
$process=Start-Process -FilePath (Join-Path $BuildDirectory 'UmaDesktop.exe') -WorkingDirectory $BuildDirectory -WindowStyle Hidden -PassThru -ArgumentList @('-force-d3d11','-force-d3d11-bitblt-model','--desktop-test-mode','--desktop-test-ignore-occlusion','--desktop-exit-seconds',$Seconds,'-logFile',('"'+$log+'"'))
$observations=[Collections.Generic.List[object]]::new()
$timer=[Diagnostics.Stopwatch]::StartNew();$clickSent=$false;$nextEncounter=14;$captureSent=$false
function Send-LocalCommand($value){
 $name=[DateTime]::UtcNow.Ticks.ToString('D19')+'-'+[Guid]::NewGuid().ToString('N')
 $temp=Join-Path $commands ($name+'.tmp');[IO.File]::WriteAllText($temp,($value|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false));Move-Item -LiteralPath $temp -Destination (Join-Path $commands ($name+'.json'))
}
try{
 while(-not $process.HasExited -and $timer.Elapsed.TotalSeconds -lt ($Seconds+30)){
  Start-Sleep -Milliseconds 500
  if(-not(Test-Path -LiteralPath $statePath)){continue}
  try{$s=Get-Content -Raw -LiteralPath $statePath|ConvertFrom-Json}catch{continue}
  $stamp=if($s.utc -is [DateTime]){$s.utc.ToUniversalTime()}else{[DateTimeOffset]::Parse([string]$s.utc).UtcDateTime}
  if($stamp -lt $start){continue}
  $observations.Add($s)
  if($s.count -ge 2 -and -not $clickSent){Send-LocalCommand @{action='testNativeClick';token=$s.actors[0].token};$clickSent=$true}
  if($s.count -eq 6 -and -not $s.loading -and $s.populationState -eq 'FullHold' -and $s.simulationSeconds -ge $nextEncounter -and @($s.actors|Where-Object {$_.phase -eq 'Active' -and $_.state -eq 'Walk'}).Count -ge 2){Send-LocalCommand @{action='testEncounter'};$nextEncounter=$s.simulationSeconds+15}
  if($s.count -eq 6 -and $s.simulationSeconds -ge 14 -and -not $captureSent){Send-LocalCommand @{action='capture'};$captureSent=$true}
 }
 if(-not $process.HasExited){throw 'Fleet test timed out.'}
 $fail=[Collections.Generic.List[string]]::new()
 if($process.ExitCode -ne 0){$fail.Add('Nonzero process exit.')}
 if($observations.Count -eq 0){throw 'No fresh fleet state received.'}
 $last=$observations[$observations.Count-1]
 if($observations|Where-Object {$_.error}){$fail.Add('Runtime error.')}
 if($observations|Where-Object {$_.count -gt 6 -or -not $_.namesUnique}){$fail.Add('Cap / unique-name invariant failed.')}
 if(-not($observations|Where-Object count -eq 6)){$fail.Add('Never reached six actors.')}
 if($last.departures -lt 1){$fail.Add('No complete edge departure.')}
 if($last.clickSpecials -lt 1){$fail.Add('Click command did not start a special.')}
 if($last.trimmedSpecials -lt 1){$fail.Add('Special early cutoff missing.')}
 $actorRows=@($observations|ForEach-Object actors)
 if(-not($actorRows|Where-Object phase -eq 'Entering') -or -not($actorRows|Where-Object phase -eq 'Exiting')){$fail.Add('Missing edge entrance / departure phase.')}
 if(-not($actorRows|Where-Object {$_.presentedFrames -ge 60})){$fail.Add('Continuous native frame presentation missing.')}
 if($actorRows|Where-Object {-not $_.desktopAttached -or $_.desktopParent -eq 0}){$fail.Add('Pet escaped the desktop parent layer.')}
 if(-not($actorRows|Where-Object {$_.automaticSpecials -gt 0 -or $_.specialDeferrals -gt 0})){$fail.Add('Automatic special clearance scheduling was never exercised.')}
 foreach($a in $actorRows){
  $action=if($a.state -eq 'Special' -or $a.state -eq 'Interaction'){'Special'}else{$a.state}
  $pool=if($action -eq 'Walk'){@('WaraiA','WaraiB','WaraiC','WaraiD','WaraiE','SleepA','SleepB')}elseif($action -eq 'Run'){@('KiriA','KiriB','KiriC','GuruguruA','FightA')}elseif($a.characterId -in @(1018,1040,1105)){@('Default')}else{@('WaraiA','WaraiB','WaraiC','WaraiD','WaraiE')}
  if($a.expressionAction -ne $action -or $a.expression -notin $pool -or -not $a.expressionMatchesPreset -or (($a.expression -eq 'Default') -eq [bool]$a.expressionOverride)){$fail.Add('Expression action/pool/effective-render-UV mismatch.');break}
 }
 $text=[IO.File]::ReadAllText($log)
 if($text -match 'ERROR |Exception:|Non-Legacy animations cannot'){$fail.Add('Runtime/graphics errors found in log.')}
 $events=[IO.File]::ReadAllLines((Join-Path $BuildDirectory 'DesktopLogs\events.log'))
 $catalogIndex=0;for($i=0;$i -lt $events.Length;$i++){if($events[$i] -match 'Catalog V3:'){$catalogIndex=$i}}
 $events=@($events[$catalogIndex..($events.Length-1)])
 $firstExit=$events|Where-Object {$_ -match 'Exit requested token='}|Select-Object -First 1
 if($firstExit -and $firstExit -notmatch 'ordinal=1 '){$fail.Add('First automatic exit was not FIFO ordinal 1.')}
 $removedIndex=-1;for($i=0;$i -lt $events.Length;$i++){if($events[$i] -match 'Removed token='){$removedIndex=$i;break}}
 $replacementGap=$null
 if($removedIndex -ge 0){
  $nextSpawn=@($events|Select-Object -Skip ($removedIndex+1)|Where-Object {$_ -match 'Spawn token='}|Select-Object -First 1)
  if($nextSpawn.Count){$replacementGap=([DateTimeOffset]::Parse($nextSpawn[0].Split(' ')[0])-[DateTimeOffset]::Parse($events[$removedIndex].Split(' ')[0])).TotalSeconds;if($replacementGap -lt 1.4){$fail.Add('Replacement appeared before required empty gap.')}}
 }
 $report=[ordered]@{Passed=$fail.Count -eq 0;ExitCode=$process.ExitCode;Seconds=$timer.Elapsed.TotalSeconds;Samples=$observations.Count;MaximumCount=($observations|Measure-Object count -Maximum).Maximum;CharactersLoaded=$last.charactersLoaded;Departures=$last.departures;ClickSpecials=$last.clickSpecials;TrimmedSpecials=$last.trimmedSpecials;ReplacementGapSeconds=$replacementGap;FirstExit=$firstExit;Interactions=$last.interactions;EncounterRolls=$last.encounterRolls;Failures=@($fail)}
 $report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $BuildDirectory 'DesktopLogs\fleet-report.json') -Encoding UTF8
 $observations|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $BuildDirectory 'DesktopLogs\fleet-observations.json') -Encoding UTF8
 $report|ConvertTo-Json -Depth 6
 if($fail.Count){throw 'Fleet test failed.'}
}finally{if(-not $process.HasExited){Stop-Process -Id $process.Id}}
