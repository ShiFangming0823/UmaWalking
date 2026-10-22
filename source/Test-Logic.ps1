$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach($suite in @('ScheduleTests','FleetTests','DesktopCoverageTests','SpacingTests','ExpressionTests','GroundTests','GroundDragTests','FootprintTests','AvoidanceTests','PopulationLimitTests')) {
 $testExe=Join-Path $env:TEMP ('UmaDesktop-'+$suite+'-'+[Guid]::NewGuid().ToString('N')+'.exe')
 try {
  & $compiler /nologo ("/out:"+$testExe) (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopSchedule.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopRules.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopEnvironment.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopSpacing.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\MiniExpressionPolicy.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopGroundMath.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopGroundDrag.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopFootprintMath.cs') (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopAvoidance.cs') (Join-Path $PSScriptRoot ('Tests\'+$suite+'.cs'))
  if($LASTEXITCODE -ne 0){throw 'Logic test compilation failed.'}
  & $testExe
  if($LASTEXITCODE -ne 0){throw 'Logic tests failed.'}
 } finally {if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe}}
}
