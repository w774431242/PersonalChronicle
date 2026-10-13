# Reverse-sync: publish folder -> dev tree for Languages/Defs/Patches/About/Textures.
# Overwrites dev files that differ or are missing (publish = 08-29/08-30 working state, newer than HEAD).
# Files existing only in dev are kept. ASCII only.
$ErrorActionPreference = 'Stop'
$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$pub  = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统'
$copied = 0
foreach ($d in @('Languages','Defs','Patches','About','Textures')) {
    $srcDir = Join-Path $root $d
    $pubDir = Join-Path $pub $d
    if (-not (Test-Path $pubDir)) { continue }
    Get-ChildItem $pubDir -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($pubDir.Length + 1)
        $dst = Join-Path $srcDir $rel
        if (-not (Test-Path $dst)) {
            $dd = Split-Path $dst -Parent
            if (-not (Test-Path $dd)) { New-Item -ItemType Directory -Path $dd -Force | Out-Null }
            Copy-Item $_.FullName $dst -Force
            $script:copied++
            Write-Host ("new:    " + $d + "\" + $rel)
        }
        else {
            $h1 = (Get-FileHash $_.FullName -Algorithm MD5).Hash
            $h2 = (Get-FileHash $dst -Algorithm MD5).Hash
            if ($h1 -ne $h2) {
                Copy-Item $_.FullName $dst -Force
                $script:copied++
                Write-Host ("upd:    " + $d + "\" + $rel)
            }
        }
    }
}
Write-Host ("total copied back: " + $copied)
