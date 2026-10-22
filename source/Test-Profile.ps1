$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $env:TEMP ('UmaWalking-profile-tests-'+[Guid]::NewGuid().ToString('N')+'.exe')
try {
    & $compiler /nologo ("/out:"+$output) (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype\DesktopProfile.cs') (Join-Path $PSScriptRoot 'Tests\ProfileTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Profile tests failed to compile.'}
    & $output
    if($LASTEXITCODE -ne 0){throw 'Profile tests failed.'}
} finally {if(Test-Path -LiteralPath $output){Remove-Item -LiteralPath $output}}
