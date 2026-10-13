# Slice pipe atlas v2: square-padded crop (preserve aspect -> no vertical squash).
# Family-wide square size so pipe diameter stays consistent inside a family.
# Usage: slice_pipe_atlas_v2.ps1 -Src <atlas.png> -OutDir <dir>
param(
    [Parameter(Mandatory=$true)][string]$Src,
    [Parameter(Mandatory=$true)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

try {

$bmp = New-Object System.Drawing.Bitmap($Src)
$W = $bmp.Width; $H = $bmp.Height
$rect = New-Object System.Drawing.Rectangle(0, 0, $W, $H)
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $H)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$stride = $data.Stride
$bmp.UnlockBits($data)

function IsContent([int]$x, [int]$y) {
    $i = $y * $stride + $x * 4
    return ((($bytes[$i] + $bytes[$i + 1] + $bytes[$i + 2]) -gt 45))
}

$rowCount = New-Object int[] $H
for ($y = 0; $y -lt $H; $y++) {
    $c = 0
    for ($x = 0; $x -lt $W; $x++) { if (IsContent $x $y) { $c++ } }
    $rowCount[$y] = $c
}

$bands = New-Object System.Collections.ArrayList
$inBand = $false; $start = 0
for ($y = 0; $y -lt $H; $y++) {
    if ($rowCount[$y] -gt 3) {
        if (-not $inBand) { $inBand = $true; $start = $y }
    } else {
        if ($inBand) {
            $gap = $true
            for ($k = 1; $k -le 5; $k++) { if (($y + $k) -lt $H -and $rowCount[$y + $k] -gt 3) { $gap = $false; break } }
            if ($gap) { [void]$bands.Add(@($start, ($y - 1))); $inBand = $false }
        }
    }
}
if ($inBand) { [void]$bands.Add(@($start, ($H - 1))) }
$spriteBands = @($bands | Where-Object { ($_[1] - $_[0] + 1) -gt 90 })

$cells = New-Object System.Collections.ArrayList
foreach ($r in @(@(30,162),@(178,314),@(331,466),@(481,618),@(637,774),@(791,928),@(944,1078),@(1095,1225))) { [void]$cells.Add($r) }
if ($cells.Count -ne 8 -or $spriteBands.Count -ne 5) { throw 'grid mismatch' }

# band interior: strip a fixed 3px frame on each side (frame-row detection is
# unreliable because a row of horizontal pipes also fills the full width)
$interiors = New-Object System.Collections.ArrayList
foreach ($sb in $spriteBands) {
    $cy0 = $sb[0] + 3; $cy1 = $sb[1] - 3
    [void]$interiors.Add(@($cy0, $cy1))
}
Write-Host 'Band interiors:'
for ($i = 0; $i -lt $interiors.Count; $i++) { Write-Host ("  band{0}: y{1}-{2} h={3}" -f $i, $interiors[$i][0], $interiors[$i][1], ($interiors[$i][1] - $interiors[$i][0] + 1)) }

# families: 0-1 = visible, 2-3 = hidden, 4 = ports
$familyOf = @{ 0 = 'vis'; 1 = 'vis'; 2 = 'hid'; 3 = 'hid'; 4 = 'port' }
# one uniform square size per family: must fit inside EVERY cell of that family
# (no bleed from neighbouring cells / label strips), so take the MINIMUM.
$famS = @{}
foreach ($bi in 0..4) {
    $f = $familyOf[$bi]
    $h = $interiors[$bi][1] - $interiors[$bi][0] + 1
    foreach ($c in $cells) {
        $w = $c[1] - $c[0] + 1
        $m = [Math]::Min($w, $h)
        if (-not $famS.ContainsKey($f) -or $m -lt $famS[$f]) { $famS[$f] = $m }
    }
}
Write-Host 'Family square size:'
foreach ($k in $famS.Keys) { Write-Host ("  {0} = {1}" -f $k, $famS[$k]) }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Save-Square([int]$cx0, [int]$cx1, [int]$cy0, [int]$cy1, [int]$S, [string]$name) {
    $cw = $cx1 - $cx0 + 1; $ch = $cy1 - $cy0 + 1
    $cx = [int](($cx0 + $cx1) / 2); $cy = [int](($cy0 + $cy1) / 2)
    # crop square S centered on cell, clamp inside atlas
    $sx = $cx - [int]($S / 2); $sy = $cy - [int]($S / 2)
    if ($sx -lt 0) { $sx = 0 }
    if ($sy -lt 0) { $sy = 0 }
    if (($sx + $S) -gt $W) { $sx = $W - $S }
    if (($sy + $S) -gt $H) { $sy = $H - $S }
    $out = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, 64, 64)), (New-Object System.Drawing.Rectangle($sx, $sy, $S, $S)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $path = Join-Path $OutDir "$name.png"
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    return $path
}

$names = @()
for ($i = 0; $i -lt 16; $i++) { $names += "PC_Pipe_BodyM$i" }
for ($i = 0; $i -lt 16; $i++) { $names += "PC_PipeH_M$i" }
$names += @('PC_Pipe_In_N','PC_Pipe_In_E','PC_Pipe_In_S','PC_Pipe_In_W','PC_Pipe_Out_N','PC_Pipe_Out_E','PC_Pipe_Out_S','PC_Pipe_Out_W')

# --- measure arms / diameters on the 64x64 result ---
function Measure-Tile([string]$path) {
    $im = New-Object System.Drawing.Bitmap($path)
    $b = New-Object int[] 4096
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            $p = $im.GetPixel($x, $y)
            if ($p.A -gt 90) { $b[($y * 64 + $x)] = 1 }
        }
    }
    $im.Dispose()
    # arms: content in rows 0-3 / cols 60-63 within central span
    $mask = 0
    $n = 0; $s = 0; $e = 0; $w = 0
    for ($x = 22; $x -le 41; $x++) { for ($y = 0; $y -le 3; $y++) { $n += $b[($y * 64 + $x)] } for ($y = 60; $y -le 63; $y++) { $s += $b[($y * 64 + $x)] } }
    for ($y = 22; $y -le 41; $y++) { for ($x = 60; $x -le 63; $x++) { $e += $b[($y * 64 + $x)] } for ($x = 0; $x -le 3; $x++) { $w += $b[($y * 64 + $x)] } }
    if ($n -ge 4) { $mask = $mask -bor 1 }
    if ($e -ge 4) { $mask = $mask -bor 2 }
    if ($s -ge 4) { $mask = $mask -bor 4 }
    if ($w -ge 4) { $mask = $mask -bor 8 }
    # diameter: longest run at row 6 (vertical pipe width) and col 57 (horizontal pipe height)
    $run = 0; $best = 0
    for ($x = 0; $x -lt 64; $x++) { if ($b[(6 * 64 + $x)] -eq 1) { $run++; if ($run -gt $best) { $best = $run } } else { $run = 0 } }
    $dv = $best
    $run = 0; $best = 0
    for ($y = 0; $y -lt 64; $y++) { if ($b[($y * 64 + 57)] -eq 1) { $run++; if ($run -gt $best) { $best = $run } } else { $run = 0 } }
    $dh = $best
    return @($mask, $dv, $dh)
}

$idx = 0
for ($bi = 0; $bi -lt 5; $bi++) {
    $f = $familyOf[$bi]; $S = $famS[$f]
    $cy0 = $interiors[$bi][0]; $cy1 = $interiors[$bi][1]
    foreach ($c in $cells) {
        $p = Save-Square $c[0] $c[1] $cy0 $cy1 $S $names[$idx]
        $m = Measure-Tile $p
        $exp = -1
        if ($names[$idx] -match 'M(\d+)$') { $exp = [int]$Matches[1] }
        $ok = 'n/a'
        if ($exp -ge 0) { if ($m[0] -eq $exp) { $ok = 'OK' } else { $ok = "exp=$exp" } }
        Write-Host ("{0,-18} arms={1,-2} diaV={2,-2} diaH={3,-2} {4}" -f $names[$idx], $m[0], $m[1], $m[2], $ok)
        $idx++
    }
}
Write-Host "Sliced $idx -> $OutDir"
$bmp.Dispose()

} catch {
    Write-Host "EXC: $($_.Exception.Message)"
    Write-Host $_.InvocationInfo.PositionMessage
    throw
}
