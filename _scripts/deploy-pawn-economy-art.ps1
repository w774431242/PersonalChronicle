$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = [IO.Path]::GetFullPath((Join-Path $root '../../Mods/殖民地综合管理系统'))
$base = Join-Path $root 'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08'
$srcAbout = [xml](Get-Content -Raw -LiteralPath (Join-Path $root 'About/About.xml'))
$dstAbout = [xml](Get-Content -Raw -LiteralPath (Join-Path $target 'About/About.xml'))
if ($srcAbout.ModMetaData.packageId -ne $dstAbout.ModMetaData.packageId -or $srcAbout.ModMetaData.packageId -ne 'colony.integratedmanagement') { throw 'Package mismatch' }
& (Join-Path $PSScriptRoot 'verify-pawn-economy-art.ps1')
$previous = Get-Content -Raw -LiteralPath (Join-Path $root 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.4.2_部署验证_2026-10-08.json') | ConvertFrom-Json
$defPath = 'Defs/PawnEconomy_Institutions.xml'
foreach ($file in $previous.files | Where-Object path -ne $defPath) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $file.path)).Hash -ne $file.sha256 -or (Get-FileHash -LiteralPath (Join-Path $target $file.path)).Hash -ne $file.sha256) { throw "Unrelated runtime drift: $($file.path)" }
}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $base '图像检查.json') | ConvertFrom-Json
$paths = @($defPath) + @($manifest.files | Select-Object -ExpandProperty path)
$expected = @(@($previous.targetInventory.path) + @($manifest.files.path) | Sort-Object -Unique)
$unexpectedBefore = @(Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object { [IO.Path]::GetRelativePath($target,$_.FullName).Replace('\','/') -notin $expected })
if ($unexpectedBefore.Count) { throw 'Unexpected target files before deployment; no files copied' }
$oldDefHash = ($previous.files | Where-Object path -eq $defPath).sha256
$targetDefHash = (Get-FileHash -LiteralPath (Join-Path $target $defPath)).Hash
$sourceDefHash = (Get-FileHash -LiteralPath (Join-Path $root $defPath)).Hash
if ($targetDefHash -ne $oldDefHash -and $targetDefHash -ne $sourceDefHash) { throw 'Target economic Def has independent changes; no files copied' }
$backup = Join-Path $root ('_backups/economy-art-deploy_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
$idPath = Join-Path $target 'About/PublishedFileId.txt'
$idHash = if (Test-Path -LiteralPath $idPath) { (Get-FileHash -LiteralPath $idPath).Hash } else { $null }
$running = @(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue | Select-Object Id,ProcessName)
$copied = @()
foreach ($relative in $paths) {
    $src = Join-Path $root $relative
    $dst = Join-Path $target $relative
    $existed = Test-Path -LiteralPath $dst
    if ($existed) {
        $bak = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $bak) -Force | Out-Null
        Copy-Item -LiteralPath $dst -Destination $bak
        if ((Get-FileHash -LiteralPath $bak).Hash -ne (Get-FileHash -LiteralPath $dst).Hash) { throw 'Backup mismatch' }
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
    $hash = (Get-FileHash -LiteralPath $src).Hash
    if ((Get-FileHash -LiteralPath $dst).Hash -ne $hash) { throw "Deployment mismatch: $relative" }
    $copied += [pscustomobject]@{ path=$relative; sha256=$hash; bytes=(Get-Item -LiteralPath $dst).Length; targetExisted=$existed }
}
$null = [xml](Get-Content -Raw -LiteralPath (Join-Path $target $defPath))
$afterId = if (Test-Path -LiteralPath $idPath) { (Get-FileHash -LiteralPath $idPath).Hash } else { $null }
if ($idHash -ne $afterId) { throw 'Workshop identity changed' }
$targetFiles = @(Get-ChildItem -LiteralPath $target -Recurse -File)
$unexpected = @($targetFiles | Where-Object { [IO.Path]::GetRelativePath($target,$_.FullName).Replace('\','/') -notin $expected })
if ($unexpected.Count) { throw 'Unexpected target files; review before declaring deployment pass' }
if ($targetFiles.Count -ne $expected.Count) { throw 'Target inventory missing expected files' }
$dllHash = (Get-FileHash -LiteralPath (Join-Path $target 'Assemblies/PersonalChronicle.dll')).Hash
if ($dllHash -ne $previous.deployedBuildSha256) { throw 'Logic DLL changed during art-only deployment' }
$report = [ordered]@{
    changeId='CHG-20261008-ECON-ART'; deployed=$true; gameVerified=$false; checkedUtc=[DateTime]::UtcNow.ToString('o')
    target=$target; backup=$backup; files=$copied; fileCount=$copied.Count; originalRuntimeResourcesVerified=210
    targetFileCount=$targetFiles.Count; totalBytes=($targetFiles | Measure-Object Length -Sum).Sum
    logicDllSha256=$dllHash; unchangedLogicDll=$true; workshopIdentityPreserved=$true; runningGameProcesses=$running
    restartRequired=$true; buildRerun=$false; logicalTestsRerun=$false; globalGateAvailable=$false
    note='Art-only local deployment; PNG/Def/native signatures checked. No gameplay acceptance or fee capability unlock.'
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $base '部署验证.json') -Encoding utf8
Write-Host "PASS: 1 Def + 20 PNG deployed and SHA-256 verified; other 210 files unchanged; game restart required. Backup: $backup"
