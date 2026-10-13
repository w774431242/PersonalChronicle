$ErrorActionPreference = 'Stop'
$cfgPath = 'C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml'
$wsRoot = 'E:\SteamLibrary\steamapps\workshop\content\294100'
$localRoot = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods'
$outDir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\_scripts\_mod_audit'
$outCsv = Join-Path $outDir 'mods_raw.csv'

[xml]$cfg = Get-Content $cfgPath -Encoding UTF8
$active = @($cfg.ModsConfigData.activeMods.li)

$meta = @{}

function Read-About([string]$aboutPath, [string]$sourceId, [bool]$isWorkshop) {
    if (-not (Test-Path $aboutPath)) { return }
    try {
        [xml]$a = Get-Content $aboutPath -Encoding UTF8
        $pkg = [string]$a.ModMetaData.packageId
        if (-not $pkg) { return }
        $name = $a.ModMetaData.name
        if (-not $name) { $name = $pkg }
        $desc = $a.ModMetaData.description
        if ($desc -is [System.Xml.XmlElement]) { $desc = $desc.InnerText }
        $author = $a.ModMetaData.author
        if (-not $author) { $author = '' }
        $vers = @()
        if ($a.ModMetaData.supportedVersions -and $a.ModMetaData.supportedVersions.li) {
            $vers = @($a.ModMetaData.supportedVersions.li)
        }
        $deps = @()
        if ($a.ModMetaData.modDependencies -and $a.ModMetaData.modDependencies.li) {
            foreach ($d in @($a.ModMetaData.modDependencies.li)) {
                if ($d.packageId) { $deps += [string]$d.packageId }
            }
        }
        $loadAfter = @()
        if ($a.ModMetaData.loadAfter -and $a.ModMetaData.loadAfter.li) {
            $loadAfter = @($a.ModMetaData.loadAfter.li)
        }
        $meta[$pkg] = @{
            Name = [string]$name
            Desc = [string]$desc
            Author = [string]$author
            Vers = ($vers -join ';')
            Deps = ($deps -join ';')
            LoadAfter = ($loadAfter -join ';')
            Source = $(if ($isWorkshop) { 'ws:' + $sourceId } else { 'local:' + $sourceId })
        }
    } catch {
        Write-Warning "About parse failed: $aboutPath :: $($_.Exception.Message)"
    }
}

Get-ChildItem $wsRoot -Directory | ForEach-Object {
    Read-About (Join-Path $_.FullName 'About\About.xml') $_.Name $true
}
Get-ChildItem $localRoot -Directory | ForEach-Object {
    Read-About (Join-Path $_.FullName 'About\About.xml') $_.Name $false
}

function Esc([string]$s) {
    $s = $s -replace "`r?`n", ' '
    $s = $s -replace '"', '""'
    return $s
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('packageId,name,author,desc,versions,deps,loadAfter,source')
foreach ($id in $active) {
    if ($meta.ContainsKey($id)) {
        $m = $meta[$id]
        $line = '"' + $id + '","' + (Esc $m.Name) + '","' + (Esc $m.Author) + '","' + (Esc $m.Desc) + '","' + (Esc $m.Vers) + '","' + (Esc $m.Deps) + '","' + (Esc $m.LoadAfter) + '","' + (Esc $m.Source) + '"'
    } else {
        $line = '"' + $id + '","(not found)","","","","","",""'
    }
    [void]$sb.AppendLine($line)
}
[System.IO.File]::WriteAllText($outCsv, $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))
Write-Host "active=$($active.Count) mapped=$($meta.Count) -> $outCsv"
