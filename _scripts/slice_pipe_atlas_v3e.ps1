# PRODUCTION pipeline for atlas 03_37_13.png.
# Human-verified enum->mask mapping (contact sheet + closeups + seam counts):
#   body: [0]M0 [1]M1 [2]M2 [3]M4 [4]M5 [5]M10 [6]M6 [7]M12 [10]M15  (8,9,11-15 dup)
#   hid : [0]M0 [1]M1 [2]M10 [3]M4 [4]M5 [6]M6 [7]M14 [8]M12 [9]M8 [10]M15 (5,11-15 dup)
# Missing kinds synthesized: elbows via mirror, T-pieces = straight + half-arm,
#   W/E endcap via flipX. Arms centered on tile midline (AI cells are off-center),
#   edges normalized (clone line 2 -> 0,1 / 61 -> 62,63). Final audit included.
# Usage: slice_pipe_atlas_v3e.ps1 -Src <atlas.png> -OutDir <dir>
param(
    [Parameter(Mandatory=$true)][string]$Src,
    [Parameter(Mandatory=$true)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap($Src)
$W = $bmp.Width; $H = $bmp.Height

$cols = @(
    @(22, 162), @(181, 318), @(335, 472), @(491, 628),
    @(646, 784), @(802, 940), @(958, 1092), @(1110, 1242)
)
$rows = @(
    @{ y0 = 170; y1 = 334 },
    @{ y0 = 439; y1 = 605 },
    @{ y0 = 718; y1 = 882 },
    @{ y0 = 986; y1 = 1123 }
)
$INSET = 4
$KEY_MAX = 195
$KEY_SAT = 40

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-Tile([int]$cell, [int]$band) {
    $cx0 = $cols[$cell][0] + $INSET; $cx1 = $cols[$cell][1] - $INSET
    $cy0 = $rows[$band].y0 + $INSET; $cy1 = $rows[$band].y1 - $INSET
    $cw = $cx1 - $cx0 + 1; $ch = $cy1 - $cy0 + 1
    $S = [Math]::Min($cw, $ch)
    $cx = [int](($cx0 + $cx1) / 2); $cy = [int](($cy0 + $cy1) / 2)
    $sx = $cx - [int]($S / 2); $sy = $cy - [int]($S / 2)
    if ($sx -lt 0) { $sx = 0 }
    if ($sy -lt 0) { $sy = 0 }
    if (($sx + $S) -gt $W) { $sx = $W - $S }
    if (($sy + $S) -gt $H) { $sy = $H - $S }

    $crop = $bmp.Clone((New-Object System.Drawing.Rectangle($sx, $sy, $S, $S)), $bmp.PixelFormat)
    $rect = New-Object System.Drawing.Rectangle(0, 0, $S, $S)
    $data = $crop.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $S)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $stride = $data.Stride
    $bg = New-Object byte[] ($S * $S)
    $q = New-Object 'System.Collections.Generic.Queue[int]'
    for ($x = 0; $x -lt $S; $x++) { $q.Enqueue($x); $q.Enqueue(0); $q.Enqueue($x); $q.Enqueue($S - 1) }
    for ($y = 0; $y -lt $S; $y++) { $q.Enqueue(0); $q.Enqueue($y); $q.Enqueue($S - 1); $q.Enqueue($y) }
    while ($q.Count -gt 0) {
        $x = $q.Dequeue(); $y = $q.Dequeue()
        if ($x -lt 0 -or $y -lt 0 -or $x -ge $S -or $y -ge $S) { continue }
        $pi = $y * $S + $x
        if ($bg[$pi] -eq 1) { continue }
        $i = $y * $stride + $x * 4
        if ($bytes[$i+3] -lt 40) { $bg[$pi] = 1; continue }
        $r = $bytes[$i]; $g = $bytes[$i+1]; $b = $bytes[$i+2]
        $mx = $r; if ($g -gt $mx) { $mx = $g }; if ($b -gt $mx) { $mx = $b }
        $mn = $r; if ($g -lt $mn) { $mn = $g }; if ($b -lt $mn) { $mn = $b }
        if (($mx -ge $KEY_MAX) -and (($mx - $mn) -le $KEY_SAT)) {
            $bg[$pi] = 1
            $q.Enqueue($x + 1); $q.Enqueue($y)
            $q.Enqueue($x - 1); $q.Enqueue($y)
            $q.Enqueue($x); $q.Enqueue($y + 1)
            $q.Enqueue($x); $q.Enqueue($y - 1)
        }
    }
    for ($y = 0; $y -lt $S; $y++) {
        for ($x = 0; $x -lt $S; $x++) {
            if ($bg[($y * $S) + $x] -eq 1) { $bytes[(($y * $stride) + ($x * 4)) + 3] = 0 }
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $crop.UnlockBits($data)
    $out = New-Object System.Drawing.Bitmap(64, 64)
    $g2 = [System.Drawing.Graphics]::FromImage($out)
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $dst = New-Object System.Drawing.Rectangle(0, 0, 64, 64)
    $srcR = New-Object System.Drawing.Rectangle(0, 0, $S, $S)
    $g2.DrawImage($crop, $dst, $srcR, [System.Drawing.GraphicsUnit]::Pixel)
    $g2.Dispose()
    $crop.Dispose()
    return $out
}

function Copy-Bitmap([System.Drawing.Bitmap]$src) {
    return $src.Clone((New-Object System.Drawing.Rectangle(0, 0, 64, 64)), $src.PixelFormat)
}
function Clear-Region([System.Drawing.Bitmap]$img, [int]$x0, [int]$y0, [int]$x1, [int]$y1) {
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = $x0; $x -le $x1; $x++) { $img.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0,0,0,0)) }
    }
}
function Overlay([System.Drawing.Bitmap]$dst, [System.Drawing.Bitmap]$src) {
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            $p = $src.GetPixel($x, $y)
            if ($p.A -gt 12) { $dst.SetPixel($x, $y, $p) }
        }
    }
}

