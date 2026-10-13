<#
  准备殖民地综合管理系统 0.1 的发行目录、ZIP、SHA256 清单。
  -NewItem 要求目标未绑定工坊；已绑定时拒绝运行，不清掉已有 ID。
  后续更新不传 -NewItem，部署仍保持目标端工坊关联。
#>
param([switch]$NewItem)
$ErrorActionPreference = 'Stop'
$dev = (Get-Item -LiteralPath (Split-Path -Parent $PSScriptRoot)).FullName
$target = Join-Path (Split-Path -Parent (Split-Path -Parent $dev)) 'Mods\殖民地综合管理系统'
$out = Join-Path $dev '_releases\殖民地综合管理系统_0.1'
[xml]$about = Get-Content -LiteralPath (Join-Path $dev 'About\About.xml') -Raw
if ($about.ModMetaData.packageId -ne 'colony.integratedmanagement' -or
    $about.ModMetaData.name -ne '殖民地综合管理系统' -or $about.ModMetaData.modVersion -ne '0.1') {
    throw '开发区元数据与当前发行身份不一致。'
}
if (Test-Path -LiteralPath (Join-Path $dev 'About\PublishedFileId.txt')) {
    throw '开发区存在工坊关联文件；请留档移出，防止跨账号误用。'
}
if (Test-Path -LiteralPath $target) {
    if (-not (Test-Path -LiteralPath (Join-Path $target 'About\About.xml'))) { throw '目标已存在但缺少可核对的身份，拒绝覆盖。' }
    [xml]$existing = Get-Content -LiteralPath (Join-Path $target 'About\About.xml') -Raw
    if ($existing.ModMetaData.packageId -ne $about.ModMetaData.packageId) { throw '目标属于其他模组。' }
} else {
    New-Item -ItemType Directory -Path (Join-Path $target 'About') | Out-Null
    Copy-Item -LiteralPath (Join-Path $dev 'About\About.xml') -Destination (Join-Path $target 'About\About.xml')
}
$association = Join-Path $target 'About\PublishedFileId.txt'
if ($NewItem -and (Test-Path -LiteralPath $association)) { throw '目标已有工坊 ID，禁止作为新条目处理。' }
$associationHash = if (Test-Path -LiteralPath $association) { (Get-FileHash -LiteralPath $association).Hash } else { $null }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$global:LASTEXITCODE = 0
& (Join-Path $PSScriptRoot 'deploy-to-game.ps1') -Target $target *>&1 |
    Out-File -LiteralPath (Join-Path $out '部署日志.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw '部署脚本失败。' }

$files = @(Get-ChildItem -LiteralPath $target -File -Recurse -Force)
$forbidden = @($files | Where-Object {
    $_.FullName.Substring($target.Length + 1) -match '(^|[\\/])(_backups|_backup|Source|Tests|docs|_scripts|\.git|bin|obj)([\\/]|$)' -or
    $_.Name -match '\.bak(?:_|$)|\.locked_|\.pdb$|\.old$|\.orig$|\.log$'
})
if ($forbidden.Count) { throw "发行目录含禁止内容：$($forbidden[0].FullName)" }
$manifest = @(foreach ($file in $files) {
    $relative = $file.FullName.Substring($target.Length + 1)
    if ($relative -notmatch '^(Assemblies\\[^\\]+\.dll|About\\(About\.xml|Preview\.png|PublishedFileId\.txt|Screens\\.+\.png)|Defs\\.+\.xml|Languages\\.+\.xml|Patches\\.+\.xml|Textures\\.+\.(png|jpg)|Fonts\\.+\.(ttf|otf|txt)|README\.md)$') {
        throw "发行目录存在白名单之外的文件：$relative"
    }
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ($relative -ne 'About\PublishedFileId.txt') {
        $source = Join-Path $dev $relative
        if (-not (Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source).Hash -ne $hash) {
            throw "发行文件与开发区不同：$relative"
        }
    }
    if ($file.Extension -eq '.xml') { [xml](Get-Content -LiteralPath $file.FullName -Raw) | Out-Null }
    [pscustomobject]@{ RelativePath = $relative; Bytes = $file.Length; SHA256 = $hash }
})
if ($associationHash) {
    if ((Get-FileHash -LiteralPath $association).Hash -ne $associationHash) { throw '目标工坊 ID 被修改。' }
} elseif (Test-Path -LiteralPath $association) { throw '无关联部署期间出现工坊 ID，需检查。' }
if ((Get-Item -LiteralPath (Join-Path $target 'About\Preview.png')).Length -ge 1000000) { throw '封面超过保守的 1 MB 上传预算。' }
$dll = Join-Path $target 'Assemblies\PersonalChronicle.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -ne '0.1.0.0') { throw 'DLL 发行版本未更新。' }
$bytes = [long](($files | Measure-Object Length -Sum).Sum)
if ($bytes -gt 50MB) { throw '发行目录超过 50 MiB，需先复核体积。' }
$manifest | Export-Csv -LiteralPath (Join-Path $out '发行文件_SHA256.csv') -NoTypeInformation -Encoding utf8
$zip = Join-Path $out '殖民地综合管理系统_0.1_发行包.zip'
Compress-Archive -LiteralPath $target -DestinationPath $zip -CompressionLevel Optimal -Force
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$zipHash  殖民地综合管理系统_0.1_发行包.zip" | Set-Content -LiteralPath ($zip + '.sha256.txt') -Encoding utf8
$report = [ordered]@{
    Name = $about.ModMetaData.name; Version = '0.1'; PackageId = $about.ModMetaData.packageId;
    Author = $about.ModMetaData.author; Target = $target; Files = $files.Count; Bytes = $bytes;
    WorkshopIdPresent = (Test-Path -LiteralPath $association); NewItemMode = [bool]$NewItem;
    FileHashesVerified = $true; XMLParsed = $true; NoForbiddenFiles = $true;
    Zip = $zip; ZipSHA256 = $zipHash; RuntimeTested = $false; Uploaded = $false;
    PreparedAt = (Get-Date -Format o)
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out '封装校验报告.json') -Encoding utf8
Write-Output ('发行目录：{0}；{1} files / {2:N3} MiB' -f $target, $files.Count, ($bytes / 1MB))
Write-Output "工坊 ID 已存在：$(Test-Path -LiteralPath $association)；新上传模式：$NewItem"
Write-Output "发行包：$zip"
Write-Output "SHA256：$zipHash"
