param([ValidateSet('Merge','Package','All')][string]$Mode='All')
$ErrorActionPreference='Stop'
$designRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../evidence/全模块_设计清单.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$mergedPath=Join-Path $designRoot $manifest.merged
$deliveryDir=Join-Path $designRoot '00-总纲与共享/06-交付包'
$utf8=[System.Text.UTF8Encoding]::new($false)

if ($Mode -in @('Merge','All')) {
    $builder=[System.Text.StringBuilder]::new()
    [void]$builder.AppendLine('# Pawn 经济需求驱动 AI：全部十二模块方案合订本')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('整套设计 v1.0 · 2026-10-08。包含跨模块总册及 M1–M12 全文。此文件由分模块方案合并生成；更新请编辑各源文件后重新导出。')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('这是代码设计交付，尚未实现完整模型。原版接口证据、文档校验和假设演算不等于运行编译、经济仿真、性能或游戏验收。前端硬依赖 RUIF，机构公有，新增货币须有真实银币缴存。')
    [void]$builder.AppendLine()
    $sources=@([pscustomobject]@{file=$manifest.master;anchor='overall'})
    foreach($module in $manifest.modules) {
        $number=[int]($module.id -replace '^M','')
        $sources += [pscustomobject]@{file=$module.file;anchor=('module-{0:00}' -f $number)}
    }
    foreach($source in $sources) {
        [void]$builder.AppendLine(('<!-- source: {0} -->' -f $source.file))
        [void]$builder.AppendLine(('<a id="{0}"></a>' -f $source.anchor))
        $insideFence=$false
        foreach($line in [System.IO.File]::ReadAllLines((Join-Path $designRoot $source.file),[System.Text.Encoding]::UTF8)) {
            $outputLine=$line
            if ($line -match '^\s*```') {$insideFence=!$insideFence}
            elseif (!$insideFence -and $line -match '^#{1,5} ') {$outputLine='#'+$line}
            foreach($targetSource in $sources) {
                $fileName=[System.IO.Path]::GetFileName($targetSource.file)
                $outputLine=$outputLine.Replace((']('+ $fileName +')'),('](#'+$targetSource.anchor+')'))
            }
            [void]$builder.AppendLine($outputLine)
        }
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('---')
        [void]$builder.AppendLine()
    }
    [System.IO.File]::WriteAllText($mergedPath,$builder.ToString(),$utf8)
    Write-Output "Merged 12 modules plus master into $($manifest.merged) ($($builder.Length) characters including code and Markdown)."
}
if ($Mode -eq 'Merge') {return}

& (Join-Path $PSScriptRoot 'Validate-PerceptionDesign.ps1')
& (Join-Path $PSScriptRoot 'Validate-LifePlanningDesign.ps1')
& (Join-Path $PSScriptRoot 'Validate-AllModulesDesign.ps1')
if (!(Test-Path -LiteralPath $deliveryDir)) {[void](New-Item -ItemType Directory -Path $deliveryDir)}
$zipName='经济需求驱动AI_全部模块方案_v1.0_20261008.zip'
$zipPath=Join-Path $deliveryDir $zipName
if (Test-Path -LiteralPath $zipPath) {
    $zipName='经济需求驱动AI_全部模块方案_v1.0_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmssfff')+'.zip'
    $zipPath=Join-Path $deliveryDir $zipName
}
$readmePath=Join-Path $deliveryDir '交付说明.md'
$readme=@'
# 全部模块设计交付说明

首先阅读：00-总纲与共享/02-代码设计总册/00-全部模块方案_合订本.md。
分模块入口：游戏代码设计/00-全部模块方案_总册.md。

本包包含全部十二模块、相关制度/历史设计、接口证据、参数样例和校验/导出脚本。属于代码设计交付，尚未实现完整 AI，不是可安装 Mod，不宣称游戏验收通过。

