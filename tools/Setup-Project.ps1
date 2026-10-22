param(
    [string]$UpstreamRepository = 'https://github.com/katboi01/UmaViewer.git',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\work\UmaViewer')
)
$ErrorActionPreference = 'Stop'
$revision = '07f82e9fa08f23c1cda3a9be3045a09372eae8bb'
$project = [IO.Path]::GetFullPath($ProjectPath)
if (Test-Path -LiteralPath $project) {
    if (-not (Test-Path -LiteralPath (Join-Path $project '.git'))) { throw 'Existing directory is not a Git checkout; no files changed.' }
    $head = & git -C $project rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $head.Trim() -ne $revision) { throw 'Existing checkout is not the pinned revision; no reset or deletion will be performed.' }
    throw 'Project already exists. Review local changes, then apply source/Apply-Prototype.ps1 explicitly if you intend to refresh the overlay.'
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($project)) | Out-Null
& git clone --no-hardlinks --no-checkout -- $UpstreamRepository $project
if ($LASTEXITCODE -ne 0) { throw 'Upstream clone failed.' }
& git -C $project checkout --detach $revision
if ($LASTEXITCODE -ne 0) { throw 'Pinned upstream revision is unavailable.' }
$head = & git -C $project rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head.Trim() -ne $revision) { throw 'Upstream revision verification failed.' }
& (Join-Path $PSScriptRoot '..\source\Apply-Prototype.ps1') -ProjectPath $project
Write-Output ('Prepared UmaWalking project: ' + $project)