# measured mouth center of one arm at the seam (returns -100 if absent)
function Get-MouthCenter([System.Drawing.Bitmap]$im, [string]$side) {
    $min = 999; $max = -999
    for ($j = 20; $j -le 43; $j++) {
        $hit = $false
        for ($k = 0; $k -le 1; $k++) {
            if ($side -eq 'N') { if ($im.GetPixel($j, $k).A -gt 20) { $hit = $true } }
            elseif ($side -eq 'S') { if ($im.GetPixel($j, 63 - $k).A -gt 20) { $hit = $true } }
            elseif ($side -eq 'W') { if ($im.GetPixel($k, $j).A -gt 20) { $hit = $true } }
            else { if ($im.GetPixel(63 - $k, $j).A -gt 20) { $hit = $true } }
        }
        if ($hit) { if ($j -lt $min) { $min = $j }; if ($j -gt $max) { $max = $j } }
    }
    if ($max -lt 0) { return -100.0 }
    return (($min + $max) / 2.0)
}

# shift whole image so present mouths center on the tile midline
function Center-Arms([System.Drawing.Bitmap]$im) {
    $dxs = New-Object System.Collections.ArrayList
    $dys = New-Object System.Collections.ArrayList
    $c = Get-MouthCenter $im 'N'; if ($c -gt -50) { [void]$dxs.Add(31.5 - $c) }
    $c = Get-MouthCenter $im 'S'; if ($c -gt -50) { [void]$dxs.Add(31.5 - $c) }
    $c = Get-MouthCenter $im 'W'; if ($c -gt -50) { [void]$dys.Add(31.5 - $c) }
    $c = Get-MouthCenter $im 'E'; if ($c -gt -50) { [void]$dys.Add(31.5 - $c) }
    if ($dxs.Count -eq 0 -and $dys.Count -eq 0) { return $im }
    $dx = 0.0; $dy = 0.0
    foreach ($v in $dxs) { $dx += $v }
    foreach ($v in $dys) { $dy += $v }
    if ($dxs.Count -gt 0) { $dx = $dx / $dxs.Count }
    if ($dys.Count -gt 0) { $dy = $dy / $dys.Count }
    $idx = [int][Math]::Round($dx); $idy = [int][Math]::Round($dy)
    if ($idx -eq 0 -and $idy -eq 0) { return $im }
    $shifted = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($shifted)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $dstR = New-Object System.Drawing.Rectangle($idx, $idy, 64, 64)
    $g.DrawImage($im, $dstR, (New-Object System.Drawing.Rectangle(0, 0, 64, 64)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $im.Dispose()
    return $shifted
}

function Normalize-Edges([System.Drawing.Bitmap]$img) {
    for ($j = 0; $j -lt 64; $j++) {
        $t = $img.GetPixel($j, 2)
        if ($t.A -gt 30) { $img.SetPixel($j, 0, $t); $img.SetPixel($j, 1, $t) }
        $t = $img.GetPixel($j, 61)
        if ($t.A -gt 30) { $img.SetPixel($j, 62, $t); $img.SetPixel($j, 63, $t) }
        $t = $img.GetPixel(2, $j)
        if ($t.A -gt 30) { $img.SetPixel(0, $j, $t); $img.SetPixel(1, $j, $t) }
        $t = $img.GetPixel(61, $j)
        if ($t.A -gt 30) { $img.SetPixel(62, $j, $t); $img.SetPixel(63, $j, $t) }
    }
}

# mouth mask at the physical seam BEFORE shifting (true border-crossing arms)
function Get-MouthMask([System.Drawing.Bitmap]$im) {
    $mask = 0
    $n = 0; $s2 = 0; $e = 0; $w = 0
    for ($x = 24; $x -le 39; $x++) {
        for ($y = 0; $y -le 1; $y++) { if ($im.GetPixel($x, $y).A -gt 20) { $n++ } }
        for ($y = 62; $y -le 63; $y++) { if ($im.GetPixel($x, $y).A -gt 20) { $s2++ } }
    }
    for ($y = 24; $y -le 39; $y++) {
        for ($x = 62; $x -le 63; $x++) { if ($im.GetPixel($x, $y).A -gt 20) { $e++ } }
        for ($x = 0; $x -le 1; $x++) { if ($im.GetPixel($x, $y).A -gt 20) { $w++ } }
    }
    if ($n -ge 6) { $mask = $mask -bor 1 }
    if ($e -ge 6) { $mask = $mask -bor 2 }
    if ($s2 -ge 6) { $mask = $mask -bor 4 }
    if ($w -ge 6) { $mask = $mask -bor 8 }
    return $mask
}

# after centering, a TRUE arm mouth may sit a few px inside the border; extend the
# outermost content line (within the center band) out to the border rows/cols,
# but only for directions that were real border-crossing arms before the shift.
function Extend-Edges([System.Drawing.Bitmap]$img, [int]$mask) {
    if ($mask -band 1) {
        for ($y = 0; $y -le 10; $y++) {
            $hit = $false
            for ($j = 24; $j -le 39; $j++) { if ($img.GetPixel($j, $y).A -gt 20) { $hit = $true; break } }
            if ($hit) { if ($y -gt 0) { for ($j = 0; $j -lt 64; $j++) { $p = $img.GetPixel($j, $y); $img.SetPixel($j, 0, $p); $img.SetPixel($j, 1, $p) } }; break }
        }
    }
    if ($mask -band 4) {
        for ($y = 63; $y -ge 53; $y--) {
            $hit = $false
            for ($j = 24; $j -le 39; $j++) { if ($img.GetPixel($j, $y).A -gt 20) { $hit = $true; break } }
            if ($hit) { if ($y -lt 63) { for ($j = 0; $j -lt 64; $j++) { $p = $img.GetPixel($j, $y); $img.SetPixel($j, 62, $p); $img.SetPixel($j, 63, $p) } }; break }
        }
    }
    if ($mask -band 8) {
        for ($x = 0; $x -le 10; $x++) {
            $hit = $false
            for ($j = 24; $j -le 39; $j++) { if ($img.GetPixel($x, $j).A -gt 20) { $hit = $true; break } }
            if ($hit) { if ($x -gt 0) { for ($j = 0; $j -lt 64; $j++) { $p = $img.GetPixel($x, $j); $img.SetPixel(0, $j, $p); $img.SetPixel(1, $j, $p) } }; break }
        }
    }
    if ($mask -band 2) {
        for ($x = 63; $x -ge 53; $x--) {
            $hit = $false
            for ($j = 24; $j -le 39; $j++) { if ($img.GetPixel($x, $j).A -gt 20) { $hit = $true; break } }
            if ($hit) { if ($x -lt 63) { for ($j = 0; $j -lt 64; $j++) { $p = $img.GetPixel($x, $j); $img.SetPixel(62, $j, $p); $img.SetPixel(63, $j, $p) } }; break }
        }
    }
}

# ---------- slice + human-verified mapping ----------
$prefixes = @('PC_Pipe_BodyM', 'PC_PipeH_M')
$mapBody = @{ 0 = 0; 1 = 1; 2 = 2; 3 = 4; 4 = 5; 5 = 10; 6 = 6; 7 = 12; 10 = 15 }
$mapHid  = @{ 0 = 0; 1 = 1; 2 = 10; 3 = 4; 4 = 5; 6 = 6; 7 = 14; 8 = 12; 9 = 8; 10 = 15 }

for ($fam = 0; $fam -lt 2; $fam++) {
    $pre = $prefixes[$fam]
    $map = if ($fam -eq 0) { $mapBody } else { $mapHid }

    # enum tiles (band pair)
    $e = @{}
    foreach ($k in $map.Keys) {
        $band = [Math]::Floor($k / 8); $cell = $k % 8
        $e[$k] = Get-Tile $cell ((($fam * 2) + $band))
    }

    $t = @{}
    foreach ($k in $map.Keys) { $t[$map[$k]] = $e[$k] }

    # --- synthesize missing ---
    $sv = $t[5]; $sh = $t[10]
    if (-not $t.ContainsKey(8)) { $x = Copy-Bitmap $t[2]; $x.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX); $t[8] = $x; Write-Host ("{0}: M8 = flipX(M2)" -f $pre) }
    if (-not $t.ContainsKey(2)) { $x = Copy-Bitmap $t[8]; $x.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX); $t[2] = $x; Write-Host ("{0}: M2 = flipX(M8)" -f $pre) }
    if (-not $t.ContainsKey(3)) { $x = Copy-Bitmap $t[6]; $x.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY); $t[3] = $x; Write-Host ("{0}: M3 = flipY(M6)" -f $pre) }
    if (-not $t.ContainsKey(9)) { $x = Copy-Bitmap $t[12]; $x.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY); $t[9] = $x; Write-Host ("{0}: M9 = flipY(M12)" -f $pre) }
    if (-not $t.ContainsKey(7)) { $x = Copy-Bitmap $sv; $a = Copy-Bitmap $sh; Clear-Region $a 0 0 31 63; Overlay $x $a; $t[7] = $x; Write-Host ("{0}: M7 = NS + east arm" -f $pre) }
    if (-not $t.ContainsKey(11)) { $x = Copy-Bitmap $sh; $a = Copy-Bitmap $sv; Clear-Region $a 0 32 63 63; Overlay $x $a; $t[11] = $x; Write-Host ("{0}: M11 = EW + north arm" -f $pre) }
    if (-not $t.ContainsKey(13)) { $x = Copy-Bitmap $sv; $a = Copy-Bitmap $sh; Clear-Region $a 32 0 63 63; Overlay $x $a; $t[13] = $x; Write-Host ("{0}: M13 = NS + west arm" -f $pre) }
    if (-not $t.ContainsKey(14)) { $x = Copy-Bitmap $sh; $a = Copy-Bitmap $sv; Clear-Region $a 0 0 63 31; Overlay $x $a; $t[14] = $x; Write-Host ("{0}: M14 = EW + south arm" -f $pre) }
    if (-not $t.ContainsKey(15)) { $x = Copy-Bitmap $sv; Overlay $x $sh; $t[15] = $x; Write-Host ("{0}: M15 = NS + EW" -f $pre) }

    # --- M0 isolation: shrink horizontal content to x[6..57] ---
    $m0 = $t[0]
    $minx = 64; $maxx = -1
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            if ($m0.GetPixel($x, $y).A -gt 40) { if ($x -lt $minx) { $minx = $x }; if ($x -gt $maxx) { $maxx = $x } }
        }
    }
    if ($maxx -ge 0 -and ($minx -lt 6 -or $maxx -gt 57)) {
        $shrunk = New-Object System.Drawing.Bitmap(64, 64)
        $g3 = [System.Drawing.Graphics]::FromImage($shrunk)
        $g3.Clear([System.Drawing.Color]::Transparent)
        $g3.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g3.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dst2 = New-Object System.Drawing.Rectangle(6, 0, 52, 64)
        $src2 = New-Object System.Drawing.Rectangle($minx, 0, ($maxx - $minx + 1), 64)
        $g3.DrawImage($m0, $dst2, $src2, [System.Drawing.GraphicsUnit]::Pixel)
        $g3.Dispose()
        $m0.Dispose()
        $t[0] = $shrunk
    }

    # --- center arms + extend true arms only + normalize + save ---
    foreach ($m in (0..15)) {
        $img = $t[$m]
        if ($m -ne 0) {
            $mm = Get-MouthMask $img
            $img = Center-Arms $img
            Extend-Edges $img $mm
        }
        Normalize-Edges $img
        $img.Save((Join-Path $OutDir ($pre + $m + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    }
}

# ---------- final audit ----------
function Measure-Tile([string]$path) {
    $im = New-Object System.Drawing.Bitmap($path)
    $b = New-Object int[] 4096
    for ($y = 0; $y -lt 64; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            if ($im.GetPixel($x, $y).A -gt 90) { $b[($y * 64 + $x)] = 1 }
        }
    }
    $im.Dispose()
    $mask = 0
    $n = 0; $s2 = 0; $e = 0; $w = 0
    for ($x = 22; $x -le 41; $x++) {
        for ($y = 0; $y -le 2; $y++) { $n += $b[($y * 64 + $x)] }
        for ($y = 61; $y -le 63; $y++) { $s2 += $b[($y * 64 + $x)] }
    }
    for ($y = 22; $y -le 41; $y++) {
        for ($x = 61; $x -le 63; $x++) { $e += $b[($y * 64 + $x)] }
        for ($x = 0; $x -le 2; $x++) { $w += $b[($y * 64 + $x)] }
    }
    if ($n -ge 3) { $mask = $mask -bor 1 }
    if ($e -ge 3) { $mask = $mask -bor 2 }
    if ($s2 -ge 3) { $mask = $mask -bor 4 }
    if ($w -ge 3) { $mask = $mask -bor 8 }
    $run = 0; $best = 0
    for ($x = 0; $x -lt 64; $x++) { if ($b[(6 * 64 + $x)] -eq 1) { $run++; if ($run -gt $best) { $best = $run } } else { $run = 0 } }
    $dv = $best
    $run = 0; $best = 0
    for ($y = 0; $y -lt 64; $y++) { if ($b[($y * 64 + 57)] -eq 1) { $run++; if ($run -gt $best) { $best = $run } } else { $run = 0 } }
    $dh = $best
    $reach = 0
    for ($x = 22; $x -le 41; $x++) { if ($b[$x] -eq 1) { $reach = $reach -bor 1; break } }
    for ($y = 22; $y -le 41; $y++) { if ($b[($y * 64 + 63)] -eq 1) { $reach = $reach -bor 2; break } }
    for ($x = 22; $x -le 41; $x++) { if ($b[(63 * 64 + $x)] -eq 1) { $reach = $reach -bor 4; break } }
    for ($y = 22; $y -le 41; $y++) { if ($b[($y * 64)] -eq 1) { $reach = $reach -bor 8; break } }
    return @($mask, $dv, $dh, $reach)
}

Write-Host ""
Write-Host "=== final audit ==="
$mismatch = 0
$files = Get-ChildItem $OutDir -Filter '*.png' | Sort-Object Name
foreach ($f in $files) {
    $m = Measure-Tile $f.FullName
    $exp = [int]($f.BaseName -replace '^.*M', '')
    $ok = 'OK'
    if ($m[0] -ne $exp) { $ok = 'exp=' + $exp; $mismatch++ }
    $reachFlag = ''
    if ($exp -ne 0) {
        $missingReach = $exp -band ((-bnot $m[3]) -band 15)
        if ($missingReach -ne 0) { $reachFlag = ' NOT-REACH(' + $missingReach + ')' }
    }
    Write-Host ('  {0,-18} arms={1,-2} diaV={2,-2} diaH={3,-3} {4}{5}' -f $f.BaseName, $m[0], $m[1], $m[2], $ok, $reachFlag)
}
Write-Host ""
Write-Host ("files={0}  mask mismatches={1}" -f $files.Count, $mismatch)
$bmp.Dispose()
Write-Host ("Done -> " + $OutDir)
