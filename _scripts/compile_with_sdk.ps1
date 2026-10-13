# Compile PersonalChronicle into the mod's real assembly (Assemblies/PersonalChronicle.dll)
# using .NET 8 SDK csc.dll (full C# syntax). BCL surface = RimWorld's own netstandard.dll
# (a forwarder to its mscorlib) + RimWorld's mscorlib, so the produced DLL matches the
# runtime exactly and loads cleanly. Auto-backs up the previous assembly.
$ErrorActionPreference = "Stop"

$rw = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
$devRoot = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $devRoot 'Source\PersonalChronicle'
$outDir = Join-Path $devRoot 'Assemblies'
$outDll = Join-Path $outDir "PersonalChronicle.dll"
$backup = Join-Path $outDir ("PersonalChronicle.dll.bak_" + (Get-Date -Format "yyyyMMddHHmmss"))
$rsp = Join-Path $PSScriptRoot 'compile.rsp'

if (Test-Path $outDll) { Copy-Item $outDll $backup -Force; Write-Host "Backup -> $backup" }

# ── 构建时间戳（CHG-20261002-FLOORDETAIL-AUDIT）────────────────────────────────
# csproj 的 WriteBuildStamp 目标只在 `dotnet build` 路径下执行；本脚本走**裸 csc** ⇒
# BuildStamp.g.cs 不会刷新，DevMode 卡头一直显示**上一次 dotnet build** 的时间
#（2026-10-02 实测：新 DLL 的卡头仍显示 10:24 的旧戳）⇒ 会被误读成"改了没变化"。
# 此处按与 csproj 相同的格式重写，使卡头时间戳恒等于本次编译时刻（UTC, MM-dd HH:mm）。
$stampPath = Join-Path $srcRoot 'BuildStamp.g.cs'
$stampText = "namespace PersonalChronicle`r`n{`r`n    internal static class BuildInfo`r`n    {`r`n        internal const string BuildTimeUtc = `"$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm'))`";`r`n    }`r`n}"
[System.IO.File]::WriteAllText($stampPath, $stampText, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("BuildStamp -> {0} UTC" -f [System.DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm'))

$lines = @(
    "/nologo"
    "/target:library"
    "/nostdlib"
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
    "/reference:E:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"
    # RUIF（本工程同目录下的 UI 框架，**硬依赖**）：少了这行会出现成片的
    # `CS0103: 当前上下文中不存在名称“RUIF”`（UIComponents / RuifBridge / Roster 等），
    # 而报错位置全是既有代码 —— 见到「RUIF 不存在」先查这一行，不要去改业务代码。
    "/reference:E:\SteamLibrary\steamapps\common\RimWorld\11\RUIF\Assemblies\RUIF.dll"
    "/unsafe"
    "/define:TRACE;RELEASE;NET48"
    "/warn:4"
)

foreach ($f in Get-ChildItem -Path $srcRoot -Recurse -Filter *.cs) {
    $lines += $f.FullName
}

$lines | Out-File -FilePath $rsp -Encoding UTF8

& dotnet exec "D:\toolbox\dotnet\sdk\8.0.424\Roslyn\bincore\csc.dll" "@$rsp"
