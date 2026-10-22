param(
    [Parameter(Mandatory=$true)][string]$UnityPath,
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\work\UmaViewer'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\build\UmaWalking')
)
$ErrorActionPreference = 'Stop'
$editor = (Resolve-Path -LiteralPath $UnityPath).Path
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if ([IO.Path]::GetFileName($editor) -ne 'Unity.exe') { throw 'UnityPath must point to Editor\Unity.exe, not Unity Hub.exe.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($editor).ProductVersion
if (-not $version.StartsWith('2022.3.62f3')) { throw ('Expected Unity Editor 2022.3.62f3; found ' + $version) }
if ($project.Contains('"') -or $OutputDirectory.Contains('"')) { throw 'Unexpected quote in path.' }
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$buildOutput = [IO.Path]::GetFullPath($OutputDirectory)
$env:UMA_DESKTOP_BUILD = Join-Path $buildOutput 'UmaDesktop.exe'
$logFile = Join-Path $buildOutput 'build.log'
$arguments = @('-batchmode','-quit','-projectPath',('"' + $project + '"'),'-buildTarget','Win64','-executeMethod','UmaDesktopBuild.Build','-logFile',('"' + $logFile + '"'))
try {
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    # Wait for the editor itself, not long-lived licensing/package descendants.
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw ('Unity build failed. See ' + $logFile) }
    if (-not (Test-Path -LiteralPath $env:UMA_DESKTOP_BUILD)) { throw ('No executable was generated. See ' + $logFile) }
    & (Join-Path $PSScriptRoot 'Build-Controller.ps1') -OutputDirectory $buildOutput
    Write-Output ('Built: ' + $env:UMA_DESKTOP_BUILD)
} finally { Remove-Item Env:\UMA_DESKTOP_BUILD -ErrorAction SilentlyContinue }
