param([Parameter(Mandatory=$true)][int]$TargetProcessId,[int]$Seconds=60,[Parameter(Mandatory=$true)][string]$OutputFile)
$ErrorActionPreference='Stop'
$cpu=Get-CimInstance Win32_Processor
$logical=($cpu | Measure-Object NumberOfLogicalProcessors -Sum).Sum
$hardware=[ordered]@{Cpu=@($cpu | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);Gpu=@(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion);LogicalProcessors=$logical}
$rows=[Collections.Generic.List[object]]::new()
$timer=[Diagnostics.Stopwatch]::StartNew()
$proc=Get-Process -Id $TargetProcessId
$lastCpu=$proc.TotalProcessorTime.TotalSeconds
$lastTime=$timer.Elapsed.TotalSeconds
while($timer.Elapsed.TotalSeconds -lt $Seconds){
    Start-Sleep -Milliseconds 1000
    try{$proc=Get-Process -Id $TargetProcessId -ErrorAction Stop}catch{break}
    $proc.Refresh();$resident=$proc.WorkingSet64;$private=$proc.PrivateMemorySize64
    if($resident -le 0 -or $private -le 0){break}
    $now=$timer.Elapsed.TotalSeconds;$cpuTime=$proc.TotalProcessorTime.TotalSeconds
    $corePercent=100*($cpuTime-$lastCpu)/[Math]::Max(0.001,$now-$lastTime)
    $gpu=@();$gpuMemory=@()
    try{$gpu=@(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -Filter ("Name LIKE 'pid_"+$TargetProcessId+"_%'") | Where-Object Name -Like '*engtype_3D*')}catch{}
    try{$gpuMemory=@(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -Filter ("Name LIKE 'pid_"+$TargetProcessId+"_%'"))}catch{}
    $gpuPeak=$null;if($gpu.Count){$gpuPeak=($gpu | Measure-Object UtilizationPercentage -Maximum).Maximum}
    $dedicated=$null;$shared=$null
    if($gpuMemory.Count){$dedicated=($gpuMemory | Measure-Object DedicatedUsage -Sum).Sum;$shared=($gpuMemory | Measure-Object SharedUsage -Sum).Sum}
    $rows.Add([ordered]@{elapsed=$now;cpuTotalPercent=$corePercent/$logical;cpuOneCorePercent=$corePercent;workingSetMiB=$resident/1MB;privateBytesMiB=$private/1MB;gpu3dPercent=$gpuPeak;gpuDedicatedMiB=if($null-ne$dedicated){$dedicated/1MB}else{$null};gpuSharedMiB=if($null-ne$shared){$shared/1MB}else{$null}})
    $lastTime=$now;$lastCpu=$cpuTime
}
$summary=[ordered]@{}
foreach($field in @('cpuTotalPercent','cpuOneCorePercent','workingSetMiB','privateBytesMiB','gpu3dPercent','gpuDedicatedMiB','gpuSharedMiB')){
    $values=@($rows | ForEach-Object {$_[$field]} | Where-Object {$null-ne$_})
    if($values.Count){$m=$values|Measure-Object -Average -Maximum -Minimum;$summary[$field]=@{average=$m.Average;maximum=$m.Maximum;minimum=$m.Minimum}}
}
$report=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');processId=$TargetProcessId;seconds=$timer.Elapsed.TotalSeconds;samples=$rows.Count;hardware=$hardware;summary=$summary;observations=@($rows.ToArray());notes='GPU counters are driver estimates; process GPU memory counters may be inaccurate on some Windows versions.'}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),($report|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
$summary|ConvertTo-Json -Depth 5
