# Old (current textures) vs New (sliced) side-by-side contact sheet, 3x upscale.
param(
    [Parameter(Mandatory=$true)][string]$OldDir,
    [Parameter(Mandatory=$true)][string]$NewDir,
    [Parameter(Mandatory=$true)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$names = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt 16; $i++) { [void]$names.Add("PC_Pipe_BodyM$i") }
for ($i = 0; $i -lt 16; $i++) { [void]$names.Add("PC_PipeH_M$i") }

$scale = 3; $cell = 64 * $scale; $labelH = 18
$cols = 4
$rows = [Math]::Ceiling($names.Count / $cols)
$mw = $cols * ($cell * 2 + 14) + 14
$mh = $rows * ($cell + $labelH + 8) + 14
$m = New-Object System.Drawing.Bitmap($mw, $mh)
$g = [System.Drawing.Graphics]::FromImage($m)
$g.Clear([System.Drawing.Color]::FromArgb(46, 46, 54))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font = New-Object System.Drawing.Font('Consolas', 9)
$brushW = [System.Drawing.Brushes]::White
$brushY = [System.Drawing.Brushes]::Yellow

for ($i = 0; $i -lt $names.Count; $i++) {
    $cxi = 14 + ($i % $cols) * ($cell * 2 + 14)
    $cyi = 14 + [Math]::Floor($i / $cols) * ($cell + $labelH + 8)
    $oldP = Join-Path $OldDir ($names[$i] + '.png')
    if (Test-Path $oldP) {
        $oi = [System.Drawing.Image]::FromFile($oldP)
        $g.DrawImage($oi, $cxi, $cyi, $cell, $cell)
        $oi.Dispose()
    }
    $newP = Join-Path $NewDir ($names[$i] + '.png')
    if (Test-Path $newP) {
        $ni = [System.Drawing.Image]::FromFile($newP)
        $g.DrawImage($ni, ($cxi + $cell + 6), $cyi, $cell, $cell)
        $ni.Dispose()
    }
    $g.DrawString($names[$i], $font, $brushY, $cxi, ($cyi + $cell + 1))
}
$g.Dispose()
$m.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$m.Dispose()
Write-Host ("montage -> " + $Out)
