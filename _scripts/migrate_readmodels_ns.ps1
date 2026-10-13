# Bulk namespace migration: PersonalChronicle.Archive.ReadModels -> PersonalChronicle.Application.ReadModels
# Preserves BOM state per file. ASCII only.
$ErrorActionPreference = 'Stop'
$src = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle'
$old = 'PersonalChronicle.Archive.ReadModels'
$new = 'PersonalChronicle.Application.ReadModels'
$changed = 0
Get-ChildItem $src -Recurse -Filter *.cs | ForEach-Object {
    $p = $_.FullName
    $bytes = [System.IO.File]::ReadAllBytes($p)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $enc = New-Object System.Text.UTF8Encoding($bom)
    $txt = [System.IO.File]::ReadAllText($p)
    if ($txt.Contains($old)) {
        $txt2 = $txt.Replace($old, $new)
        [System.IO.File]::WriteAllText($p, $txt2, $enc)
        $changed++
        Write-Host ("migrated: " + $p.Substring($src.Length + 1))
    }
}
Write-Host ("total migrated: " + $changed)
