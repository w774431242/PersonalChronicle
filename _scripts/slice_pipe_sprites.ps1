# 管道图集合切割：2 列（左=竖管/右=横管）× 3 行（上=无IO主体/中=输出口/下=输入口）
# 8-conn 连通域检测 → alpha bbox+2px → 等比 fit 64×64 → 四向 13 文件（In/Out 各 4 + Body 5，含 _north 竖管槽）
# 底色自动判定：四角 alpha<10 视为透明底；否则报错退出（黑底不自动 keying，防误删深色机体）
param(
    [Parameter(Mandatory=$true)][string]$InputPng,
    [Parameter(Mandatory=$true)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Bitmap]::FromFile($InputPng)
$W = $src.Width; $H = $src.Height
Write-Host "sheet: ${W}x${H}"

$rect = New-Object System.Drawing.Rectangle(0,0,$W,$H)
$data = $src.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes = New-Object byte[] ($stride * $H)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$src.UnlockBits($data)

function Get-A([int]$x,[int]$y){ $bytes[$y*$stride + $x*4 + 3] }

# 四角透明判定
$corners = @( (Get-A 0 0), (Get-A ($W-1) 0), (Get-A 0 ($H-1)), (Get-A ($W-1) ($H-1)) )
Write-Host ("corner alpha: " + ($corners -join ','))
if (($corners | Where-Object { $_ -ge 10 }).Count -gt 0) { throw 'OPAQUE background: 需人工确认底色处理，脚本不自动 keying' }

# 8-conn 连通域（alpha 阈值 10）
$visited = New-Object byte[] ($W*$H)
$comps = New-Object System.Collections.ArrayList
$stack = New-Object System.Collections.Generic.Stack[int]
for($y0=0;$y0 -lt $H;$y0++){
  for($x0=0;$x0 -lt $W;$x0++){
    $i0 = $y0*$W+$x0
    if($visited[$i0] -eq 1){ continue }
    if((Get-A $x0 $y0) -lt 10){ $visited[$i0]=1; continue }
    $minX=$x0;$maxX=$x0;$minY=$y0;$maxY=$y0;$area=0
    $stack.Push($i0); $visited[$i0]=1
    while($stack.Count -gt 0){
      $i=$stack.Pop(); $cx=$i % $W; $cy=[int]($i/$W); $area++
      if($cx -lt $minX){$minX=$cx}; if($cx -gt $maxX){$maxX=$cx}
      if($cy -lt $minY){$minY=$cy}; if($cy -gt $maxY){$maxY=$cy}
      for($dy=-1;$dy -le 1;$dy++){
        for($dx=-1;$dx -le 1;$dx++){
          if($dx -eq 0 -and $dy -eq 0){ continue }
          $nx=$cx+$dx; $ny=$cy+$dy
          if($nx -lt 0 -or $ny -lt 0 -or $nx -ge $W -or $ny -ge $H){ continue }
          $ni=$ny*$W+$nx
          if($visited[$ni] -eq 1){ continue }
          if((Get-A $nx $ny) -ge 10){ $visited[$ni]=1; $stack.Push($ni) }
          else { $visited[$ni]=1 }
        }
      }
    }
    if($area -ge 10000){
      [void]$comps.Add(@{minX=$minX;maxX=$maxX;minY=$minY;maxY=$maxY;area=$area;
        cx=(($minX+$maxX)/2); cy=(($minY+$maxY)/2)})
    }
  }
}
Write-Host ("components(>=500px): " + $comps.Count)
if($comps.Count -ne 6){ $comps | ForEach-Object { Write-Host ("  comp cx={0} cy={1} area={2}" -f $_.cx,$_.cy,$_.area) }; throw "expect 6 components" }

# 排序：按 cx 分左右列，列内按 cy 排行 → [V,H]×[Body,Out,In]
$left  = $comps | Where-Object { $_.cx -lt ($W/2) } | Sort-Object cy
$right = $comps | Where-Object { $_.cx -ge ($W/2) } | Sort-Object cy
if($left.Count -ne 3 -or $right.Count -ne 3){ throw ("column split failed: left={0} right={1}" -f $left.Count,$right.Count) }
$map = @(
  @{name='Body'; v=$left[0];  h=$right[0]},
  @{name='Out';  v=$left[1];  h=$right[1]},
  @{name='In';   v=$left[2];  h=$right[2]}
)

function New-CellBitmap($c){
  $m = 2
  $x=[Math]::Max(0,$c.minX-$m); $y=[Math]::Max(0,$c.minY-$m)
  $w=[Math]::Min($W-$x, $c.maxX-$c.minX+1+2*$m); $h=[Math]::Min($H-$y, $c.maxY-$c.minY+1+2*$m)
  $cell = $src.Clone((New-Object System.Drawing.Rectangle($x,$y,$w,$h)), $src.PixelFormat)
  return $cell
}

function Save-64($cellBmp, [System.Drawing.RotateFlipType]$rf, $path){
  $cellBmp.RotateFlip($rf)
  $out = New-Object System.Drawing.Bitmap(64,64)
  $g = [System.Drawing.Graphics]::FromImage($out)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $cw=$cellBmp.Width; $ch=$cellBmp.Height
  $scale = [Math]::Min(64.0/$cw, 64.0/$ch)
  $dw=[int][Math]::Round($cw*$scale); $dh=[int][Math]::Round($ch*$scale)
  $dx=[int]((64-$dw)/2); $dy=[int]((64-$dh)/2)
  $g.DrawImage($cellBmp, (New-Object System.Drawing.Rectangle($dx,$dy,$dw,$dh)))
  $g.Dispose()
  $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $out.Dispose(); $cellBmp.Dispose()
  Write-Host ("  saved " + (Split-Path $path -Leaf))
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
foreach($m in $map){
  # In/Out：横管素材（口在东端=轴向口）旋转四向 → 口朝东/南/西/北（管形态随之竖横交替）
  # Body：竖管图填 north/south 槽、横管图填 east/west 槽（Graphic_Multi 兼容命名）
  if($m.name -ne 'Body'){
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::RotateNoneFlipNone) (Join-Path $OutDir ("PC_Pipe_{0}.png" -f $m.name))          # 口朝东
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::Rotate90FlipNone)  (Join-Path $OutDir ("PC_Pipe_{0}_south.png" -f $m.name))   # 口朝南
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::Rotate180FlipNone) (Join-Path $OutDir ("PC_Pipe_{0}_west.png" -f $m.name))     # 口朝西
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::Rotate270FlipNone) (Join-Path $OutDir ("PC_Pipe_{0}_north.png" -f $m.name))    # 口朝北
  } else {
    Save-64 (New-CellBitmap $m.v) ([System.Drawing.RotateFlipType]::RotateNoneFlipNone) (Join-Path $OutDir "PC_Pipe_Body.png")
    Save-64 (New-CellBitmap $m.v) ([System.Drawing.RotateFlipType]::RotateNoneFlipNone) (Join-Path $OutDir "PC_Pipe_Body_north.png")
    Save-64 (New-CellBitmap $m.v) ([System.Drawing.RotateFlipType]::RotateNoneFlipNone) (Join-Path $OutDir "PC_Pipe_Body_south.png")
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::RotateNoneFlipNone) (Join-Path $OutDir "PC_Pipe_Body_east.png")
    Save-64 (New-CellBitmap $m.h) ([System.Drawing.RotateFlipType]::Rotate180FlipNone) (Join-Path $OutDir "PC_Pipe_Body_west.png")
  }
}
$src.Dispose()
Write-Host 'DONE'
