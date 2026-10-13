$ErrorActionPreference = 'Stop'

"==== CAPTURE LAYER: HarmonyPatch class accessibility (internal = silent-fail risk, FIX-015)"
$cap = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle\Capture'
Get-ChildItem $cap -Filter *.cs | ForEach-Object {
    $name = $_.Name
    $t = Get-Content $_.FullName -Encoding UTF8 -Raw
    $hasPatch = $t -match '\[HarmonyPatch'
    foreach ($m in [regex]::Matches($t, '(internal|public|private|sealed|abstract|static)\s+class\s+(\w+)')) {
        $acc = $m.Groups[1].Value
        $cls = $m.Groups[2].Value
        if ($acc -eq 'internal' -and $hasPatch) {
            "VIOLATION(internal+HarmonyPatch): {0} in {1}" -f $cls, $name
        }
        elseif ($acc -eq 'internal') {
            "internal(non-patch): {0} in {1}" -f $cls, $name
        }
    }
    if ($hasPatch) { "HAS_HARMONYPATCH: {0}" -f $name }
}

"==== XML BOM CHECK (first 3 bytes; EF BB BF = BOM = violation of 铁律 1.1)"
$bases = @(
    'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\About\About.xml',
    'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs',
    'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Languages'
)
function Test-BOM($path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
        "BOM_VIOLATION: {0}" -f $path
    }
}
Test-BOM 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\About\About.xml'
Get-ChildItem 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs' -Recurse -Filter *.xml | ForEach-Object { Test-BOM $_.FullName }
Get-ChildItem 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Languages' -Recurse -Filter *.xml | ForEach-Object { Test-BOM $_.FullName }

"==== DONE"
