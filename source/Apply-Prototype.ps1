param([string]$ProjectPath = (Join-Path $PSScriptRoot '..\work\UmaViewer'))
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets\Scripts\UmaViewerBuilder.cs'))) {
    throw 'This is not an UmaViewer source project.'
}
$utf8 = [System.Text.UTF8Encoding]::new($true)
$configFile = Join-Path $project 'Assets\Scripts\Config.cs'
$managerFile = Join-Path $project 'Assets\Scripts\UmaAssetManager.cs'
$configText = [IO.File]::ReadAllText($configFile)
$managerText = [IO.File]::ReadAllText($managerFile)
$configAnchor = '    private static string GetConfigPath()'
$managerAnchor = '    public static void ReleaseAllForEditorCompilation()'
if (-not $configText.Contains($configAnchor) -or -not $managerText.Contains($managerAnchor)) {
    throw 'Upstream source layout changed. Refusing to patch unmatched code.'
}
$backupDirectory = Join-Path $project '.umawalking-backup'
[IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
foreach ($file in @($configFile, $managerFile)) {
    $backup = Join-Path $backupDirectory ([IO.Path]::GetFileName($file))
    if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $file -Destination $backup }
}
if (-not $configText.Contains('UMA_DESKTOP_CONFIG')) {
    $regex = [regex]'(    private static string GetConfigPath\(\)\s*\{)'
    $insert = @'

        // Desktop prototype uses a separate process-local configuration profile.
        var desktopConfigPath = Environment.GetEnvironmentVariable("UMA_DESKTOP_CONFIG");
        if (!string.IsNullOrWhiteSpace(desktopConfigPath)) return desktopConfigPath;
'@
    $configText = $regex.Replace($configText, {param($m) $m.Groups[1].Value + $insert}, 1)
    [IO.File]::WriteAllText($configFile, $configText, $utf8)
}
if (-not $managerText.Contains('ReleaseDesktopTransientBundles')) {
    $insert = @'
    // The desktop owns a single character. Keep shaders pinned, release every
    // transient handle after destroying that character and its PlayableGraph.
    public static void ReleaseDesktopTransientBundles()
    {
        if (instance == null) return;
        instance.ReleaseAllInternal(false, false, true);
    }

'@
    $managerText = $managerText.Replace($managerAnchor, $insert + $managerAnchor)
    [IO.File]::WriteAllText($managerFile, $managerText, $utf8)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype') -Destination (Join-Path $project 'Assets\Scripts') -Recurse -Force
[IO.Directory]::CreateDirectory((Join-Path $project 'Assets\Editor')) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Editor\UmaDesktopBuild.cs') -Destination (Join-Path $project 'Assets\Editor\UmaDesktopBuild.cs') -Force
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Assets\Editor\UmaDesktopBuild.cs.meta')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Editor\UmaDesktopBuild.cs.meta') -Destination (Join-Path $project 'Assets\Editor\UmaDesktopBuild.cs.meta') -Force
}
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype.meta')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Scripts\DesktopPrototype.meta') -Destination (Join-Path $project 'Assets\Scripts\DesktopPrototype.meta') -Force
}
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Assets\Resources\UmaDesktop')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Resources\UmaDesktop') -Destination (Join-Path $project 'Assets\Resources') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\Resources\UmaDesktop.meta') -Destination (Join-Path $project 'Assets\Resources\UmaDesktop.meta') -Force
}
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Assets\DesktopSplash')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\DesktopSplash') -Destination (Join-Path $project 'Assets') -Recurse -Force
}
# Retain the original files' UTF-8 BOM and CRLF style.
foreach ($file in @($configFile, $managerFile)) {
    $text = [IO.File]::ReadAllText($file).Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($file, $text, $utf8)
}
Write-Output ('Prototype source installed into ' + $project)
& (Join-Path $PSScriptRoot 'Repair-UpstreamDuplicates.ps1') -ProjectPath $project
