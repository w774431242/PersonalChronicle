$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$model = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'simple_model.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$colors = @{
 ink='#17263C'; muted='#52647D'; line='#61728B'; blue='#3664A3'; teal='#147E7A'; orange='#A9651C'; purple='#7352A0'; green='#237F5E'; red='#B14949'; amber='#AA751F'
}
$fills = @{ blue='#EDF3FD'; teal='#EAF8F5'; orange='#FFF4E6'; purple='#F4EEFB'; green='#E9F7EF'; red='#FFF0F0'; amber='#FFF7E4' }
$bitmap = [Drawing.Bitmap]::new([int]($model.width*$model.scale), [int]($model.height*$model.scale))
$bitmap.SetResolution(300,300)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([Drawing.ColorTranslator]::FromHtml('#F7F9FD'))
$graphics.ScaleTransform([single]$model.scale,[single]$model.scale)
$family = [Drawing.FontFamily]::new('Microsoft YaHei')
$culture = [Globalization.CultureInfo]::InvariantCulture
$svg = [Text.StringBuilder]::new()
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="6000" height="4700" viewBox="0 0 3000 2350" role="img" aria-labelledby="title desc">')
[void]$svg.AppendLine('<title id="title">Pawn经济AI简明闭环流程图</title><desc id="desc">原版调度、经济决策、购买穿戴、就业体检、收入与异常恢复。所有中文已转换矢量轮廓；全部路径通过实线箭头返回反馈。</desc><rect width="3000" height="2350" fill="#F7F9FD"/>')
function Num($value) { ([double]$value).ToString('0.###',$culture) }
function Color($hex) { [Drawing.ColorTranslator]::FromHtml($hex) }
function Outline($path) {
    $pts=$path.PathPoints; $types=$path.PathTypes; $out=[Text.StringBuilder]::new()
    for($i=0;$i -lt $pts.Length;$i++) {
        $kind=$types[$i] -band 7
        if($kind -eq 0) { [void]$out.Append("M$(Num $pts[$i].X),$(Num $pts[$i].Y) ") }
        elseif($kind -eq 1) { [void]$out.Append("L$(Num $pts[$i].X),$(Num $pts[$i].Y) ") }
        elseif($kind -eq 3) {
            [void]$out.Append("C$(Num $pts[$i].X),$(Num $pts[$i].Y) $(Num $pts[$i+1].X),$(Num $pts[$i+1].Y) $(Num $pts[$i+2].X),$(Num $pts[$i+2].Y) ")
            $i+=2
        }
        if(($types[$i] -band 128) -ne 0) { [void]$out.Append('Z ') }
    }
    $out.ToString()
}
function TextAt([double]$x,[double]$y,[string]$text,[double]$size,[string]$tone='ink',[string]$align='center',[bool]$bold=$false) {
    $lines=$text -split "`n"; $lineHeight=$size*1.42
    $cy=$y-($lines.Length-1)*$lineHeight/2
    foreach($line in $lines) {
        $path=[Drawing.Drawing2D.GraphicsPath]::new()
        $style=if($bold){1}else{0}
        $path.AddString($line,$family,$style,[single]$size,[Drawing.PointF]::new(0,0),[Drawing.StringFormat]::GenericDefault)
        if($path.PointCount -eq 0){$path.Dispose(); continue}
        $bounds=$path.GetBounds()
        $tx=if($align -eq 'left'){$x-$bounds.X}else{$x-$bounds.X-$bounds.Width/2}
        $matrix=[Drawing.Drawing2D.Matrix]::new()
        $matrix.Translate([single]$tx,[single]($cy-$bounds.Y-$bounds.Height/2)); $path.Transform($matrix)
        $brush=[Drawing.SolidBrush]::new((Color $colors[$tone]))
        $graphics.FillPath($brush,$path)
        $escaped=[Security.SecurityElement]::Escape($line)
        [void]$svg.AppendLine("<g aria-label=`"$escaped`"><path d=`"$(Outline $path)`" fill=`"$($colors[$tone])`"/></g>")
        $matrix.Dispose();$path.Dispose();$brush.Dispose(); $cy+=$lineHeight
    }
}
function RoundRect([double]$x,[double]$y,[double]$w,[double]$h,[string]$fill,[string]$stroke,[double]$radius=18,[double]$strokeWidth=2) {
    $path=[Drawing.Drawing2D.GraphicsPath]::new(); $d=[single]($radius*2)
    $path.AddArc([single]$x,[single]$y,$d,$d,180,90)
    $path.AddArc([single]($x+$w-$d),[single]$y,$d,$d,270,90)
    $path.AddArc([single]($x+$w-$d),[single]($y+$h-$d),$d,$d,0,90)
    $path.AddArc([single]$x,[single]($y+$h-$d),$d,$d,90,90);$path.CloseFigure()
    $brush=[Drawing.SolidBrush]::new((Color $fill)); $pen=[Drawing.Pen]::new((Color $stroke),[single]$strokeWidth)
    $graphics.FillPath($brush,$path); $graphics.DrawPath($pen,$path)
    [void]$svg.AppendLine("<rect x=`"$x`" y=`"$y`" width=`"$w`" height=`"$h`" rx=`"$radius`" fill=`"$fill`" stroke=`"$stroke`" stroke-width=`"$strokeWidth`"/>")
    $brush.Dispose();$pen.Dispose();$path.Dispose()
}
foreach($panel in $model.panels) {
    RoundRect $panel.x $panel.y $panel.w $panel.h '#FFFFFF' '#DCE4EE' 24 2
    TextAt ($panel.x+30) ($panel.y+48) $panel.title 35 $panel.tone 'left' $true
}
# Draw edges first, then nodes and labels. Arrowheads use the last segment's direction.
foreach($edge in $model.edges) {
    if($edge.virtual -or $edge.points.Count -lt 2){continue}
    $points=[Drawing.PointF[]]@($edge.points | ForEach-Object { [Drawing.PointF]::new([single]$_[0],[single]$_[1]) })
    $pen=[Drawing.Pen]::new((Color $colors[$edge.tone]),3.4)
    $pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($pen,$points)
    $poly=($edge.points | ForEach-Object { "$(Num $_[0]),$(Num $_[1])" }) -join ' '
    [void]$svg.AppendLine("<polyline points=`"$poly`" fill=`"none`" stroke=`"$($colors[$edge.tone])`" stroke-width=`"3.4`" stroke-linejoin=`"round`"/>")
    $end=$points[-1]; $before=$points[-2];$dx=$end.X-$before.X;$dy=$end.Y-$before.Y;$len=[Math]::Sqrt($dx*$dx+$dy*$dy)
    $ux=$dx/$len;$uy=$dy/$len
    $arrow=[Drawing.PointF[]]@($end,[Drawing.PointF]::new([single]($end.X-15*$ux+7*$uy),[single]($end.Y-15*$uy-7*$ux)),[Drawing.PointF]::new([single]($end.X-15*$ux-7*$uy),[single]($end.Y-15*$uy+7*$ux)))
    $brush=[Drawing.SolidBrush]::new((Color $colors[$edge.tone]));$graphics.FillPolygon($brush,$arrow)
    $poly=($arrow | ForEach-Object { "$(Num $_.X),$(Num $_.Y)" }) -join ' '
    [void]$svg.AppendLine("<polygon points=`"$poly`" fill=`"$($colors[$edge.tone])`"/>")
    $pen.Dispose();$brush.Dispose()
}
foreach($n in $model.nodes) {
    $fill=$fills[$n.tone];$stroke=$colors[$n.tone]
    if($n.shape -eq 'rect') { RoundRect $n.x $n.y $n.w $n.h $fill $stroke 18 2.4 }
    elseif($n.shape -eq 'circle') {
        $brush=[Drawing.SolidBrush]::new((Color $fill));$pen=[Drawing.Pen]::new((Color $stroke),2.6)
        $graphics.FillEllipse($brush,[single]$n.x,[single]$n.y,[single]$n.w,[single]$n.h)
        $graphics.DrawEllipse($pen,[single]$n.x,[single]$n.y,[single]$n.w,[single]$n.h)
        [void]$svg.AppendLine("<ellipse cx=`"$($n.x+$n.w/2)`" cy=`"$($n.y+$n.h/2)`" rx=`"$($n.w/2)`" ry=`"$($n.h/2)`" fill=`"$fill`" stroke=`"$stroke`" stroke-width=`"2.6`"/>")
        $brush.Dispose();$pen.Dispose()
    } else {
        $pts=[Drawing.PointF[]]@([Drawing.PointF]::new($n.x+$n.w/2,$n.y),[Drawing.PointF]::new($n.x+$n.w,$n.y+$n.h/2),[Drawing.PointF]::new($n.x+$n.w/2,$n.y+$n.h),[Drawing.PointF]::new($n.x,$n.y+$n.h/2))
        $brush=[Drawing.SolidBrush]::new((Color $fill));$pen=[Drawing.Pen]::new((Color $stroke),2.4)
        $graphics.FillPolygon($brush,$pts);$graphics.DrawPolygon($pen,$pts)
        $poly=($pts | ForEach-Object { "$($_.X),$($_.Y)" }) -join ' '
        [void]$svg.AppendLine("<polygon points=`"$poly`" fill=`"$fill`" stroke=`"$stroke`" stroke-width=`"2.4`"/>")
        $brush.Dispose();$pen.Dispose()
    }
    TextAt ($n.x+$n.w/2) ($n.y+$n.h/2) $n.text $n.size 'ink' 'center' ($n.shape -eq 'circle')
}
foreach($edge in $model.edges) {
    if(!$edge.virtual -and $edge.label){TextAt $edge.lx $edge.ly $edge.label 26 $edge.tone}
}
foreach($note in $model.notes){
    if($note.text -eq '下一次原版自然决策') { RoundRect ($note.x-180) ($note.y-20) 360 40 '#FFFFFF' '#FFFFFF' 8 0 }
    TextAt $note.x $note.y $note.text $note.size $note.tone $note.align $note.bold
}
[void]$svg.AppendLine('</svg>')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Pawn经济AI_简明闭环_矢量.svg'),$svg.ToString(),[Text.UTF8Encoding]::new($false))
$bitmap.Save((Join-Path $PSScriptRoot 'Pawn经济AI_简明闭环_高清.png'),[Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose();$family.Dispose();$bitmap.Dispose()
Write-Output 'Created 6000×4700 PNG at 300 DPI and outlined SVG.'
