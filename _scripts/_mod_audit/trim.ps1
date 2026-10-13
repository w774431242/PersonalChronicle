$ErrorActionPreference = 'Stop'
$dir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\_scripts\_mod_audit'
$inCsv = Join-Path $dir 'mods_raw.csv'
$outCsv = Join-Path $dir 'mods_trim.csv'

$lines = [System.IO.File]::ReadAllLines($inCsv)
$sb = New-Object System.Text.StringBuilder
foreach ($line in $lines) {
    # packageId,name,author,desc,versions,deps,loadAfter,source  (CSV quoted)
    $cols = $line -split '","'
    if ($cols.Count -ge 4) {
        $pkg = $cols[0].TrimStart('"')
        $name = $cols[1]
        $author = $cols[2]
        $desc = $cols[3]
        if ($desc.Length -gt 160) { $desc = $desc.Substring(0, 160) }
        $desc = $desc.Replace('"', '')
        $vers = ''
        $deps = ''
        $loadAfter = ''
        $source = ''
        if ($cols.Count -ge 5) { $vers = $cols[4] }
        if ($cols.Count -ge 6) { $deps = $cols[5] }
        if ($cols.Count -ge 7) { $loadAfter = $cols[6] }
        if ($cols.Count -ge 8) { $source = $cols[7].TrimEnd('"') }
        [void]$sb.AppendLine("$pkg`t$name`t$author`t$desc`t$vers`t$deps`t$loadAfter`t$source")
    } else {
        [void]$sb.AppendLine($line)
    }
}
[System.IO.File]::WriteAllText($outCsv, $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))
Write-Host "done -> $outCsv"
