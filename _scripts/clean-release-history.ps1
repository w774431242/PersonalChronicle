<#
  发布区历史文件清理：默认只预览，-Apply 才执行。
  完整复制到开发区并校验 SHA256 后，才逐文件移除发布区副本。
  保留文件也记录 SHA256；报告与清单只写开发区。
#>
param(
    [string]$Target = '',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$devRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
if (-not $Target) {
    $Target = Join-Path (Split-Path -Parent (Split-Path -Parent $devRoot)) 'Mods\殖民地综合管理系统'
}
$Target = (Get-Item -LiteralPath $Target).FullName.TrimEnd('\')
if ($Target -eq $devRoot -or $Target.StartsWith($devRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $devRoot.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw '开发区与发布区必须是互不包含的目录。'
}
$srcAbout = [xml](Get-Content -LiteralPath (Join-Path $devRoot 'About\About.xml') -Raw)
$dstAbout = [xml](Get-Content -LiteralPath (Join-Path $Target 'About\About.xml') -Raw)
if (-not $srcAbout.ModMetaData.packageId -or $srcAbout.ModMetaData.packageId -ne $dstAbout.ModMetaData.packageId) {
    throw 'packageId 缺失或不一致，拒绝清理。'
}
$tree = @(Get-ChildItem -LiteralPath $Target -Recurse -Force)
if (@($tree | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
    throw '发布区含链接或重解析点，需人工检查后再清理。'
}
$files = @($tree | Where-Object { -not $_.PSIsContainer })
$history = @($files | Where-Object {
    $_.FullName.StartsWith($Target + '\_backups\', [StringComparison]::OrdinalIgnoreCase) -or
    $_.Name -match '\.bak(?:_|$)|\.locked_' -or $_.Extension -eq '.pdb'
})
$retained = @($files | Where-Object { $_ -notin $history })
$beforeBytes = [long](($files | Measure-Object Length -Sum).Sum)
$historyBytes = [long](($history | Measure-Object Length -Sum).Sum)
Write-Output ('发布区：{0}；总计 {1} files / {2:N3} MiB' -f $Target, $files.Count, ($beforeBytes / 1MB))
Write-Output ('待归档：{0} files / {1:N3} MiB；保留 {2} files' -f $history.Count, ($historyBytes / 1MB), $retained.Count)
if (-not $Apply) { Write-Output '预览完成；使用 -Apply 执行备份校验与清理。'; return }
if ($history.Count -eq 0) { Write-Output '没有历史文件需要清理。'; return }
$archive = [IO.Path]::GetFullPath((Join-Path $devRoot ('_backups\release-cleanup_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))))
if (-not $archive.StartsWith($devRoot + '\_backups\', [StringComparison]::OrdinalIgnoreCase) -or
    $archive.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $archive)) {
    throw '归档路径越界或已存在，拒绝写入。'
}
New-Item -ItemType Directory -Path $archive | Out-Null
Write-Output "归档目录：$archive"
$keepManifest = @(foreach ($file in $retained) {
    [pscustomobject]@{ RelativePath = $file.FullName.Substring($Target.Length + 1); Bytes = $file.Length;
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
})
$keepManifest | Export-Csv -LiteralPath (Join-Path $archive 'retained-before.csv') -NoTypeInformation -Encoding utf8
$manifest = @(foreach ($file in $history) {
    $relative = $file.FullName.Substring($Target.Length + 1)
    $destination = [IO.Path]::GetFullPath((Join-Path $archive ('files\' + $relative)))
    if (-not $file.FullName.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $destination.StartsWith($archive + '\files\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "文件路径越界：$relative"
    }
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) {
        throw "归档校验失败，发布区尚未清理：$relative"
    }
    [pscustomobject]@{ RelativePath = $relative; Bytes = $file.Length; SHA256 = $hash }
})
$manifest | Export-Csv -LiteralPath (Join-Path $archive 'archived-files.csv') -NoTypeInformation -Encoding utf8
Write-Output "已归档并校验 $($manifest.Count) 个文件；开始移除发布区副本。"
foreach ($entry in $manifest) {
    $source = [IO.Path]::GetFullPath((Join-Path $Target $entry.RelativePath))
    if (-not $source.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '删除路径越界。' }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.SHA256) {
        throw "源文件在归档后发生变更，停止清理：$($entry.RelativePath)"
    }
    Remove-Item -LiteralPath $source -Force
}
# 仅删除已清空的 _backups 子目录；不递归删除任何非空目录。
$backupDir = Join-Path $Target '_backups'
if (Test-Path -LiteralPath $backupDir) {
    $emptyCandidates = @(Get-ChildItem -LiteralPath $backupDir -Directory -Recurse -Force | Sort-Object { $_.FullName.Length } -Descending)
    $emptyCandidates += Get-Item -LiteralPath $backupDir
    foreach ($dir in $emptyCandidates) {
        if (-not $dir.FullName.StartsWith($Target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '空目录删除路径越界。' }
        if (@(Get-ChildItem -LiteralPath $dir.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $dir.FullName -Force }
    }
}
foreach ($entry in $keepManifest) {
    if ((Get-FileHash -LiteralPath (Join-Path $Target $entry.RelativePath) -Algorithm SHA256).Hash -ne $entry.SHA256) {
        throw "保留文件校验失败：$($entry.RelativePath)"
    }
}
$afterFiles = @(Get-ChildItem -LiteralPath $Target -Recurse -File -Force)
if ($afterFiles.Count -ne $keepManifest.Count) { throw '清理期间文件集合发生变化，请检查。' }
$afterBytes = [long](($afterFiles | Measure-Object Length -Sum).Sum)
$report = [ordered]@{
    CompletedAt = (Get-Date -Format o); Target = $Target; Archive = $archive;
    BeforeFiles = $files.Count; BeforeBytes = $beforeBytes; ArchivedFiles = $manifest.Count;
    ArchivedBytes = $historyBytes; AfterFiles = $afterFiles.Count; AfterBytes = $afterBytes;
    ArchiveSHA256Verified = $true; RetainedSHA256Verified = $true
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $archive 'cleanup-report.json') -Encoding utf8
Write-Output ('清理完成：{0} files / {1:N3} MiB；保留文件 SHA256 全部一致。' -f $afterFiles.Count, ($afterBytes / 1MB))
Write-Output "报告：$(Join-Path $archive 'cleanup-report.json')"
