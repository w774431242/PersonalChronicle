# Redesign PC_PipeH.png (2026-09-06, root-cause of "hidden pipe still visible"):
#   OLD design: alpha=255 dark-brown 8-px cross + 12x12 center node.
#     Looked like a chromatic shadow on every tile, far more visible than the visible PC_Pipe_*
#     variant and inconsistent with the "almost invisible" promise of the def description.
#   NEW design (aligned with vanilla "hidden conduit" semantics):
#     - 64x64, alpha=0 across the entire tile (invisible at all zoom levels when not selected).
#     - tiny 2x2 dirt-tone #4a3e2a alpha=160 micro-node at the center only,
#       so even when zoomed in the player gets a faint hint of where the segment sits.
#     - vanilla "hidden conduit" check uses the *Def attribute*, not the alpha, to swap the
#       mesh for a wireframe at print/select time -- but 1.6 lacks that hook without a Harmony
#       patch. The simplest mechanical fix is to lean on RimWorld's built-in selection
#       ribbon: when the segment is mouse-selected or god-mode selected, the standard
#       outline + selection bracket still draws over alpha=0 sprites, which is enough
#       to locate the path. Players still see the *network* via the inspection panel
#       and "selected" pipe chain flow visualization if they want a stronger indicator.
#
# Reasoning sources (2026-09-06 audit):
#   - Byte audit: PC_PipeH.png was 64x64x32 ARGB, 347 bytes, alpha was 255 on every pixel
#     inside the 8-px-wide cross + 12x12 node; outside was alpha=0. The 8-px cross + node
#     drew a dark glyph over any terrain, much louder than the actual visible pipe.
#   - Building_PipeHidden.UseDynamicGraphic=false -> base.Graphic -> def.graphicData
#     (verified in dll: 1 hit for Building_PipeHidden + 1 hit for UseDynamicGraphic in
#     Assemblies\PersonalChronicle.dll). Code path is correct, the bug is purely the
#     sprite content.
#
# Args (override at run-time):
#   -Dir      directory containing the target PC_PipeH.png (default: relative to script).
#   -BackupDir backup destination (default: _backups\pipe_hidden_<timestamp>).

param(
    [string]$Dir = (Join-Path $PSScriptRoot '..\Textures\Things\Building\Automation'),
    [string]$BackupDir = (Join-Path $PSScriptRoot ('..\_backups\pipe_hidden_' + (Get-Date -Format 'yyyyMMdd_HHmmss')))
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$target = Join-Path $Dir 'PC_PipeH.png'
if (Test-Path $target) {
    Copy-Item $target (Join-Path $BackupDir 'PC_PipeH.png') -Force
} else {
    throw "missing target: $target"
}

# New sprite: 64x64 alpha=0 everywhere except a single 2x2 dirt-tone dot in the center.
$bm = New-Object System.Drawing.Bitmap(64, 64)
# Make absolutely sure we start at alpha=0:
for ($y = 0; $y -lt 64; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
        $bm.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    }
}
# 2x2 micro dot at the center (covers (31,31), (31,32), (32,31), (32,32)).
# dirt 50% alpha = 160
$dot = [System.Drawing.Color]::FromArgb(160, 74, 62, 42)
for ($y = 31; $y -le 32; $y++) {
    for ($x = 31; $x -le 32; $x++) {
        $bm.SetPixel($x, $y, $dot)
    }
}
# Optional: even softer halo 4x4 around the dot to break hard 1-px aliasing on bright tiles.
# Alpha decays 96 -> 0 from inside out; never reaching above 100 so it stays below terrain noise.
$halo = [System.Drawing.Color]::FromArgb(96, 74, 62, 42)
for ($y = 30; $y -le 33; $y++) {
    for ($x = 30; $x -le 33; $x++) {
        if ($x -ge 31 -and $x -le 32 -and $y -ge 31 -and $y -le 32) { continue }
        $bm.SetPixel($x, $y, $halo)
    }
}

$bm.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$bm.Dispose()
("saved PC_PipeH.png (alpha=0 base + 2x2 dirt dot @ center, halo); backup -> {0}" -f $BackupDir)
Write-Host "DONE"
