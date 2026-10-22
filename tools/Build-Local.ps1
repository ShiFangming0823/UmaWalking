param(
    [Parameter(Mandatory=$true)][string]$UnityPath,
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\work\UmaViewer'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\build\UmaWalking')
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '..\source\Build-Prototype.ps1') -UnityPath $UnityPath -ProjectPath $ProjectPath -OutputDirectory $OutputDirectory
$publication = Split-Path -Parent $PSScriptRoot
$destination = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($file in @('DesktopSettings.json','Launch-UmaWalking.cmd','Launch-Low-Power.cmd')) {
    $target = Join-Path $destination $file
    if (-not (Test-Path -LiteralPath $target)) {
        $origin = if ($file -eq 'DesktopSettings.json') { Join-Path $PSScriptRoot 'Default-Settings.json' } else { Join-Path $publication $file }
        Copy-Item -LiteralPath $origin -Destination $target
    }
}
Write-Output ('Run: ' + (Join-Path $destination 'Launch-UmaWalking.cmd'))
