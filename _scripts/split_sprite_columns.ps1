param([string]$Source, [string]$OutDir, [int]$Columns = 3, [string]$Sizes, [string]$Boundaries = "")

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$targets = @()
foreach ($s in $Sizes.Split(',')) {
    $p2 = $s.Trim().Split('x')
    $targets += ,@([int]$p2[0], [int]$p2[1])
}

$src = [System.Drawing.Bitmap]::FromFile($Source)

# build column ranges: either from normalized boundaries or equal division
$ranges = @()
if ($Boundaries -ne "") {
    $cuts = @()
    foreach ($b in $Boundaries.Split(',')) {
        $cuts += [int]([double]$b.Trim() * $src.Width)
    }
    $prev = 0
    for ($i = 0; $i -lt $Columns; $i++) {
        $end = $src.Width
        if ($i -lt $cuts.Count) { $end = $cuts[$i] }
        $ranges += ,@($prev, ($end - $prev))
        $prev = $end
    }
} else {
    $colW = [int][Math]::Floor($src.Width / $Columns)
    for ($i = 0; $i -lt $Columns; $i++) {
        $x0 = $i * $colW
        $w = $src.Width - $x0
        if ($i -ne ($Columns - 1)) { $w = $colW }
        $ranges += ,@($x0, $w)
    }
}

for ($c = 0; $c -lt $Columns; $c++) {
    $x0 = $ranges[$c][0]
    $w = $ranges[$c][1]
    $strip = $src.Clone((New-Object System.Drawing.Rectangle($x0, 0, $w, $src.Height)),
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $strip.Width, $strip.Height)
    $data = $strip.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $strip.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $strip.UnlockBits($data)

    $minX = $strip.Width; $minY = $strip.Height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $strip.Height; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $strip.Width; $x++) {
            if ($bytes[($row + ($x * 4)) + 3] -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    $tw = $targets[$c][0]
    $th = $targets[$c][1]

    if ($maxX -lt 0) {
        $blank = New-Object System.Drawing.Bitmap($tw, $th)
        $blank.Save((Join-Path $OutDir ("col" + $c + ".png")),
            [System.Drawing.Imaging.ImageFormat]::Png)
        $blank.Dispose()
        $strip.Dispose()
        continue
    }

    $cropX = [Math]::Max(0, $minX - 2)
    $cropY = [Math]::Max(0, $minY - 2)
    $cropW = [Math]::Min($strip.Width - $cropX, ($maxX - $minX + 1) + 4)
    $cropH = [Math]::Min($strip.Height - $cropY, ($maxY - $minY + 1) + 4)
    $cropped = $strip.Clone((New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)),
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $strip.Dispose()

    $scale = [Math]::Min($tw / $cropped.Width, $th / $cropped.Height)
    $dstW = [Math]::Max(1, [int]([Math]::Round($cropped.Width * $scale)))
    $dstH = [Math]::Max(1, [int]([Math]::Round($cropped.Height * $scale)))

    $dst = New-Object System.Drawing.Bitmap($tw, $th,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $ox = [int](($tw - $dstW) / 2)
    $oy = [int](($th - $dstH) / 2)
    $g.DrawImage($cropped, (New-Object System.Drawing.Rectangle($ox, $oy, $dstW, $dstH)))
    $g.Dispose()
    $dst.Save((Join-Path $OutDir ("col" + $c + ".png")),
        [System.Drawing.Imaging.ImageFormat]::Png)
    $dst.Dispose()
    $cropped.Dispose()
    Write-Output ("saved col" + $c)
}
