# Verify RimWorld font control APIs (Verse.Text, GameFont, custom Font loading).
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Reflection

$managed = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
$asm = [System.Reflection.Assembly]::LoadFrom("$managed\Assembly-CSharp.dll")
$uGui = $null
foreach ($f in Get-ChildItem "$managed\*.dll") {
    try { $t = [System.Reflection.Assembly]::LoadFrom($f.FullName) } catch { continue }
    if ($t.GetType("UnityEngine.TextRenderingModule.Font") -or $t.GetType("UnityEngine.Font")) { $uGui = $t }
}

# --- Verse.Text members ---
$textType = $asm.GetType("Verse.Text")
if ($textType) {
    Write-Output "=== Verse.Text ==="
    foreach ($p in $textType.GetProperties([System.Reflection.BindingFlags]"Public,Static")) {
        Write-Output ("PROP: {0} : {1}" -f $p.Name, $p.PropertyType.FullName)
    }
    foreach ($f in $textType.GetFields([System.Reflection.BindingFlags]"Public,Static")) {
        Write-Output ("FIELD: {0} : {1}" -f $f.Name, $f.FieldType.FullName)
    }
    foreach ($m in $textType.GetMethods([System.Reflection.BindingFlags]"Public,Static")) {
        if ($m.Name -match "Font|Style") { Write-Output ("METHOD: {0}" -f $m.Name) }
    }
}

# --- GameFont enum ---
$gf = $asm.GetType("Verse.GameFont")
if ($gf) {
    Write-Output "=== GameFont enum ==="
    foreach ($n in [Enum]::GetNames($gf)) { Write-Output "  $n" }
}

# --- Custom font loaders? ---
Write-Output "=== Font asset loaders (search any 'Font' return type in managed) ==="
$hits = 0
foreach ($f in Get-ChildItem "$managed\*.dll") {
    try { $t = [System.Reflection.Assembly]::LoadFrom($f.FullName) } catch { continue }
    foreach ($type in $t.GetTypes()) {
        try {
            foreach ($m in $type.GetMethods([System.Reflection.BindingFlags]"Public,Static")) {
                if ($m.ReturnType.Name -eq "Font" -and ($m.Name -match "Load|Get|Create|Find|Register")) {
                    Write-Output ("{0}.{1}" -f $m.DeclaringType.FullName, $m.Name)
                    $hits++
                    if ($hits -gt 30) { break }
                }
            }
        } catch {}
        if ($hits -gt 30) { break }
    }
    if ($hits -gt 30) { break }
}
Write-Output "TOTAL_LOADER_HITS=$hits"
