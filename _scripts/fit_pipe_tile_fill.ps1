# Normalize PC_Pipe_* sprites for seamless tiling.
# Problems found by pixel audit (2026-09-06):
#   1) edge rows/cols are semi-transparent (maxA~108-134) -> hairline gap between segments;
#   2) diameters differ per variant (Body 34px vs In/Out 24px vertical) -> joints look broken.
# Fix per flow-axis family (v: *_north/_south + Body.png/Body_north/Body_south;
#                           h: default, *_east, *_west):
#   - straighten every row/col to the family's max content width (uniform diameter);
#   - edge-extend the outermost row/col (clone row 1 -> 0, row 62 -> 63) to kill the hairline.
param(
    [string]$Dir = (Join-Path $PSScriptRoot '..\Textures\Things\Building\Automation'),
    [string]$BackupDir = (Join-Path $PSScriptRoot ('..\_backups\pipe_tex_norm_' + (Get-Date -Format 'yyyyMMdd_HHmmss')))
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Get-FlowAxis([string]$name) {
    if ($name -match '_(north|south)$') { return 'v' }
    if ($name -match '_(east|west)$')   { return 'h' }
    if ($name -like 'PC_Pipe_Body*')    { return 'v' }
    return 'h'
}
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$files = Get-ChildItem $Dir -Filter 'PC_Pipe_*.png'
if ($files.Count -eq 0) { throw "no PC_Pipe_*.png under $Dir" }

# pass 1: family max content width
$famMax = @{ v = 0; h = 0 }
foreach ($f in $files) {
    $axis = Get-FlowAxis $f.BaseName
    $bmp = [System.Drawing.Bitmap]::FromFile($f.FullName)
    for ($i = 0; $i -lt 64; $i++) {
        $min = 64; $max = -1
        for ($j = 0; $j -lt 64; $j++) {
            $a = if ($axis -eq 'v') { $bmp.GetPixel($j, $i).A } else { $bmp.GetPixel($i, $j).A }
            if ($a -ge 10) { if ($j -lt $min) { $min = $j }; if ($j -gt $max) { $max = $j } }
        }
        if ($max -ge 0) {
            $w = $max - $min + 1
            if ($w -gt $famMax[$axis]) { $famMax[$axis] = $w }
        }
    }
    $bmp.Dispose()
}
Write-Host ("family target: v={0}px h={1}px" -f $famMax['v'], $famMax['h'])

# pass 2: straighten + edge-extend
foreach ($f in $files) {
    $axis = Get-FlowAxis $f.BaseName
    $target = $famMax[$axis]
    $src = [System.Drawing.Bitmap]::FromFile($f.FullName)
    $out = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $lines = 0
    for ($i = 0; $i -lt 64; $i++) {
        $min = 64; $max = -1
        for ($j = 0; $j -lt 64; $j++) {
            $a = if ($axis -eq 'v') { $src.GetPixel($j, $i).A } else { $src.GetPixel($i, $j).A }
            if ($a -ge 10) { if ($j -lt $min) { $min = $j }; if ($j -gt $max) { $max = $j } }
        }
        if ($max -lt 0) { continue }
        $lines++
        $cx = 31.5
        $sx = $min; $sw = $max - $min + 1
        $dx = [float]($cx - $target / 2.0)
        $sRect = if ($axis -eq 'v') { New-Object System.Drawing.RectangleF($sx, $i, $sw, 1) }
                 else               { New-Object System.Drawing.RectangleF($i, $sx, 1, $sw) }
        $dRect = if ($axis -eq 'v') { New-Object System.Drawing.RectangleF($dx, $i, $target, 1) }
                 else               { New-Object System.Drawing.RectangleF($i, $dx, 1, $target) }
        $g.DrawImage($src, $dRect, $sRect, [System.Drawing.GraphicsUnit]::Pixel)
    }
    $g.Dispose()
    $src.Dispose()
    # edge-extend outermost line (clone line 1 -> 0 and 62 -> 63) to remove hairline gap
    for ($j = 0; $j -lt 64; $j++) {
        if ($axis -eq 'v') {
            $out.SetPixel($j, 0,  $out.GetPixel($j, 1))
            $out.SetPixel($j, 63, $out.GetPixel($j, 62))
        } else {
            $out.SetPixel(0, $j,  $out.GetPixel(1, $j))
            $out.SetPixel(63, $j, $out.GetPixel(62, $j))
        }
    }
    Copy-Item $f.FullName (Join-Path $BackupDir $f.Name) -Force
    $out.Save($f.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    ("{0,-24} axis={1} lines={2} -> width {3}" -f $f.Name, $axis, $lines, $target)
}
Write-Host "DONE (backup: $BackupDir)"
