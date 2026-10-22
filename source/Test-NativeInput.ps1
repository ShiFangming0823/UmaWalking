param([string]$EvidenceDirectory=(Join-Path $PWD 'native-input-results'))
$ErrorActionPreference='Stop'
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$testExe=Join-Path $env:TEMP ('UmaDesktop-NativeInput-'+[Guid]::NewGuid().ToString('N')+'.exe')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
try {
 $source=Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype'
 & $compiler /nologo /platform:x64 /define:UNITY_STANDALONE_WIN /r:System.Windows.Forms.dll /r:System.Drawing.dll ("/win32manifest:"+(Join-Path $PSScriptRoot 'Tests\NativeInputTests.manifest')) ("/out:"+$testExe) (Join-Path $PSScriptRoot 'Tests\NativeInputTests.cs') (Join-Path $source 'DesktopMouseInput.cs') (Join-Path $source 'WindowsOverlay.cs') (Join-Path $source 'DesktopEnvironment.cs')
 if($LASTEXITCODE -ne 0){throw 'Native input test compilation failed.'}
 $lines=@(& $testExe);$exit=$LASTEXITCODE;$lines|ForEach-Object {Write-Output $_}
 $lines|Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'native-input-transcript.txt') -Encoding UTF8
 [ordered]@{Passed=($exit -eq 0);Checks=@($lines|Where-Object {$_ -like 'PASS *'}).Count;PhysicalMouseDeliveryVerified=$false;Scope='Actual native windows and hook installation; invokes the production low-level callback with test coordinates, without OS input injection.';SourceSha256=@{DesktopMouseInput=(Get-FileHash (Join-Path $source 'DesktopMouseInput.cs')).Hash;WindowsOverlay=(Get-FileHash (Join-Path $source 'WindowsOverlay.cs')).Hash;DesktopEnvironment=(Get-FileHash (Join-Path $source 'DesktopEnvironment.cs')).Hash}}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'native-input-report.json') -Encoding UTF8
 if($exit -ne 0){throw 'Native input checks failed.'}
} finally {if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe}}
