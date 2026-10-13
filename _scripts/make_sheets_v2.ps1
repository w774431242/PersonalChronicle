param([string]$Dir, [string]$Out)
Add-Type -AssemblyName System.Drawing
$groups = @(
    @('sheet_hidden', @('PC_PipeH_M0','PC_PipeH_M1','PC_PipeH_M2','PC_PipeH_M3','PC_PipeH_M4','PC_PipeH_M5','PC_PipeH_M6','PC_PipeH_M7','PC_PipeH_M8','PC_PipeH_M9','PC_PipeH_M10','PC_PipeH_M11','PC_PipeH_M12','PC_PipeH_M13','PC_PipeH_M14','PC_PipeH_M15')),
    @('sheet_ports', @('PC_Pipe_In_N','PC_Pipe_In_E','PC_Pipe_In_S','PC_Pipe_In_W','PC_Pipe_Out_N','PC_Pipe_Out_E','PC_Pipe_Out_S','PC_Pipe_Out_W')),
    @('sheet_visible', @('PC_Pipe_BodyM0','PC_Pipe_BodyM1','PC_Pipe_BodyM2','PC_Pipe_BodyM3','PC_Pipe_BodyM4','PC_Pipe_BodyM5','PC_Pipe_BodyM6','PC_Pipe_BodyM7','PC_Pipe_BodyM8','PC_Pipe_BodyM9','PC_Pipe_BodyM10','PC_Pipe_BodyM11','PC_Pipe_BodyM12','PC_Pipe_BodyM13','PC_Pipe_BodyM14','PC_Pipe_BodyM15'))
)
foreach ($grp in $groups) {
    $names = $grp[1]
    $s = 3; $c = 64 * $s; $lh = 20; $cols = 8
    $rows = [Math]::Ceiling($names.Count / $cols)
    $m = New-Object System.Drawing.Bitmap (($cols * ($c + 8) + 8), ($rows * ($c + $lh + 6) + 8))
    $g = [System.Drawing.Graphics]::FromImage($m)
    $g.Clear([System.Drawing.Color]::FromArgb(34, 34, 40))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $f = New-Object System.Drawing.Font('Consolas', 9)
    for ($i = 0; $i -lt $names.Count; $i++) {
        $x = 8 + ($i % 8) * ($c + 8)
        $y = 8 + [Math]::Floor($i / 8) * ($c + $lh + 6)
        $p = Join-Path $Dir "$($names[$i]).png"
        if (Test-Path $p) {
            $im = [System.Drawing.Image]::FromFile($p)
            $g.DrawImage($im, $x, $y, $c, $c)
            $im.Dispose()
        }
        $g.DrawString(($names[$i] -replace 'PC_Pipe_?', ''), $f, [System.Drawing.Brushes]::Yellow, $x, ($y + $c + 2))
    }
    $g.Dispose()
    $m.Save((Join-Path $Out "$($grp[0]).png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $m.Dispose()
}
Write-Host 'ok'
