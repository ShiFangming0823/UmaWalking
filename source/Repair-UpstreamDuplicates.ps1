param([string]$ProjectPath = (Join-Path $PSScriptRoot '..\work\UmaViewer'))
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
# July 2026 added reorganized implementations without removing these older copies.
# Keep every file and GUID; only the obsolete copies are excluded from compilation.
$relativeFiles = @(
    'Assets\Scripts\umamusume\Gallop\CySpringDataContainer.cs',
    'Assets\Scripts\umamusume\Gallop\FaceDrivenKeyTarget.cs',
    'Assets\Scripts\umamusume\Gallop\FaceEmotionKeyTarget.cs',
    'Assets\Scripts\umamusume\Gallop\FaceOverrideData.cs',
    'Assets\Scripts\umamusume\Gallop\FacePartsSet.cs',
    'Assets\Scripts\umamusume\Gallop\FacialMorph.cs',
    'Assets\Scripts\umamusume\Gallop\TearController.cs',
    'Assets\Scripts\umamusume\Gallop\Cyalume\CyalumeControllerBase.cs',
    'Assets\Scripts\umamusume\Gallop\DereScript\StageScript\CacheCamera.cs',
    'Assets\Scripts\umamusume\Gallop\DereScript\StageScript\Character3DBase.cs',
    'Assets\Scripts\umamusume\Gallop\DereScript\StageScript\LiveTimelineCamera.cs',
    'Assets\Scripts\umamusume\Gallop\DereScript\StageScript\StageUtil.cs',
    'Assets\Scripts\umamusume\Gallop\StageScript\GameObjectUtility.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\ILiveTimelineCharactorLocator.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\ILiveTimelineMSQTarget.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\ILiveTimelineKeyDataList.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\LiveTimelineCharaLocator.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\LiveTimelineControl.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\LiveTimelineData.cs',
    'Assets\Scripts\umamusume\Gallop\Live\Cutt\LiveTimelineDefine.cs'
)
foreach ($relative in $relativeFiles) {
    $file = Join-Path $project $relative
    if (-not (Test-Path -LiteralPath $file)) { throw ('Expected legacy source is missing: ' + $file) }
}
foreach ($relative in $relativeFiles) {
    $file = Join-Path $project $relative
    $bytes = [IO.File]::ReadAllBytes($file)
    if ([Text.Encoding]::ASCII.GetString($bytes, 0, [Math]::Min(100, $bytes.Length)).Contains('UMA_LEGACY_DUPLICATE_SOURCES')) { continue }
    $backup = Join-Path $project ('.umawalking-backup\' + $relative)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
    if (-not (Test-Path -LiteralPath $backup)) { [IO.File]::WriteAllBytes($backup, $bytes) }
    $start = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { $start = 3 }
    $header = [Text.Encoding]::ASCII.GetBytes("#if UMA_LEGACY_DUPLICATE_SOURCES`r`n// Superseded implementation retained for reference; the July 2026 source is compiled.`r`n")
    $footer = [Text.Encoding]::ASCII.GetBytes("`r`n#endif`r`n")
    $stream = [IO.MemoryStream]::new()
    try {
        $stream.Write($header, 0, $header.Length)
        $stream.Write($bytes, $start, $bytes.Length - $start)
        $stream.Write($footer, 0, $footer.Length)
        [IO.File]::WriteAllBytes($file, $stream.ToArray())
    } finally { $stream.Dispose() }
}
Write-Output ('Excluded ' + $relativeFiles.Count + ' legacy duplicate source files; originals preserved in the project .umawalking-backup directory.')
$otherFiles = @('Assets\Scripts\UmaContainerCharacter.cs', 'Assets\Scripts\umamusume.asmdef', 'Assets\StandaloneFileBrowser\SFB.asmdef')
foreach ($relative in $otherFiles) {
    $file = Join-Path $project $relative
    $backup = Join-Path $project ('.umawalking-backup\' + $relative)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
    if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $file -Destination $backup }
}
$utf8 = [Text.UTF8Encoding]::new($true)
$characterPath = Join-Path $project 'Assets\Scripts\UmaContainerCharacter.cs'
$characterText = [IO.File]::ReadAllText($characterPath)
$characterText = $characterText.Replace("using CySpringParamDataElement = Gallop.CySpringParamDataElement;`r`n", '')
$characterText = $characterText.Replace("using CySpringParamDataChildElement = Gallop.CySpringParamDataChildElement;`r`n", '')
$characterText = $characterText.Replace('                CySpringParamDataElement element = container.springParam[i];', '                Gallop.CySpringParamDataElement element = container.springParam[i];')
$characterText = $characterText.Replace('                    CySpringParamDataChildElement child = element._childElements[j];', '                    Gallop.CySpringParamDataChildElement child = element._childElements[j];')
[IO.File]::WriteAllText($characterPath, $characterText, $utf8)
# Only the current file-browser assembly is referenced by viewer/runtime/editor
# consumers. The older sample assembly can still compile in isolation.
$runtimePath = Join-Path $project 'Assets\Scripts\umamusume.asmdef'
$runtime = [IO.File]::ReadAllText($runtimePath) | ConvertFrom-Json
if ('GUID:450c8a1fa024349709aa2cdb63221944' -in $runtime.references) {
    $runtime.references = @($runtime.references | Where-Object { $_ -ne 'GUID:450c8a1fa024349709aa2cdb63221944' })
    [IO.File]::WriteAllText($runtimePath, ($runtime | ConvertTo-Json -Depth 10), $utf8)
}
$oldBrowserPath = Join-Path $project 'Assets\StandaloneFileBrowser\SFB.asmdef'
$oldBrowser = [IO.File]::ReadAllText($oldBrowserPath) | ConvertFrom-Json
if ($oldBrowser.autoReferenced -ne $false) {
    $oldBrowser | Add-Member -MemberType NoteProperty -Name autoReferenced -Value $false -Force
    [IO.File]::WriteAllText($oldBrowserPath, ($oldBrowser | ConvertTo-Json -Depth 10), $utf8)
}
Write-Output 'Selected current file-browser assembly and qualified current Gallop spring parameter types.'
