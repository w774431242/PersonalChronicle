# Generate "underground trench" pipe sprites (PM: change to underground pipes, no need for too many textures).
# Design: each tile is a dug trench crossing the FULL tile edge-to-edge (no end rings),
# so adjacent segments join seamlessly - no more visible seams between pipe segments.
# 15 files (same names as before, zero code changes):
#   Body / Body_north / Body_south  = vertical trench (all through)
#   Body_east / Body_west           = horizontal trench (all through)
#   In/Out + suffix                 = through-trench with a colored port collar at that end
# Port colors: In = blue (faces the device), Out = orange (faces the target).
param(
    [string]$Dir = (Join-Path $PSScriptRoot '..\Textures\Things\Building\Automation'),
    [string]$BackupDir = (Join-Path $PSScriptRoot ('..\_backups\pipe_trench_' + (Get-Date -Format 'yyyyMMdd_HHmmss')))
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $Dir, $BackupDir | Out-Null

# palette
# 配色取自玩家提供的 AI 参考图集（2026-09-07 像素统计）：冷调、低饱和、深灰近黑。
#   主色分布：rgb(16,16,16) 32.7% / rgb(48,48,48) 31.4% / rgb(80,80,80) 8.9% / rgb(112,112,112) 3.1%
$trenchBg = [System.Drawing.Color]::FromArgb(255, 40, 42, 46)     # 沟道内部（冷灰）
$trenchEdge = [System.Drawing.Color]::FromArgb(255, 16, 16, 16)   # 沟壁描边（近黑）
$pipeBody = [System.Drawing.Color]::FromArgb(255, 80, 80, 80)     # 管体主色（中灰）
$pipeHi = [System.Drawing.Color]::FromArgb(255, 112, 112, 112)    # 高光（浅灰）
$pipeLo = [System.Drawing.Color]::FromArgb(255, 48, 48, 48)       # 暗部（深灰）
$inCol = [System.Drawing.Color]::FromArgb(255, 74, 134, 206)      # blue port
$outCol = [System.Drawing.Color]::FromArgb(255, 214, 132, 48)     # orange port

function New-Trench([bool]$vertical, [string]$portEnd, [System.Drawing.Color]$portColor) {
    # vertical: trench runs y (north-south); horizontal: runs x.
    # portEnd: '' | 'start' | 'end'  (start = north/west end, end = south/east end)
    $b = New-Object System.Drawing.Bitmap(64, 64)
    # trench width 24 (x 20..43 vertical), pipe core 10 (x 27..36)
    for ($i = 0; $i -lt 64; $i++) {
        for ($j = 20; $j -le 43; $j++) {
            if ($vertical) { $b.SetPixel($j, $i, $trenchBg) } else { $b.SetPixel($i, $j, $trenchBg) }
        }
        foreach ($e in 19, 44) {
            if ($vertical) { $b.SetPixel($e, $i, $trenchEdge) } else { $b.SetPixel($i, $e, $trenchEdge) }
        }
        for ($j = 27; $j -le 36; $j++) {
            $c = $pipeBody
            if ($j -le 28) { $c = $pipeLo } elseif ($j -le 31) { $c = $pipeHi } elseif ($j -ge 35) { $c = $pipeLo }
            if ($vertical) { $b.SetPixel($j, $i, $c) } else { $b.SetPixel($i, $j, $c) }
        }
    }
    if ($portEnd -ne '') {
        # port collar: 16px long block across the trench at the given end
        $lo = 0; $hi = 15
        if ($portEnd -eq 'end') { $lo = 48; $hi = 63 }
        for ($i = $lo; $i -le $hi; $i++) {
            for ($j = 18; $j -le 45; $j++) {
                $border = ($j -le 20 -or $j -ge 43 -or $i -eq $lo -or $i -eq $hi)
                $c = if ($border) { $portColor } else { [System.Drawing.Color]::FromArgb(255, 34, 30, 26) }
                if ($vertical) { $b.SetPixel($j, $i, $c) } else { $b.SetPixel($i, $j, $c) }
            }
        }
    }
    return $b
}

function Save-Variants([string]$kind, [System.Drawing.Color]$portColor) {
    # vertical north-port / south-port; horizontal east-port / west-port
    $nv = New-Trench $true 'start' $portColor
    $sv = New-Trench $true 'end'   $portColor
    $eh = New-Trench $false 'end'   $portColor   # port at x-high end = east
    $wh = New-Trench $false 'start' $portColor   # port at x-low  end = west
    $pairs = @(
        @("$kind.png", $eh), @("$kind`_east.png", $eh),
        @("$kind`_west.png", $wh),
        @("$kind`_north.png", $nv), @("$kind`_south.png", $sv)
    )
    foreach ($p in $pairs) {
        $path = Join-Path $Dir $p[0]
        if (Test-Path $path) { Copy-Item $path (Join-Path $BackupDir $p[0]) -Force }
        $p[1].Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        "saved $($p[0])"
    }
    # 位图被多个文件名复用（In 与 In_east 同图），循环结束后统一释放
    $nv.Dispose(); $sv.Dispose(); $eh.Dispose(); $wh.Dispose()
}

# ---- Body: connection-mask sprites (16 variants) ----
# PM 实测：拖线拐角处两根直管相接没有 L 形。改为按「四邻连接掩码」出图：
# bit0=N bit1=E bit2=S bit3=W，臂式沟槽（延伸到格边保证拼接），中心 24x24 交叉块。
# 覆盖全部 16 种组合（直线/端头/L 拐角/T/十字），拐角自动成直角。
function DrawVertArm([System.Drawing.Bitmap]$b, [int]$y0, [int]$y1) {
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = 20; $x -le 43; $x++) { $b.SetPixel($x, $y, $trenchBg) }
        $b.SetPixel(19, $y, $trenchEdge); $b.SetPixel(44, $y, $trenchEdge)
        for ($x = 27; $x -le 36; $x++) {
            $c = $pipeBody
            if ($x -le 28) { $c = $pipeLo } elseif ($x -le 31) { $c = $pipeHi } elseif ($x -ge 35) { $c = $pipeLo }
            $b.SetPixel($x, $y, $c)
        }
    }
}

