# Compile PersonalChronicle into the mod's real assembly:
#   <devRoot>\<GameVersion>\Assemblies\PersonalChronicle.dll
# using .NET SDK csc.dll (full C# syntax). BCL surface = RimWorld's own netstandard.dll
# (a forwarder to its mscorlib) + RimWorld's mscorlib, so the produced DLL matches the
# runtime exactly and loads cleanly. Auto-backs up the previous assembly.
#
# 2026-10-13 多版本目录改造（CHG: 多版本兼容骨架）：
#   · 全部外部路径改为**探测式**，不再硬编码旧的 `RimWorld\11\...` 绝对路径；
#   · 输出目录随 -GameVersion 落到 `<devRoot>\<GameVersion>\Assemblies\`；
#   · 缺少硬依赖（RUIF）时**明确失败**，不再静默引用死路径。
param(
    [string]$GameRoot = '',
    [string]$GameVersion = '1.6',
    [string]$CscPath = ''
)
$ErrorActionPreference = 'Stop'

# ---- 路径推导：脚本位于 <devRoot>\_scripts\ ----
$devRoot = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $devRoot 'Source\PersonalChronicle'
$outDir = Join-Path (Join-Path $devRoot $GameVersion) 'Assemblies'
$outDll = Join-Path $outDir 'PersonalChronicle.dll'
$rsp = Join-Path $PSScriptRoot 'compile.rsp'

# ---- 探测：RimWorld 根 ----
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
    throw '未找到可用的 RimWorld 根目录（需含 RimWorldWin64_Data\Managed\Assembly-CSharp.dll）。请用 -GameRoot 指定，或设环境变量 RIMWORLD_ROOT。'
}

# ---- 探测：工坊 mod 的指定版本程序集 ----
function Resolve-WorkshopAsm {
    param([string]$SteamApps, [string]$ItemId, [string]$FileName, [string]$Version)
    if (-not $SteamApps) { return $null }
    $base = Join-Path $SteamApps "workshop\content\294100\$ItemId"
    $cands = @(
        (Join-Path $base "$Version\Assemblies\$FileName"),
        (Join-Path $base "Current\Assemblies\$FileName"),
        (Join-Path $base "Assemblies\$FileName")
    )
    foreach ($c in $cands) { if (Test-Path $c) { return (Resolve-Path $c).Path } }
    return $null
}

# ---- 探测：csc.dll ----
function Resolve-Csc {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path $Explicit)) { return (Resolve-Path $Explicit).Path }
    $sdkRoots = @('D:\toolbox\dotnet\sdk')
    if ($env:ProgramFiles) { $sdkRoots += (Join-Path $env:ProgramFiles 'dotnet\sdk') }
    $sdkRoots += 'C:\Program Files\dotnet\sdk'
    foreach ($r in $sdkRoots) {
        if (-not (Test-Path $r)) { continue }
        $hits = Get-ChildItem $r -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'Roslyn\bincore\csc.dll' } |
            Where-Object { Test-Path $_ }
        # ⚠ 必须显式数组化：单元素时 `$hits[0]` 会退化成**首字符**（实测报 "Cannot find path ...\D"）
        $hitList = @($hits)
        if ($hitList.Count -gt 0) { return (Resolve-Path -LiteralPath $hitList[0]).Path }
    }
    throw '未找到 Roslyn csc.dll；请用 -CscPath 指定（例如 <sdk>\<ver>\Roslyn\bincore\csc.dll）。'
}

$gameRoot = Resolve-RimWorldRoot -Explicit $GameRoot
$rw = Join-Path $gameRoot 'RimWorldWin64_Data\Managed'
$steamApps = Split-Path -Parent (Split-Path -Parent $gameRoot)   # <steamapps>（即 common 的上一级）

$harmonyDll = Resolve-WorkshopAsm -SteamApps $steamApps -ItemId '2009463077' -FileName '0Harmony.dll' -Version $GameVersion
if (-not $harmonyDll) {
    $fallback = Join-Path $rw '..\..\Mods\Harmony\Assemblies\0Harmony.dll'
    if (Test-Path $fallback) { $harmonyDll = (Resolve-Path $fallback).Path }
}
if (-not $harmonyDll) { throw "未找到 0Harmony.dll（工坊 2009463077 $GameVersion 或 Mods\Harmony）。请先安装 Harmony。" }

