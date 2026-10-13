# LOC-09 取证：源码中「非注释中文/日文字面量」扫描与分类（只读）
#
# 规则：用户可见文本必须经 Localization 管道（GOV-010 / LOC-001）。
# 本脚本剥掉注释与 XML 文本后，扫描 C# 字符串字面量里的 CJK，并按用途分档：
#   [DEV]  开发者自检/夹具（路径含 /Dev 或类名 Dev*）—— 仅 DevMode 可见，非玩家文案
#   [DIAG] 日志/探针/断言（同行含 Log./PerfProbe/Assert/Exception 等）—— 非 UI
#   [UI?]  其余 —— 需人工判定是否玩家可见（若为玩家文案，应改为 Keyed）
# 用法: pwsh -NoProfile -File _scripts/audit-cjk-literals.ps1 [-Quiet]
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$MOD = Split-Path -Parent $PSScriptRoot

function Cjk { param([string]$s) return ($s -match '[\u3000-\u9fff\uff00-\uffef]') }

# 剥离注释与 XML 文本，返回 (保留文本, 行号映射不可用 ⇒ 逐行近似)
function StripComments([string]$src) {
    $sb = New-Object System.Text.StringBuilder
    $i = 0; $n = $src.Length
    $inLine = $false; $inBlock = $false; $inStr = $false; $inVerbatim = $false; $inChar = $false
    while ($i -lt $n) {
        $c = $src[$i]; $next = if ($i + 1 -lt $n) { $src[$i + 1] } else { [char]0 }
        if ($inLine) { if ($c -eq "`n") { $inLine = $false; $null = $sb.Append($c) }; $i++; continue }
        if ($inBlock) { if ($c -eq '*' -and $next -eq '/') { $inBlock = $false; $i += 2; continue }; if ($c -eq "`n") { $null = $sb.Append($c) }; $i++; continue }
        if ($inStr) {
            if ($inVerbatim) { if ($c -eq '"' -and $next -eq '"') { $null = $sb.Append('""'); $i += 2; continue }; if ($c -eq '"') { $inStr = $false; $inVerbatim = $false } }
            else { if ($c -eq '\') { $null = $sb.Append($c); if ($i + 1 -lt $n) { $null = $sb.Append($src[$i + 1]) }; $i += 2; continue }; if ($c -eq '"') { $inStr = $false } }
            $null = $sb.Append($c); $i++; continue
        }
        if ($inChar) { if ($c -eq '\') { $null = $sb.Append($c); $i += 2; continue }; if ($c -eq "'") { $inChar = $false }; $null = $sb.Append($c); $i++; continue }
        if ($c -eq '/' -and $next -eq '/') { $inLine = $true; $i += 2; continue }
        if ($c -eq '/' -and $next -eq '*') { $inBlock = $true; $i += 2; continue }
        if ($c -eq '"') { $inStr = $true; $inVerbatim = ($i -gt 0 -and $src[$i - 1] -eq '@'); $null = $sb.Append($c); $i++; continue }
        if ($c -eq "'") { $inChar = $true; $null = $sb.Append($c); $i++; continue }
        $null = $sb.Append($c); $i++
    }
    return $sb.ToString()
}

$dev = 0; $diag = 0; $ui = @()
Get-ChildItem "$MOD/Source" -Recurse -File -Filter *.cs | ForEach-Object {
    $path = $_.FullName
    $src = StripComments ([System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8))
    $lines = $src -split "`n"
    for ($ln = 0; $ln -lt $lines.Count; $ln++) {
        $line = $lines[$ln]
        foreach ($m in [regex]::Matches($line, '"([^"\\]*(?:\\.[^"\\]*)*)"')) {
            if (-not (Cjk $m.Groups[1].Value)) { continue }
            $isDev = ($path -match '\\Dev|DevSelfTest|DevCoverageProbe|DevPopulationFixtures|DevTest|Probe')
            $isDiag = ($line -match 'Log\.|LogError|LogWarning|PerfProbe|Assert|throw new|Exception|TODO|FIXME|Debug\.')
            if ($isDev) { $dev++ } elseif ($isDiag) { $diag++ } else {
                $ui += [pscustomobject]@{ File = $path.Replace($MOD, '<MOD>'); Line = $ln + 1; Text = $m.Groups[1].Value }
            }
        }
    }
}
Write-Output ("非注释 CJK 字面量：DEV={0}  DIAG={1}  UI?={2}" -f $dev, $diag, @($ui).Count)
if (-not $Quiet) {
    $ui | Group-Object File | Sort-Object Count -Descending | ForEach-Object {
        Write-Output ("--- {0}  ({1})" -f $_.Name, $_.Count)
        $_.Group | Select-Object -First 6 | ForEach-Object { Write-Output ("     L{0}: {1}" -f $_.Line, $_.Text.Substring(0, [Math]::Min(90, $_.Text.Length))) }
    }
}
exit 0
