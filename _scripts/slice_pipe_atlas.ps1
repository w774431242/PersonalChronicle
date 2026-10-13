# Slice pipe texture atlas into 64x64 PNGs + verify arm-mask convention + old/new montage.
# Cell grid is hardcoded (measured frame interiors; frame lines wobble 1-3px between bands).
# Usage: slice_pipe_atlas.ps1 -Src <atlas.png> -OutDir <dir> -OldDir <existing textures> -Montage <out.png>
param(
    [Parameter(Mandatory=$true)][string]$Src,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [string]$OldDir = '',
    [string]$Montage = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

try {

$bmp = New-Object System.Drawing.Bitmap($Src)
$W = $bmp.Width; $H = $bmp.Height
Write-Host "Atlas: ${W}x${H}"

$rect = New-Object System.Drawing.Rectangle(0, 0, $W, $H)
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $H)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$stride = $data.Stride
$bmp.UnlockBits($data)

function IsContent([int]$x, [int]$y) {
    $i = $y * $stride + $x * 4
    return ((($bytes[$i] + $bytes[$i+1] + $bytes[$i+2]) -gt 45))
}

# --- row profile ---
$rowCount = New-Object int[] $H
for ($y = 0; $y -lt $H; $y++) {
    $c = 0
    for ($x = 0; $x -lt $W; $x++) { if (IsContent $x $y) { $c++ } }
    $rowCount[$y] = $c
}

# --- sprite bands (tall content bands) ---
$bands = New-Object System.Collections.ArrayList
$inBand = $false; $start = 0
for ($y = 0; $y -lt $H; $y++) {
    if ($rowCount[$y] -gt 3) {
        if (-not $inBand) { $inBand = $true; $start = $y }
    } else {
        if ($inBand) {
            $gap = $true
            for ($k = 1; $k -le 5; $k++) {
                if (($y + $k) -lt $H -and $rowCount[$y + $k] -gt 3) { $gap = $false; break }
            }
            if ($gap) { [void]$bands.Add(@($start, ($y - 1))); $inBand = $false }
        }
    }
}
if ($inBand) { [void]$bands.Add(@($start, ($H - 1))) }
$spriteBands = @($bands | Where-Object { ($_[1] - $_[0] + 1) -gt 90 })
Write-Host "Sprite bands: $($spriteBands.Count)"

# --- cells: measured frame interiors ---
$cells = New-Object System.Collections.ArrayList
foreach ($r in @(@(30,162),@(178,314),@(331,466),@(481,618),@(637,774),@(791,928),@(944,1078),@(1095,1225))) {
    [void]$cells.Add($r)
}
Write-Host "Cells: $($cells.Count)"
if ($cells.Count -ne 8 -or $spriteBands.Count -ne 5) { throw 'grid mismatch' }

function Save-Slice([int]$cx0, [int]$cx1, [int]$cy0, [int]$cy1, [string]$name) {
    $cw = $cx1 - $cx0 + 1; $ch = $cy1 - $cy0 + 1
    $out = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, 64, 64)), (New-Object System.Drawing.Rectangle($cx0, $cy0, $cw, $ch)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $path = Join-Path $OutDir "$name.png"
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    return $path
}

function Get-Arms([string]$path) {
    $im = New-Object System.Drawing.Bitmap($path)
    $n = $false; $e = $false; $s = $false; $w = $false
    for ($x = 19; $x -le 44; $x++) {
        if (($im.GetPixel($x, 1).GetBrightness() -gt 0.06) -or ($im.GetPixel($x, 2).GetBrightness() -gt 0.06)) { $n = $true }
        if (($im.GetPixel($x, 62).GetBrightness() -gt 0.06) -or ($im.GetPixel($x, 61).GetBrightness() -gt 0.06)) { $s = $true }
    }
    for ($y = 19; $y -le 44; $y++) {
        if (($im.GetPixel(1, $y).GetBrightness() -gt 0.06) -or ($im.GetPixel(2, $y).GetBrightness() -gt 0.06)) { $w = $true }
        if (($im.GetPixel(62, $y).GetBrightness() -gt 0.06) -or ($im.GetPixel(61, $y).GetBrightness() -gt 0.06)) { $e = $true }
    }
    $im.Dispose()
    $mask = 0
    if ($n) { $mask = $mask -bor 1 }
    if ($e) { $mask = $mask -bor 2 }
    if ($s) { $mask = $mask -bor 4 }
    if ($w) { $mask = $mask -bor 8 }
    return $mask
}

# center-row / center-col content extents (diameter proxy)
function Get-Diam([string]$path) {
    $im = New-Object System.Drawing.Bitmap($path)
    $rw = 0; $ch = 0
    for ($x = 0; $x -lt 64; $x++) { if ($im.GetPixel($x, 32).GetBrightness() -gt 0.06) { $rw++ } }
    for ($y = 0; $y -lt 64; $y++) { if ($im.GetPixel(32, $y).GetBrightness() -gt 0.06) { $ch++ } }
    $im.Dispose()
    return @($rw, $ch)
}

