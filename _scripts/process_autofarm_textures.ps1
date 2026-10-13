# Process AI-generated raw textures into RimWorld-ready PNGs.
# Pipeline: load 1024px white-background render -> near-white to transparent
#           -> trim to content bounding box (+margin) -> high-quality
#           downscale to target size -> save PNG.
#
# Usage:
#   .\process_autofarm_textures.ps1 -InputDir <raw dir> -OutputDir <out dir> [-Size 64] [-WhiteThreshold 240] [-Margin 4] [-EdgeKey] [-Tolerance 60]
#
# Notes:
#   - Items (components) typically use 64; buildings use footprint*64
#     (e.g. 3x3 building => 192). RimWorld 1.6 also accepts 2x sizes.
#   - Default keying: simple near-white threshold.
#   - -EdgeKey: flood-fill from the image borders, removing only pixels
#     CONNECTED to the edges whose color is close to the sampled corner
#     color. Use this when the AI drew a non-white (e.g. pale orange)
#     background; interior colors similar to the background are protected
#     because they are not edge-connected.

param(
    [Parameter(Mandatory = $true)][string]$InputDir,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [int]$Size = 64,
    [int]$Width = 0,
    [int]$Height = 0,
    [byte]$WhiteThreshold = 240,
    [int]$Margin = 4,
    [switch]$EdgeKey,
    [int]$Tolerance = 60
)

# Non-square support: -Width/-Height take precedence over -Size.
if ($Width -gt 0 -and $Height -gt 0) { $Size = 0 }

