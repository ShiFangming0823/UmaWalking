param([Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root 'source\Assets\Scripts\DesktopPrototype\DesktopProduct.cs'
$text=[IO.File]::ReadAllText($source)
$match=[regex]::Match($text,'Version="(\d+\.\d+\.\d+)"')
if(-not $match.Success){throw 'Public version constant is missing.'}
$previous=$match.Groups[1].Value
$utf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($source,$text.Replace(('Version="'+$previous+'"'),('Version="'+$Version+'"')),$utf8)
foreach($file in @('README.md','docs\Release-Guide.md','docs\Updates-and-Settings.md')) {
    $path=Join-Path $root $file
    if(Test-Path -LiteralPath $path){[IO.File]::WriteAllText($path,[IO.File]::ReadAllText($path).Replace($previous,$Version),$utf8)}
}
Write-Output ('Public version: '+$previous+' -> '+$Version+'. Rebuild and rerun tests before packaging. Configuration schema and historical notes were not changed.')