# arm detection at NATIVE atlas resolution inside the cell interior (no resize artifacts)
function Get-ArmsCell([int]$cx0, [int]$cx1, [int]$cy0, [int]$cy1) {
    $midX0 = $cx0 + [int](0.30 * ($cx1 - $cx0)); $midX1 = $cx0 + [int](0.70 * ($cx1 - $cx0))
    $midY0 = $cy0 + [int](0.30 * ($cy1 - $cy0)); $midY1 = $cy0 + [int](0.70 * ($cy1 - $cy0))
    $n = 0; $e = 0; $s = 0; $w = 0
    for ($x = $midX0; $x -le $midX1; $x++) {
        for ($k = 0; $k -lt 4; $k++) {
            if (IsContent $x ($cy0 + $k)) { $n++ }
            if (IsContent $x ($cy1 - $k)) { $s++ }
        }
    }
    for ($y = $midY0; $y -le $midY1; $y++) {
        for ($k = 0; $k -lt 4; $k++) {
            if (IsContent ($cx0 + $k) $y) { $w++ }
            if (IsContent ($cx1 - $k) $y) { $e++ }
        }
    }
    $mask = 0
    if ($n -ge 6) { $mask = $mask -bor 1 }
    if ($e -ge 6) { $mask = $mask -bor 2 }
    if ($s -ge 6) { $mask = $mask -bor 4 }
    if ($w -ge 6) { $mask = $mask -bor 8 }
    return $mask
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$names = @()
for ($i = 0; $i -lt 16; $i++) { $names += "PC_Pipe_BodyM$i" }
for ($i = 0; $i -lt 16; $i++) { $names += "PC_PipeH_M$i" }
$names += @('PC_Pipe_In_N','PC_Pipe_In_E','PC_Pipe_In_S','PC_Pipe_In_W','PC_Pipe_Out_N','PC_Pipe_Out_E','PC_Pipe_Out_S','PC_Pipe_Out_W')

$idx = 0
$mismatches = 0
$report = New-Object System.Collections.ArrayList
foreach ($sb in $spriteBands) {
    $y0 = $sb[0]; $y1 = $sb[1]
    # frame horizontal lines -> interior
    $frameRows = New-Object System.Collections.ArrayList
    $inR = $false; $rs = 0
    for ($y = $y0; $y -le $y1; $y++) {
        if ($rowCount[$y] -gt (0.55 * $W)) {
            if (-not $inR) { $inR = $true; $rs = $y }
        } else {
            if ($inR) { [void]$frameRows.Add(@($rs, ($y - 1))); $inR = $false }
        }
    }
    if ($inR) { [void]$frameRows.Add(@($rs, $y1)) }
    $cy0 = $y0; $cy1 = $y1
    if ($frameRows.Count -ge 2) {
        $cy0 = $frameRows[0][1] + 1
        $cy1 = $frameRows[$frameRows.Count - 1][0] - 1
    }
    foreach ($cell in $cells) {
        $name = $names[$idx]
        $path = Save-Slice $cell[0] $cell[1] $cy0 $cy1 $name
        $mask = Get-ArmsCell $cell[0] $cell[1] $cy0 $cy1
        $diam = Get-Diam $path
        $expected = -1
        if ($name -match 'M(\d+)$') { $expected = [int]$Matches[1] }
        $ok = 'n/a'
        if ($expected -ge 0) {
            if ($mask -eq $expected) { $ok = 'OK' } else { $ok = "MISMATCH exp=$expected"; $mismatches++ }
        }
        $line = ("{0,-18} arms={1,-2} diamW@32={2,-2} diamH@32={3,-2} {4}" -f $name, $mask, $diam[0], $diam[1], $ok)
        Write-Host "  $line"
        [void]$report.Add($line)
        $idx++
    }
}
if ($idx -ne $names.Count) { Write-Host "!! sliced $idx of $($names.Count)" }
Write-Host ""
Write-Host "Mask mismatches: $mismatches"

# --- montage: old | new, 4x upscale ---
if ($Montage -ne '' -and $OldDir -ne '' -and (Test-Path $OldDir)) {
    $scale = 4; $cell = 64 * $scale; $labelH = 18
    $cols = 8; $rows = [Math]::Ceiling($names.Count / $cols)
    $mw = $cols * ($cell * 2 + 12) + 12
    $mh = $rows * ($cell + $labelH + 8) + 12
    $m = New-Object System.Drawing.Bitmap($mw, $mh)
    $g = [System.Drawing.Graphics]::FromImage($m)
    $g.Clear([System.Drawing.Color]::FromArgb(40, 40, 48))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $font = New-Object System.Drawing.Font('Consolas', 8)
    $brushW = [System.Drawing.Brushes]::White
    $brushY = [System.Drawing.Brushes]::Yellow
    for ($i = 0; $i -lt $names.Count; $i++) {
        $cx = 12 + ($i % $cols) * ($cell * 2 + 12)
        $cy = 12 + [Math]::Floor($i / $cols) * ($cell + $labelH + 8)
        $oldPath = Join-Path $OldDir "$($names[$i]).png"
        if (Test-Path $oldPath) {
            $oi = [System.Drawing.Image]::FromFile($oldPath)
            $g.DrawImage($oi, $cx, $cy, $cell, $cell)
            $oi.Dispose()
        }
        $newPath = Join-Path $OutDir "$($names[$i]).png"
        if (Test-Path $newPath) {
            $ni = [System.Drawing.Image]::FromFile($newPath)
            $g.DrawImage($ni, ($cx + $cell + 4), $cy, $cell, $cell)
            $ni.Dispose()
        }
        $g.DrawString($names[$i], $font, $brushY, $cx, ($cy + $cell + 1))
    }
    $g.Dispose()
    $m.Save($Montage, [System.Drawing.Imaging.ImageFormat]::Png)
    $m.Dispose()
    Write-Host "Montage -> $Montage"
}

$bmp.Dispose()
Write-Host "Done -> $OutDir"

} catch {
    Write-Host "EXC: $($_.Exception.Message)"
    Write-Host $_.InvocationInfo.PositionMessage
    throw
}
