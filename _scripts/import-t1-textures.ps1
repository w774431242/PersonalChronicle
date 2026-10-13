<# 导入用户制作的 T1 四向贴图。只导入选定设备的完整四向组，不生成或修改图片。
   例：pwsh -File _scripts/import-t1-textures.ps1 -SourceDirectory D:\贴图 -Device PC_T1_Smelter
   不指定 Device 时要求十台共四十张齐全。先验证全部选定文件，再备份并复制。
#>
param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [string[]]$Device = @(),
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
[xml]$defs = Get-Content -LiteralPath (Join-Path $root 'Defs\T1_Production.xml') -Encoding utf8
$available = @($defs.Defs.ThingDef | ForEach-Object { $_.defName })
if ($Device.Count -eq 0) { $Device = $available }
foreach ($id in $Device) { if ($id -notin $available) { throw "未知 T1 设备：$id" } }
Add-Type -AssemblyName System.Drawing
$files = @()
function Read-BigEndianInt([byte[]]$data, [int]$offset) {
    return ([long]$data[$offset]*16777216 + [long]$data[$offset+1]*65536 + [long]$data[$offset+2]*256 + $data[$offset+3])
}
foreach ($id in ($Device | Select-Object -Unique)) {
    foreach ($direction in @('north','east','south','west')) {
        $name = "${id}_${direction}.png"
        $path = Join-Path $source $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "缺少贴图：$name；每台需完整四向。" }
        $bytes = [IO.File]::ReadAllBytes($path)
        if ($bytes.Length -lt 33 -or [BitConverter]::ToString($bytes,0,8) -ne '89-50-4E-47-0D-0A-1A-0A') { throw "不是 PNG：$name" }
        $width=Read-BigEndianInt $bytes 16; $height=Read-BigEndianInt $bytes 20
        if ($bytes[24] -ne 8 -or $bytes[25] -ne 6) { throw "要求 8 位 RGBA PNG（真实 alpha）：$name" }
        $horizontal = $direction -in @('north','south')
        $longSide = if($horizontal){$width}else{$height}
        $shortSide = if($horizontal){$height}else{$width}
        if($longSide*3 -ne $shortSide*7 -or $shortSide -lt 192) { throw "画布比例应为南北 7:3、东西 3:7，短边至少 192 像素：$name (${width}x${height})" }
        $image = [Drawing.Bitmap]::new($path)
        try {
            foreach ($corner in @(@(0,0),@(($image.Width-1),0),@(0,($image.Height-1)),@(($image.Width-1),($image.Height-1)))) {
                if ($image.GetPixel([int]$corner[0],[int]$corner[1]).A -ne 0) { throw "画布四角必须透明：$name" }
            }
            $visible = $false
            for($y=0;$y -lt $image.Height -and -not $visible;$y+=4) {
                for($x=0;$x -lt $image.Width;$x+=4) { if($image.GetPixel($x,$y).A -gt 0){$visible=$true;break} }
            }
            if(-not $visible){throw "贴图没有可见设备内容：$name"}
        } finally { $image.Dispose() }
        $files += [pscustomobject]@{Name=$name;Path=$path;Hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
}
if ($ValidateOnly) {
    Write-Host "校验通过：$($files.Count) 张贴图；未复制文件。"
    return
}
$destination = Join-Path $root 'Textures\Things\Building\T1Production'
$backup = Join-Path $root ('_backups\T1-textures_'+(Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($file in $files) {
    $target = Join-Path $destination $file.Name
    if(Test-Path -LiteralPath $target) {
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination (Join-Path $backup $file.Name)
    }
    Copy-Item -LiteralPath $file.Path -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Hash) { throw "导入哈希不一致：$($file.Name)" }
}
Write-Host "已导入并校验 $($files.Count) 张贴图：$destination"
Write-Host '执行 _scripts/deploy-to-game.ps1 后重启 RimWorld，完整四向组将自动替换临时原版贴图。'