$ruifDll = Resolve-WorkshopAsm -SteamApps $steamApps -ItemId '3799502474' -FileName 'RUIF.dll' -Version $GameVersion
if (-not $ruifDll) {
    foreach ($extra in @((Join-Path $steamApps 'common\RimWorld\Mods\RUIF\Assemblies\RUIF.dll'))) {
        if (Test-Path $extra) { $ruifDll = (Resolve-Path $extra).Path }
    }
}
if (-not $ruifDll) {
    throw '未找到 RUIF.dll（硬依赖：工坊 3799502474 的 RUI 界面框架）。请先订阅该框架（或把 RUIF.dll 放进 Mods\RUIF\Assemblies\）后重跑。缺失时会成片报 CS0103 "RUIF 不存在"，那不是业务代码的问题。'
}

Write-Host "RimWorld : $rw"
Write-Host "Harmony  : $harmonyDll"
Write-Host "RUIF     : $ruifDll"
Write-Host "版本目录 : $GameVersion  ->  $outDir"

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
# 2026-10-13 按用户要求：**不再自动生成 `.bak` 备份文件**（用户不要备份文件；旧的 779 个 `.bak_*` 已清掉）。
# 回滚依据改为版本控制：`1.6/Assemblies/PersonalChronicle.dll` 受 git 跟踪 ⇒ 需要旧件时用
#   `git checkout -- 1.6/Assemblies/PersonalChronicle.dll`（或 `git show HEAD:<path> > <path>`）还原。
# 若确需临时留旧件，请显式复制到工程目录之外，勿在 mod 目录内残留备份。

# ── 构建时间戳 ────────────────────────────────────────────────────────────────
# csproj 的 WriteBuildStamp 目标只在 `dotnet build` 路径下执行；本脚本走**裸 csc** ⇒
# BuildStamp.g.cs 不会刷新，DevMode 卡头一直显示**上一次 dotnet build** 的时间
#（2026-10-02 实测：新 DLL 的卡头仍显示 10:24 的旧戳）⇒ 会被误读成"改了没变化"。
# 此处按与 csproj 相同的格式重写，使卡头时间戳恒等于本次编译时刻（UTC, MM-dd HH:mm）。
$stampPath = Join-Path $srcRoot 'BuildStamp.g.cs'
$stampText = "namespace PersonalChronicle`r`n{`r`n    internal static class BuildInfo`r`n    {`r`n        internal const string BuildTimeUtc = `"$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm'))`";`r`n    }`r`n}"
[System.IO.File]::WriteAllText($stampPath, $stampText, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("BuildStamp -> {0} UTC" -f [System.DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm'))

$lines = @(
    '/nologo'
    '/target:library'
    '/nostdlib'
    "/out:$outDll"
    "/reference:$rw\netstandard.dll"
    "/reference:$rw\mscorlib.dll"
    "/reference:$rw\System.dll"
    "/reference:$rw\System.Core.dll"
    # System.Xml / System.Xml.Linq（2026-10-08 修）：Data 层的 Scribe 编码器
    # （SettlementLedgerCodec / TreasuryOperationsSaveData / InstitutionModeSaveData）与
    # PawnEconomySaveData 的 XmlSerializer/XmlArrayItem 需要它们。缺失时 csc 会对**既有**
    # Data 文件报 CS0246/CS1069（类型已转发到 System.Xml）—— 见到这类错误先查本引用集，
    # 不要去改业务代码。
    "/reference:$rw\System.Xml.dll"
    "/reference:$rw\System.Xml.Linq.dll"
    "/reference:$rw\Assembly-CSharp.dll"
    "/reference:$rw\UnityEngine.dll"
    "/reference:$rw\UnityEngine.CoreModule.dll"
    "/reference:$rw\UnityEngine.IMGUIModule.dll"
    "/reference:$rw\UnityEngine.TextRenderingModule.dll"
    "/reference:$harmonyDll"
    # RUIF（硬依赖，上方已探测）：少了这行会出现成片的
    # `CS0103: 当前上下文中不存在名称“RUIF”`（UIComponents / RuifBridge / Roster 等），
    # 而报错位置全是既有代码 —— 见到「RUIF 不存在」先查这一行，不要去改业务代码。
    "/reference:$ruifDll"
    '/unsafe'
    '/define:TRACE;RELEASE;NET48'
    '/warn:4'
)

foreach ($f in Get-ChildItem -Path $srcRoot -Recurse -Filter *.cs) {
    $lines += $f.FullName
}

$lines | Out-File -FilePath $rsp -Encoding UTF8

$csc = Resolve-Csc -Explicit $CscPath
& dotnet exec $csc "@$rsp"
