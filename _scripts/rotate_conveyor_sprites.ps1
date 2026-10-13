# Rotate north-facing conveyor segment sprites into Graphic_Multi directions.
# north = base file (no suffix); east = 90cw; south = 180; west = 270.
param(
    [Parameter(Mandatory = $true)][string]$NorthDir,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [ValidateSet('north', 'east')][string]$BaseDirection = 'north'
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$map = @(
    @{ Suffix = '_east';  Rot = [System.Drawing.RotateFlipType]::Rotate90FlipNone },
    @{ Suffix = '_south'; Rot = [System.Drawing.RotateFlipType]::Rotate180FlipNone },
    @{ Suffix = '_west';  Rot = [System.Drawing.RotateFlipType]::Rotate270FlipNone }
)

foreach ($file in Get-ChildItem -Path $NorthDir -Filter *.png -File) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $bmp = [System.Drawing.Bitmap]::FromFile($file.FullName)

    if ($BaseDirection -eq 'east') {
        # base image points RIGHT (= east). Outputs all four suffixes.
        $asIs = $bmp.Clone()
        $asIs.Save((Join-Path $OutDir ($baseName + '_east.png')),
            [System.Drawing.Imaging.ImageFormat]::Png)
        $asIs.Dispose()

        $r90 = $bmp.Clone()
        $r90.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone)
        $r90.Save((Join-Path $OutDir ($baseName + '_south.png')),
            [System.Drawing.Imaging.ImageFormat]::Png)
        $r90.Dispose()

        $r180 = $bmp.Clone()
        $r180.RotateFlip([System.Drawing.RotateFlipType]::Rotate180FlipNone)
        $r180.Save((Join-Path $OutDir ($baseName + '_west.png')),
            [System.Drawing.Imaging.ImageFormat]::Png)
        $r180.Dispose()

        $r270 = $bmp.Clone()
        $r270.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipNone)
        $r270.Save((Join-Path $OutDir ($baseName + '_north.png')),
            [System.Drawing.Imaging.ImageFormat]::Png)
        $r270.Dispose()

        Write-Output ("east-base: {0} -> 4 directions" -f $baseName)
        $bmp.Dispose()
        continue
    }

    foreach ($m in $map) {
        $rot = $bmp.Clone()
        $rot.RotateFlip($m.Rot)
        $outPath = Join-Path $OutDir ($baseName + $m.Suffix + '.png')
        $rot.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $rot.Dispose()
        Write-Output $outPath
    }
    $bmp.Dispose()
}

Write-Output "Done."