function DrawHorzArm([System.Drawing.Bitmap]$b, [int]$x0, [int]$x1) {
    for ($x = $x0; $x -le $x1; $x++) {
        for ($y = 20; $y -le 43; $y++) { $b.SetPixel($x, $y, $trenchBg) }
        $b.SetPixel($x, 19, $trenchEdge); $b.SetPixel($x, 44, $trenchEdge)
        for ($y = 27; $y -le 36; $y++) {
            $c = $pipeBody
            if ($y -le 28) { $c = $pipeLo } elseif ($y -le 31) { $c = $pipeHi } elseif ($y -ge 35) { $c = $pipeLo }
            $b.SetPixel($x, $y, $c)
        }
    }
}

function DrawHiddenVertArm([System.Drawing.Bitmap]$b, [int]$y0, [int]$y1) {
    for ($y = $y0; $y -le $y1; $y++) {
        foreach ($x in 28..35) {
            $c = if ($x -eq 28 -or $x -eq 35) { $hEdge } else { $hLine }
            $b.SetPixel($x, $y, $c)
        }
    }
}

function DrawHiddenHorzArm([System.Drawing.Bitmap]$b, [int]$x0, [int]$x1) {
    for ($x = $x0; $x -le $x1; $x++) {
        foreach ($y in 28..35) {
            $c = if ($y -eq 28 -or $y -eq 35) { $hEdge } else { $hLine }
            $b.SetPixel($x, $y, $c)
        }
    }
}

function New-TrenchMask([int]$mask) {
    $b = New-Object System.Drawing.Bitmap(64, 64)
    $n = ($mask -band 1) -ne 0; $e = ($mask -band 2) -ne 0
    $s = ($mask -band 4) -ne 0; $w = ($mask -band 8) -ne 0
    if (-not ($n -or $e -or $s -or $w)) { $n = $true; $s = $true } # 孤段：按竖管显示
    # ⚠️ 每条臂只画「半格」（到中心线为止），否则 N/S 都会画满全高 = 退化成直管，
    #    L 形就画不出来（2026-09-07 采样验证发现并修复）。
    # 竖向臂：N = y 0..32（上半），S = y 32..63（下半）
    if ($n) { DrawVertArm $b 0 32 }
    if ($s) { DrawVertArm $b 32 63 }
    # 横向臂：W = x 0..32（左半），E = x 32..63（右半）
    if ($w) { DrawHorzArm $b 0 32 }
    if ($e) { DrawHorzArm $b 32 63 }

    # 中心 24x24 交叉块（保证多臂交汇处连续）
    for ($y = 20; $y -le 43; $y++) {
        for ($x = 20; $x -le 43; $x++) { $b.SetPixel($x, $y, $trenchBg) }
        $b.SetPixel(19, $y, $trenchEdge); $b.SetPixel(44, $y, $trenchEdge)
    }
    for ($x = 20; $x -le 43; $x++) {
        $b.SetPixel($x, 19, $trenchEdge); $b.SetPixel($x, 44, $trenchEdge)
    }
    for ($y = 27; $y -le 36; $y++) {
        for ($x = 27; $x -le 36; $x++) {
            $c = $pipeBody; if ($x -le 28) { $c = $pipeLo } elseif ($x -le 31) { $c = $pipeHi } elseif ($x -ge 35) { $c = $pipeLo }
            $b.SetPixel($x, $y, $c)
        }
    }
    return $b
}

