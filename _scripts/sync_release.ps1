# 开发区 → 发布区 同步 + 两区全量逐文件 MD5 比对
#
# 用法：pwsh -NoProfile -File _scripts/sync_release.ps1 [-Files "a,b"]
#   不传 -Files 时只同步默认三件（dll + 中英 ColonyEconomy Keyed）。
#   ⚠ 2026-09-20（CHG-LCE-001）：MVS 三件（MVS_Commodities.xml / 两个 MVS Keyed）已随经济中心重建删除。
#
# ⚠ 盘符一律大写：PowerShell 的 Get-ChildItem 会把 FullName 规范化成大写盘符，
#   小写 $dev 会让 .Replace() 静默失败 ⇒ 自己比自己 ⇒ 恒报「0 差异」（2026-09-16 踩过）。
# ⚠ 同步**不做清理**（清理会吃掉 About\PublishedFileId.txt —— 开发区天生没有该文件）。
# ⚠ 同步前确认 RimWorldWin64 未运行，否则 Copy-Item 报「无法在使用用户映射区域打开的文件上执行」。
param(
    [string[]]$Files = @(
        'Assemblies\PersonalChronicle.dll',
        'Languages\ChineseSimplified\Keyed\ColonyEconomy.xml',
        'Languages\English\Keyed\ColonyEconomy.xml'
    )
)
$ErrorActionPreference = 'Stop'
$dev = (Get-Item -LiteralPath (Split-Path -Parent $PSScriptRoot)).FullName
$rel = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统'

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
    Write-Error 'RimWorldWin64 正在运行，禁止同步（文件被占用）。'
    exit 2
}

# ---- 1) 备份旧 dll（仅存开发区，禁止随发布区上传）----
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$bakDir = Join-Path $dev "_backups\release-deploy\dll_$stamp"
New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
if (Test-Path "$rel\Assemblies\PersonalChronicle.dll") {
    Copy-Item "$rel\Assemblies\PersonalChronicle.dll" "$bakDir\PersonalChronicle.dll" -Force
}

# ---- 2) 同步本批差异文件 ----
foreach ($f in $Files) {
    if ($f -match '(?i)(^|[\\/])PublishedFileId\.txt$') {
        throw '工坊关联不允许从开发区同步；新账号 ID 由目标上传端维护。'
    }
    $src = Join-Path $dev $f
    $dst = Join-Path $rel $f
    if (-not (Test-Path $src)) { Write-Host "SKIP(missing src): $f"; continue }
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    Copy-Item $src $dst -Force
    Write-Host "SYNC: $f"
}

# ---- 3) 两区全量比对（Defs + Languages + Patches 下所有 xml，加 dll）----
$tops = @('Defs', 'Languages', 'Patches')
$diff = 0; $same = 0; $onlyDev = 0
foreach ($top in $tops) {
    $devTop = Join-Path $dev $top
    if (-not (Test-Path $devTop)) { continue }
    foreach ($f in Get-ChildItem $devTop -Recurse -File -Filter *.xml) {
        $tail = $f.FullName.Substring($dev.Length)
        $rp = "$rel$tail"
        if (-not (Test-Path $rp)) { Write-Host "ONLY-IN-DEV: $tail"; $onlyDev++; continue }
        if ((Get-FileHash $f.FullName -Algorithm MD5).Hash -ne (Get-FileHash $rp -Algorithm MD5).Hash) {
            Write-Host "DIFF: $tail"; $diff++
        } else { $same++ }
    }
    $relTop = Join-Path $rel $top
    if (Test-Path $relTop) {
        foreach ($f in Get-ChildItem $relTop -Recurse -File -Filter *.xml) {
            $tail = $f.FullName.Substring($rel.Length)
            if (-not (Test-Path "$dev$tail")) { Write-Host "ONLY-IN-REL: $tail" }
        }
    }
}
$dllDev = (Get-FileHash "$dev\Assemblies\PersonalChronicle.dll" -Algorithm MD5).Hash
$dllRel = (Get-FileHash "$rel\Assemblies\PersonalChronicle.dll" -Algorithm MD5).Hash
Write-Host ''
Write-Host "XML identical=$same  DIFF=$diff  ONLY-IN-DEV=$onlyDev"
Write-Host "DLL dev=$dllDev"
Write-Host "DLL rel=$dllRel"
Write-Host ("DLL MATCH=" + ($dllDev -eq $dllRel))
Write-Host ("PublishedFileId 仍在: " + (Test-Path (Join-Path $rel 'About\PublishedFileId.txt')))
