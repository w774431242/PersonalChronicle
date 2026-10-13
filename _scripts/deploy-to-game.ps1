<#
  部署到游戏加载目录（默认 <RimWorld>\Mods\殖民地综合管理系统）

  ⚠ 为什么需要这一步：
    RimWorld 只扫描 `<RimWorld>\Mods\` 与 Steam Workshop，**不扫描任意位置的工程目录**。
    所以「编译成功」≠「游戏里能看到」—— 必须把 DLL 与资源部署到 Mods\ 下的副本，再重启游戏。

  ⚠ 多版本目录（2026-10-13 改造）：
    工程内容自 `1.6\`（及将来的 `1.7\`）版本目录取，**根级只发 `LoadFolders.xml` 与 `About\`**。
    部署到目标后结构为 `<mod>\LoadFolders.xml + <mod>\About\... + <mod>\1.6\...`，正是引擎期望的形态。

  用法：
    pwsh -NoProfile -File _scripts/deploy-to-game.ps1                    # 部署 1.6（自动备份被覆盖文件）
    pwsh -NoProfile -File _scripts/deploy-to-game.ps1 -WhatIf            # 只列清单不写盘
    pwsh -NoProfile -File _scripts/deploy-to-game.ps1 -GameVersion 1.7   # 部署其它版本目录
    pwsh -NoProfile -File _scripts/deploy-to-game.ps1 -Target <路径>      # 显式目标；-RimWorldRoot 指定游戏根

  行为：
    ① 校验目标有 About\About.xml 且 packageId 与工程一致（防误写别人的 mod 目录）；
    ② 把将被覆盖的文件备份到 <工程>\_backups\release-deploy\deploy_<时间戳>\（保持相对路径）；
    ③ 复制 DLL 与资源；**清点**并打印每个目录的文件数；
    ④ 打印 DLL 时间戳，便于与「游戏里看到的版本」核对。
#>
param(
    [string]$Target = '',
    [string]$RimWorldRoot = '',
    [string]$GameVersion = '1.6',
    [switch]$WhatIf,
    [switch]$SkipBackup
)

$ErrorActionPreference = 'Stop'

# ---- 路径推导：脚本位于 <工程>\_scripts\ ----
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir

# ---- 探测：RimWorld 根（不再硬编码旧工程路径）----
function Resolve-RimWorldRoot {
    param([string]$Explicit)
    $cands = [System.Collections.Generic.List[string]]::new()
    if ($Explicit) { $cands.Add($Explicit) }
    if ($env:RIMWORLD_ROOT) { $cands.Add($env:RIMWORLD_ROOT) }
    $cands.Add('E:\SteamLibrary\steamapps\common\RimWorld')
    $cands.Add('D:\SteamLibrary\steamapps\common\RimWorld')
    $cands.Add('C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
    foreach ($c in $cands) {
        if ($c -and (Test-Path (Join-Path $c 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'))) {
            return (Resolve-Path $c).Path
        }
    }
    throw '未找到可用的 RimWorld 根目录。请用 -RimWorldRoot 指定，或设环境变量 RIMWORLD_ROOT。'
}

if ([string]::IsNullOrEmpty($Target)) {
    $rwRoot = Resolve-RimWorldRoot -Explicit $RimWorldRoot
    $Target = Join-Path $rwRoot 'Mods\殖民地综合管理系统'
}

Write-Host "工程：  $root"
Write-Host "版本：  $GameVersion"
Write-Host "目标：  $Target"
if ($WhatIf) { Write-Host "模式：  WhatIf（只列清单）" -ForegroundColor Yellow }

if (-not (Test-Path $Target)) {
    if ($WhatIf) {
        Write-Host '目标目录不存在（WhatIf：只列清单，不创建）' -ForegroundColor Yellow
    } else {
        New-Item -ItemType Directory -Path $Target -Force | Out-Null
        Write-Host "首次部署：已创建目标目录 $Target" -ForegroundColor Yellow
    }
}

# ---- ① packageId 校验（防误写）----
$srcAbout = Join-Path $root 'About\About.xml'
$dstAbout = Join-Path $Target 'About\About.xml'
$srcId = (Select-String -Path $srcAbout -Pattern '<packageId>([^<]+)</packageId>' |
    Select-Object -First 1).Matches.Groups[1].Value
if (-not (Test-Path $dstAbout)) {
    # 首次部署（目标无旧件可覆盖）⇒ 无覆盖他人 mod 的风险，跳过比对但登记源 packageId
    Write-Host "目标无 About\About.xml（首次部署）⇒ 跳过 packageId 比对 · 源 packageId=$srcId" -ForegroundColor Yellow
} else {
    $dstId = (Select-String -Path $dstAbout -Pattern '<packageId>([^<]+)</packageId>' |
        Select-Object -First 1).Matches.Groups[1].Value
    if ($srcId -ne $dstId) {
        throw "packageId 不一致（工程 $srcId / 目标 $dstId）—— 拒绝写入，避免覆盖别的 mod"
    }
    Write-Host "packageId 校验通过：$srcId" -ForegroundColor Green
}

# ---- 需要部署的项（相对路径 / 通配）----
# ⚠⚠ 2026-10-03 两次踩坑后的**唯一正确写法**（务必读这条再改本段）：
#
#   1) `Get-ChildItem -Path` **不支持 `**` 递归语义**（`-Path` 是遗留模式，`**` 被降级为 `*`），
#      所以 `Defs\**\*.xml` 实际等价 `Defs\*\*.xml` —— **要求至少一层子目录**。
#      而本工程全部 Def 都在 `1.6\Defs\` 顶层 ⇒ 该项长期零匹配 ⇒ **整个 Defs 目录从未被部署**，
#      表现为「部署日志里没有 Defs 行」。别再写 `**`。
#
#   2) 补 `-Recurse` 后，**通配路径里不能夹中间通配段**。实测（PowerShell 7）：
#        -Path 'Languages\*\Keyed\*.xml'  **加** -Recurse  ⇒ 0 个（静默漏发！）
#        -Path 'Languages\*\Keyed\*.xml'  不加 -Recurse  ⇒ 6 个
#        -Path 'Languages\*.xml'          加 -Recurse  ⇒ 35 个 ✅
#      即「多段通配 + -Recurse」会解析成空。**通配只允许出现在最后一段**，
#      中间层交给 -Recurse。
#
#   3) 因此本段统一采用「单段叶子通配 + -Recurse」。改完必须看部署输出的分目录统计里
#      **Languages / Defs 两行都在且数量与工程一致**（底部 `Verify-DeployCoverage` 会自动断言）。
$items = @(
    'LoadFolders.xml',
    "$GameVersion\Assemblies\*.dll",
    'About\About.xml',
    'About\Preview.png',
    'About\Screens\*.png',
    'README.md',
    "$GameVersion\Languages\*.xml",
    "$GameVersion\Defs\*.xml",
    "$GameVersion\Patches\*.xml",
    "$GameVersion\Textures\*.png",
    "$GameVersion\Textures\*.jpg",
    "$GameVersion\Fonts\*.*"
)

# 工坊关联由上传端维护，部署绝不复制源 PublishedFileId.txt。
# 新条目首次上传：目标没有该文件。创建成功后：目标保留游戏写入的新 ID。
# 这样旧账号 ID 不会经开发区同步污染新账号的发布目录。
if (Test-Path -LiteralPath (Join-Path $root 'About\PublishedFileId.txt')) {
    Write-Host '开发区含 PublishedFileId.txt：本次忽略，目标工坊关联保持原样。' -ForegroundColor Yellow
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$backupRoot = Join-Path $root "_backups\release-deploy\deploy_$stamp"
if ([IO.Path]::GetFullPath($backupRoot).StartsWith(
    [IO.Path]::GetFullPath($Target).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw '备份路径不得位于发布区内；请将部署目标与开发区备份目录分开。'
}
$copied = 0
$backed = 0
$perDir = @{}

foreach ($item in $items) {
    # ⚠ 必须用 `-Recurse`：`Get-ChildItem -Path` 不支持 `**`（见上方 $items 处的说明）。
    #   少了它，`Defs\*.xml` 只会匹配到 Defs 顶层的文件、漏掉任何子目录；反之亦然。
    if ($item -match '[*?]') {
        $resolved = Get-ChildItem -Path (Join-Path $root $item) -Recurse -File -ErrorAction SilentlyContinue
    } else {
        # 精确文件不得加 -Recurse：否则根 README.md 会匹配开发目录中的同名文件。
        $exactPath = Join-Path $root $item
        $resolved = @(if (Test-Path -LiteralPath $exactPath -PathType Leaf) { Get-Item -LiteralPath $exactPath })
    }
    foreach ($file in $resolved) {
        $rel = $file.FullName.Substring($root.Length).TrimStart('\')
        $dstPath = Join-Path $Target $rel
        $dstDir = Split-Path -Parent $dstPath

        # 统计
        $top = ($rel -split '\\')[0]
        if ($perDir.ContainsKey($top)) { $perDir[$top] = $perDir[$top] + 1 } else { $perDir[$top] = 1 }

        if ($WhatIf) {
            Write-Host ("  将写入 {0}" -f $rel)
            continue
        }

        # ② 备份将被覆盖的文件
        if ((Test-Path $dstPath) -and (-not $SkipBackup)) {
            $bak = Join-Path $backupRoot $rel
            $bakDir = Split-Path -Parent $bak
            if (-not (Test-Path $bakDir)) { New-Item -ItemType Directory -Path $bakDir -Force | Out-Null }
            Copy-Item -Path $dstPath -Destination $bak -Force
            $backed++
        }

        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        Copy-Item -Path $file.FullName -Destination $dstPath -Force
        $copied++
    }
}

if ($WhatIf) {
    Write-Host ""
    Write-Host "清单统计：" -ForegroundColor Cyan
    foreach ($k in ($perDir.Keys | Sort-Object)) { Write-Host ("  {0,-12} {1} 个文件" -f $k, $perDir[$k]) }
    return
}

Write-Host ""
Write-Host "已复制 $copied 个文件；备份 $backed 个到 $backupRoot" -ForegroundColor Green
foreach ($k in ($perDir.Keys | Sort-Object)) { Write-Host ("  {0,-12} {1} 个文件" -f $k, $perDir[$k]) }

$dstDll = Join-Path $Target "$GameVersion\Assemblies\PersonalChronicle.dll"
if (Test-Path $dstDll) {
    $d = Get-Item $dstDll
    Write-Host ""
    Write-Host ("目标 DLL：{0}  {1:N1} KB" -f $d.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), ($d.Length / 1KB)) -ForegroundColor Cyan
}

# ---- ⑤ 覆盖率自检（2026-10-03）：断言每个关键目录都真的发出去过 ----
#  本脚本已两次静默漏发（Defs 全目录、Languages 全部），两次都**没有任何报错**，
#  只有「游戏里 Def / 翻译没生效」这种间接症状可查。通配符解析一旦写错，
#  复制数变成 0 而脚本照常退出 0 ⇒ 此处必须硬失败（exit 1）。
$stale = @()
# 自检必须与部署通配**同口径**：只检查"本来就该发"的文件，否则会把正确的跳过
# （`*.bak_*` 编译备份、`.pdb` 等）误报成漏发。版本目录同样要带前缀。
$checkMap = [ordered]@{
    "$GameVersion\Defs"       = '*.xml'
    "$GameVersion\Languages"  = '*.xml'
    "$GameVersion\Assemblies" = '*.dll'
}
foreach ($dir in $checkMap.Keys) {
    $srcFiles = @(Get-ChildItem -Path (Join-Path $root $dir) -Filter $checkMap[$dir] -Recurse -File -ErrorAction SilentlyContinue)
    if ($srcFiles.Count -eq 0) { continue }
    $notNewer = 0
    foreach ($f in $srcFiles) {
        $rel = $f.FullName.Substring($root.Length).TrimStart('\')
        $dstPath = Join-Path $Target $rel
        if (-not (Test-Path $dstPath)) { $notNewer++; continue }
        $d2 = Get-Item $dstPath
        if ($d2.LastWriteTime -lt $f.LastWriteTime) { $notNewer++ }
    }
    if ($notNewer -gt 0) {
        $stale += ("{0}: {1}/{2} 个文件未同步" -f $dir, $notNewer, $srcFiles.Count)
    } else {
        Write-Host ("  OK {0,-14} {1} files synced" -f $dir, $srcFiles.Count) -ForegroundColor Green
    }
}
if ($stale.Count -gt 0) {
    Write-Host ""
    Write-Host "[X] 部署覆盖率自检失败：" -ForegroundColor Red
    $stale | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    Write-Host "   常见原因：通配符写错导致匹配为 0（见脚本顶部注释的三条约束）。" -ForegroundColor Red
    exit 1
}

Write-Host "⚠ 下一步必须**重启 RimWorld**（运行中不会重载程序集），再打开设备卡弹窗核对。" -ForegroundColor Yellow
