param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\build\UmaWalking'),[string]$EvidenceDirectory=(Join-Path $PWD 'transient-visibility-results'))
$ErrorActionPreference='Stop'
if(Get-Process UmaDesktop -ErrorAction SilentlyContinue){throw 'Close UmaDesktop before running visibility integration tests.'}
$fixtures=Join-Path $env:TEMP ('UmaDesktopTransientFixtures-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtures)|Out-Null
try {
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 # These are our own synthetic fixtures, never the installed Explorer/PowerToys executables.
 $fancy=Join-Path $fixtures 'PowerToys.FancyZones.exe'
 & $compiler /nologo /platform:x64 ("/out:"+$fancy) (Join-Path $PSScriptRoot 'Tests\TransientOverlayFixture.cs')
 if($LASTEXITCODE -ne 0){throw 'Could not compile native overlay fixture.'}
 Copy-Item -LiteralPath $fancy -Destination (Join-Path $fixtures 'explorer.exe')
 & python (Join-Path $PSScriptRoot 'Tests\verify_transient_visibility.py') --build-directory $BuildDirectory --fixtures-directory $fixtures --evidence-directory $EvidenceDirectory
 if($LASTEXITCODE -ne 0){throw 'Transient visibility integration checks failed.'}
} finally {
 foreach($name in @('PowerToys.FancyZones.exe','explorer.exe')) {
  $file=Join-Path $fixtures $name
  if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
 }
 if((Get-ChildItem -LiteralPath $fixtures -Force).Count -eq 0){Remove-Item -LiteralPath $fixtures}
}
