# 物流管道图集校验：按网格逐格检测「背景框」与「四向臂位」是否符合掩码。
# 用法：
#   .\_scripts\verify_pipe_atlas.ps1 -InputPng <图集> [-Rows 4] [-Cols 4]
# 检测项：
#   ① 背景框：每格内缩 10px 的四角 alpha 必须 ≈0（>8 视为有底块/边框）
#   ② 臂位：每格四方向边缘中点 alpha（>8 视为该方向有臂），与 mask 期望比对
# 掩码约定 bit0=N bit1=E bit2=S bit3=W，reading order：M0..M15
param(
    [Parameter(Mandatory = $true)][string]$InputPng,
    [int]$Rows = 4,
    [int]$Cols = 4
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (!(Test-Path $InputPng)) { throw "file not found: $InputPng" }
$src = [System.Drawing.Bitmap]::FromFile($InputPng)
$W = $src.Width; $H = $src.Height
"atlas: ${W}x${H}   grid: ${Rows}x${Cols}"
$cw = [int]($W / $Cols); $ch = [int]($H / $Rows)
"cell : ${cw}x${ch}"
""

function Get-Expected([int]$m) {
    if ($m -eq 0) { return @(0, 0, 0, 0) }   # 孤段：边缘无臂
    return @(($m -band 1), (($m -band 2) -shr 1), (($m -band 4) -shr 2), (($m -band 8) -shr 3))
}

$problems = New-Object System.Collections.ArrayList
for ($r = 0; $r -lt $Rows; $r++) {
    for ($c = 0; $c -lt $Cols; $c++) {
        $m = $r * $Cols + $c
        $x0 = $c * $cw; $y0 = $r * $ch

        # ① 背景框：格内四角（内缩）
        $i = 10
        $maxCorner = @(
            $src.GetPixel($x0 + $i, $y0 + $i).A,
            $src.GetPixel($x0 + $cw - $i - 1, $y0 + $i).A,
            $src.GetPixel($x0 + $i, $y0 + $ch - $i - 1).A,
            $src.GetPixel($x0 + $cw - $i - 1, $y0 + $ch - $i - 1).A
        ) | Measure-Object -Maximum | ForEach-Object { $_.Maximum }

        # ② 臂位：四方向边缘中点
        $cx = $x0 + [int]($cw / 2); $cy = $y0 + [int]($ch / 2)
        $e = 3
        $got = @(
            $src.GetPixel($cx, $y0 + $e).A,
            $src.GetPixel($x0 + $cw - 1 - $e, $cy).A,
            $src.GetPixel($cx, $y0 + $ch - 1 - $e).A,
            $src.GetPixel($x0 + $e, $cy).A
        ) | ForEach-Object { if ($_ -gt 8) { 1 } else { 0 } }

        $exp = Get-Expected $m
        $gotStr = ($got -join ''); $expStr = ($exp -join '')
        $okArm = ($got[0] -eq $exp[0]) -and ($got[1] -eq $exp[1]) -and ($got[2] -eq $exp[2]) -and ($got[3] -eq $exp[3])

        $flag = ''
        if ($maxCorner -gt 8) { $flag += " BACKDROP(corner alpha=$maxCorner)" }
        if (-not $okArm) { $flag += " ARMS(got=$gotStr exp=$expStr)" }
        if ($flag -eq '') { $flag = 'ok'; $mark = ' ' } else { $mark = 'X'; [void]$problems.Add("M$m$r$c$flag") }
        "  [$mark] M{0,-2} r{1}c{2}  corner={3,-3}  arms={4}  {5}" -f $m, $r, $c, $maxCorner, $gotStr, $flag
    }
}
""
"=== problems: " + $problems.Count + " ==="
$problems | ForEach-Object { "  " + $_ }
$src.Dispose()
