# Build PC_PipeH.png (hidden logistics pipe) directly from the VISIBLE pipe sprites.
# PM instruction (2026-09-06): "hidden pipe texture = just copy the logistics pipe texture".
#
# Why a composite instead of a byte copy of PC_Pipe_Body.png:
#   - PC_Pipe_Body.png is a VERTICAL trench (26 px wide, full height).
#   - Building_PipeHidden sets UseDynamicGraphic=false, so EVERY hidden segment (any
#     orientation) renders that ONE sprite. A straight byte copy therefore renders
#     horizontal runs as vertical trenches -> looks like a row of fences, visually broken.
#   - The correct all-direction sprite is the union of the vertical and horizontal
#     variants, which is exactly what PC_Pipe_Body.png (v) and PC_Pipe_Body_east.png (h)
#     give us. Both are vanilla-visible-pipe assets, so the hidden pipe still reads as
#     "the logistics pipe" -- it just also joins in all four directions.
#   - Result is identical in visual language to the visible pipe: same trench bg, same
#     metal core, same edge treatment. The only "hidden" semantics left is the Def/NPC
#     class, which is fine: the pipe is visually identical to the visible variant, so the
#     player understands the network shape at a glance.
#
# Overlay rule: where both arms overlap (the center square) prefer the horizontal arm's
# metal core so the crossing reads as a continuous pipe, not a broken joint.
param(
    [string]$Dir = (Join-Path $PSScriptRoot '..\Textures\Things\Building\Automation'),
    [string]$BackupDir = (Join-Path $PSScriptRoot ('..\_backups\pipe_hidden_' + (Get-Date -Format 'yyyyMMdd_HHmmss')))
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$vPath = Join-Path $Dir 'PC_Pipe_Body.png'       # vertical trench
$hPath = Join-Path $Dir 'PC_Pipe_Body_east.png'  # horizontal trench
foreach ($p in @($vPath, $hPath)) { if (!(Test-Path $p)) { throw "missing source: $p" } }

$out = Join-Path $Dir 'PC_PipeH.png'
if (Test-Path $out) { Copy-Item $out (Join-Path $BackupDir 'PC_PipeH.png') -Force }

$v = [System.Drawing.Bitmap]::FromFile($vPath)
$h = [System.Drawing.Bitmap]::FromFile($hPath)
if ($v.Width -ne 64 -or $v.Height -ne 64 -or $h.Width -ne 64 -or $h.Height -ne 64) {
    throw "source sprites must be 64x64 (v=$($v.Width)x$($v.Height) h=$($h.Width)x$($h.Height))"
}

$canvas = New-Object System.Drawing.Bitmap(64, 64)
for ($y = 0; $y -lt 64; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
        $pv = $v.GetPixel($x, $y)
        $ph = $h.GetPixel($x, $y)
        # take whichever arm actually has content; on overlap prefer the horizontal core
        $c = if ($ph.A -gt 10) { $ph } elseif ($pv.A -gt 10) { $pv } else { [System.Drawing.Color]::FromArgb(0, 0, 0, 0) }
        $canvas.SetPixel($x, $y, $c)
    }
}

$canvas.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose(); $v.Dispose(); $h.Dispose()
"saved PC_PipeH.png = PC_Pipe_Body(v) UNION PC_Pipe_Body_east(h); backup -> $BackupDir"
Write-Host "DONE"