# Edge flood-fill keyer compiled in C# (fast enough for 1024x1024).
if ($EdgeKey) {
    Add-Type -TypeDefinition @"
using System.Collections.Generic;
public static class TextureEdgeKeyer
{
    public static void KeyFromEdges(byte[] px, int w, int h, int tol)
    {
        bool[] bg = new bool[w * h];
        var q = new Queue<int>();
        // seed color from the four corners (pure corner key)
        int kr = 0, kg = 0, kb = 0;
        int[] c = { 0, (w - 1) * 4, (h - 1) * w * 4, ((h - 1) * w + w - 1) * 4 };
        foreach (int i in c) { kb += px[i]; kg += px[i + 1]; kr += px[i + 2]; }
        kb /= 4; kg /= 4; kr /= 4;
        for (int x = 0; x < w; x++)
        {
            TrySeedCorner(x, px, w, h, bg, q, kr, kg, kb, tol);
            TrySeedCorner((h - 1) * w + x, px, w, h, bg, q, kr, kg, kb, tol);
        }
        for (int y = 0; y < h; y++)
        {
            TrySeedCorner(y * w, px, w, h, bg, q, kr, kg, kb, tol);
            TrySeedCorner(y * w + w - 1, px, w, h, bg, q, kr, kg, kb, tol);
        }
        // drift BFS: compare each candidate with its SOURCE pixel, so the
        // fill can gradually drift across gradient background layers
        // (orange bg -> pale ground plate) while the dark outline of the
        // machine breaks propagation and protects the interior.
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int x = i % w, y = i / w;
            byte sr = px[i + 2], sg = px[i + 1], sb = px[i];
            if (x > 0) TrySeed(i - 1, px, w, h, bg, q, sr, sg, sb, tol);
            if (x < w - 1) TrySeed(i + 1, px, w, h, bg, q, sr, sg, sb, tol);
            if (y > 0) TrySeed(i - w, px, w, h, bg, q, sr, sg, sb, tol);
            if (y < h - 1) TrySeed(i + w, px, w, h, bg, q, sr, sg, sb, tol);
        }
        for (int i = 0; i < w * h; i++)
        {
            if (bg[i]) px[i * 4 + 3] = 0;
        }
    }

    private static void TrySeedCorner(int i, byte[] px, int w, int h, bool[] bg, Queue<int> q,
        int kr, int kg, int kb, int tol)
    {
        if (bg[i]) return;
        int dr = System.Math.Abs(px[i + 2] - kr);
        int dg = System.Math.Abs(px[i + 1] - kg);
        int db = System.Math.Abs(px[i] - kb);
        if (dr <= tol && dg <= tol && db <= tol)
        {
            bg[i] = true;
            q.Enqueue(i);
        }
    }

    private static void TrySeed(int i, byte[] px, int w, int h, bool[] bg, Queue<int> q,
        int sr, int sg, int sb, int tol)
    {
        if (bg[i]) return;
        int dr = System.Math.Abs(px[i + 2] - sr);
        int dg = System.Math.Abs(px[i + 1] - sg);
        int db = System.Math.Abs(px[i] - sb);
        if (dr <= tol && dg <= tol && db <= tol)
        {
            bg[i] = true;
            q.Enqueue(i);
        }
    }
}
"@
}

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $InputDir)) {
    Write-Error "InputDir not found: $InputDir"
    exit 1
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$files = Get-ChildItem -Path $InputDir -Filter *.png -File
if ($files.Count -eq 0) {
    Write-Error "No PNG files in $InputDir"
    exit 1
}

foreach ($file in $files) {
    $outW = if ($Width -gt 0) { $Width } else { $Size }
    $outH = if ($Height -gt 0) { $Height } else { $Size }
    $src = [System.Drawing.Bitmap]::FromFile($file.FullName)

    # --- Pass 1: keying (near-white threshold OR edge flood-fill) ---
    $rect = New-Object System.Drawing.Rectangle(0, 0, $src.Width, $src.Height)
    $data = $src.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $src.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

    if ($EdgeKey) {
        [TextureEdgeKeyer]::KeyFromEdges($bytes, $src.Width, $src.Height, $Tolerance)
    }

    $minX = $src.Width;  $minY = $src.Height
    $maxX = -1;          $maxY = -1

    for ($y = 0; $y -lt $src.Height; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $src.Width; $x++) {
            $i = $row + ($x * 4)
            if (-not $EdgeKey) {
                $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]
                if ($r -ge $WhiteThreshold -and $g -ge $WhiteThreshold -and $b -ge $WhiteThreshold) {
                    $bytes[$i + 3] = 0
                }
            }
            if ($bytes[$i + 3] -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $src.UnlockBits($data)

    if ($maxX -lt 0) {
        Write-Warning "$($file.Name): fully transparent after keying, skipped."
        $src.Dispose()
        continue
    }

    # --- Pass 2: crop to bounding box + margin ---
    $cropX = [Math]::Max(0, $minX - $Margin)
    $cropY = [Math]::Max(0, $minY - $Margin)
    $cropW = [Math]::Min($src.Width  - $cropX, ($maxX - $minX + 1) + $Margin * 2)
    $cropH = [Math]::Min($src.Height - $cropY, ($maxY - $minY + 1) + $Margin * 2)
    $cropped = $src.Clone((New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)),
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $src.Dispose()

    # --- Pass 3: fit into outW x outH, centered, high quality ---
    $scale = [Math]::Min($outW / $cropped.Width, $outH / $cropped.Height)
    $dstW = [Math]::Max(1, [int]([Math]::Round($cropped.Width  * $scale)))
    $dstH = [Math]::Max(1, [int]([Math]::Round($cropped.Height * $scale)))

    $dst = New-Object System.Drawing.Bitmap($outW, $outH,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $offsetX = [int](($outW - $dstW) / 2)
    $offsetY = [int](($outH - $dstH) / 2)
    $g.DrawImage($cropped, (New-Object System.Drawing.Rectangle($offsetX, $offsetY, $dstW, $dstH)))
    $g.Dispose()

    $outPath = Join-Path $OutputDir $file.Name
    $dst.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $dst.Dispose()
    $cropped.Dispose()

    Write-Output ("{0}  ->  {1} ({2}x{3})" -f $file.Name, $outPath, $outW, $outH)
}

Write-Output "Done. $($files.Count) file(s) processed."