for ($mask = 0; $mask -le 15; $mask++) {
    $name = "PC_Pipe_BodyM$mask.png"
    $path = Join-Path $Dir $name
    if (Test-Path $path) { Copy-Item $path (Join-Path $BackupDir $name) -Force }
    $img = New-TrenchMask $mask
    $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $img.Dispose()
    "saved $name"
}
# 清理上一版竖/横五联图（代码已不再引用；PC_Pipe_Body.png 保留作 def 默认图/图标）
foreach ($n in @('PC_Pipe_Body_north', 'PC_Pipe_Body_south', 'PC_Pipe_Body_east', 'PC_Pipe_Body_west')) {
    $old = Join-Path $Dir "$n.png"
    if (Test-Path $old) { Remove-Item $old -Force; "removed stale $n.png" }
}

Save-Variants 'PC_Pipe_In' $inCol
Save-Variants 'PC_Pipe_Out' $outCol

# ---- hidden logistics pipe: ONE single vertical buried line ----
# PM 2026-09-06 (final): no cross, no 4-direction Graphic_Multi variants.
# Just one vertical pipe sprite (PC_PipeH.png) used for both the world view and the
# menu icon. Because there is a single fixed graphic, rotatable = false (see Def).
$hLine = [System.Drawing.Color]::FromArgb(255, 56, 58, 62)     # 埋地细线（冷调）
$hEdge = [System.Drawing.Color]::FromArgb(255, 26, 27, 29)     # 线描边
$hNode = [System.Drawing.Color]::FromArgb(255, 72, 74, 78)     # 接线节点
$hNodeEdge = [System.Drawing.Color]::FromArgb(255, 38, 39, 42)

# 与可见管道同款「四邻掩码」：bit0=N bit1=E bit2=S bit3=W。
# 原因：单张竖图 + drawRotated 只能得到 0/90/180/270 的同一根线，**表达不了 L 拐角**；
# 改为掩码制后方向由连接自动决定（含 L/T/十字），QE 旋转随之变为冗余。
function New-HiddenMask([int]$mask) {
    $b = New-Object System.Drawing.Bitmap(64, 64)
    $n = ($mask -band 1) -ne 0; $e = ($mask -band 2) -ne 0
    $s = ($mask -band 4) -ne 0; $w = ($mask -band 8) -ne 0
    if (-not ($n -or $e -or $s -or $w)) { $n = $true; $s = $true } # 孤段：竖线
    # 同样：每条臂只画半格（N 上半 / S 下半 / W 左半 / E 右半）
    if ($n) { DrawHiddenVertArm $b 0 32 }
    if ($s) { DrawHiddenVertArm $b 32 63 }
    if ($w) { DrawHiddenHorzArm $b 0 32 }
    if ($e) { DrawHiddenHorzArm $b 32 63 }
    # junction node 12x12 at center
    for ($y = 26; $y -le 37; $y++) {
        for ($x = 26; $x -le 37; $x++) {
            $border = ($x -le 27 -or $x -ge 36 -or $y -le 27 -or $y -ge 36)
            $b.SetPixel($x, $y, $(if ($border) { $hNodeEdge } else { $hNode }))
        }
    }
    return $b
}

for ($mask = 0; $mask -le 15; $mask++) {
    $name = "PC_PipeH_M$mask.png"
    $path = Join-Path $Dir $name
    if (Test-Path $path) { Copy-Item $path (Join-Path $BackupDir $name) -Force }
    $img = New-HiddenMask $mask
    $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $img.Dispose()
    "saved $name"
}

# def 默认图 / 菜单图标：单张竖细线
$hp = Join-Path $Dir 'PC_PipeH.png'
if (Test-Path $hp) { Copy-Item $hp (Join-Path $BackupDir 'PC_PipeH.png') -Force }
$icon = New-HiddenMask 5   # mask 5 = N+S 全贯通竖线
$icon.Save($hp, [System.Drawing.Imaging.ImageFormat]::Png)
$icon.Dispose()
"saved PC_PipeH.png (default / menu icon, vertical)"

# 清理上一版遗留的四向变体（已有备份，且代码已不再引用）
foreach ($n in @('PC_PipeH_north', 'PC_PipeH_south', 'PC_PipeH_east', 'PC_PipeH_west')) {
    $old = Join-Path $Dir "$n.png"
    if (Test-Path $old) { Remove-Item $old -Force; "removed stale $n.png" }
}
Write-Host "DONE (backup: $BackupDir)"