共同规则：RUIF 前端；原版调度与强制指令优先；机构公有；真实银币缴存支持账本；实际劳动/产品/服务证据结算；家庭需要去重；未知不当零；一次一个经济前台意图。

原版 DLL、反编译源码和完整 RUIF 工程未附带。证据 JSON 中的绝对源码/DLL路径及原始用户参考文档 D:/QQ/Pawn经济AI核心方案-1.md 需要在原工作环境定位；正文已完整收录，不依赖这些文件才能阅读方案。各工具重跑需该环境及现有 Mono.Cecil，不能把文档链接检查等同实际游戏验证。

交付清单.json 记录包内源文件指纹；ZIP 外的 .sha256 文件记录压缩包指纹。合订本由分模块文件生成，后续修订编辑源文件再导出。
'@
[System.IO.File]::WriteAllText($readmePath,$readme,$utf8)
$extensions=@('.md','.json','.ps1','.py','.cs','.png','.svg','.txt')
$sourceFiles=@(Get-ChildItem -LiteralPath $designRoot -Recurse -File | Where-Object {
    $_.Extension.ToLowerInvariant() -in $extensions -and
    !$_.FullName.StartsWith($deliveryDir+[System.IO.Path]::DirectorySeparatorChar,[System.StringComparison]::OrdinalIgnoreCase) -and
    $_.FullName -notmatch '[\\/](bin|obj|/.git)[\\/]'
} | Sort-Object FullName)
$specs=@(foreach($file in $sourceFiles) {
    $entryName=$file.FullName.Substring($designRoot.TrimEnd('\','/').Length+1).Replace('\','/')
    [pscustomobject]@{entry=$entryName;source=$file.FullName;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
})
$specs+=[pscustomobject]@{entry='交付说明.md';source=$readmePath;sha256=(Get-FileHash -LiteralPath $readmePath).Hash}
if (@($specs.entry | Select-Object -Unique).Count -ne $specs.Count) {throw 'Duplicate package entries'}
$deliveryManifestPath=Join-Path $deliveryDir '交付清单.json'
$deliveryManifest=[ordered]@{version='1.0';stage='design_only';moduleCount=12;createdUtc=[DateTime]::UtcNow.ToString('o');archive=$zipName;mergedSha256=(Get-FileHash -LiteralPath $mergedPath).Hash;files=@($specs | ForEach-Object {[ordered]@{entry=$_.entry;sha256=$_.sha256}});runtimeCompiled=$false;gameValidated=$false}
$deliveryManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $deliveryManifestPath -Encoding UTF8
$specs+=[pscustomobject]@{entry='交付清单.json';source=$deliveryManifestPath;sha256=(Get-FileHash -LiteralPath $deliveryManifestPath).Hash}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[System.IO.Compression.ZipFile]::Open($zipPath,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($spec in $specs) {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$spec.source,$spec.entry,[System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally {$zip.Dispose()}
$zip=[System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if ($zip.Entries.Count -ne $specs.Count) {throw 'ZIP entry count mismatch'}
    foreach($spec in $specs) {
        $entry=$zip.GetEntry($spec.entry)
        if (!$entry) {throw "ZIP entry not found: $($spec.entry)"}
        $stream=$entry.Open();$hasher=[System.Security.Cryptography.SHA256]::Create()
        try {$hash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','')}
        finally {$hasher.Dispose();$stream.Dispose()}
        if ($hash -ne $spec.sha256) {throw "ZIP content mismatch: $($spec.entry)"}
    }
} finally {$zip.Dispose()}
$zipHash=(Get-FileHash -LiteralPath $zipPath).Hash
[System.IO.File]::WriteAllText($zipPath+'.sha256',($zipHash+'  '+$zipName+[Environment]::NewLine),$utf8)
Write-Output "Verified all $($specs.Count) ZIP entries by SHA-256."
Write-Output "Archive: $zipPath"
Write-Output "SHA256: $zipHash"
