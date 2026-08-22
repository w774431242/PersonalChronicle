$ErrorActionPreference = "Stop"
$managed = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"

$asm = [System.Reflection.Assembly]::LoadFrom("$managed\Assembly-CSharp.dll")
$textType = $asm.GetType("Verse.Text")

# Is fontStyles field writable?
$fld = $textType.GetField("fontStyles", [System.Reflection.BindingFlags]"Public,Static")
Write-Output "fontStyles: IsInitOnly=$($fld.IsInitOnly) FieldType=$($fld.FieldType.FullName)"
$fld2 = $textType.GetField("SmallFontHeight", [System.Reflection.BindingFlags]"Public,Static")
Write-Output "SmallFontHeight: IsInitOnly=$($fld2.IsInitOnly)"

# CurFontStyle is a method -> look at get_CurFontStyle return type
$p = $textType.GetProperty("CurFontStyle", [System.Reflection.BindingFlags]"Public,Static")
Write-Output "CurFontStyle return: $($p.PropertyType.FullName)"

# UnityEngine.GUIStyle -> font property?
$guitype = [System.Type]::GetType("UnityEngine.GUIStyle, UnityEngine.IMGUIModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
if (-not $guitype) {
    foreach ($f in Get-ChildItem "$managed\*.dll") {
        try { $t = [System.Reflection.Assembly]::LoadFrom($f.FullName) } catch { continue }
        $g = $t.GetType("UnityEngine.GUIStyle")
        if ($g) { $guitype = $g; Write-Output "GUIStyle found in $($t.GetName().Name)"; break }
    }
}
if ($guitype) {
    foreach ($prop in $guitype.GetProperties([System.Reflection.BindingFlags]"Public,Instance")) {
        if ($prop.Name -match "font|Font") { Write-Output "GUIStyle.PROP: $($prop.Name) : $($prop.PropertyType.FullName)" }
    }
}

# UnityEngine.Font type + how to load (Resources.Load? Font.CreateDynamicFontFromOSFont?)
foreach ($f in Get-ChildItem "$managed\*.dll") {
    try { $t = [System.Reflection.Assembly]::LoadFrom($f.FullName) } catch { continue }
    $fontType = $t.GetType("UnityEngine.Font")
    if ($fontType) {
        Write-Output "=== UnityEngine.Font found in $($t.GetName().Name) ==="
        foreach ($m in $fontType.GetMethods([System.Reflection.BindingFlags]"Public,Static")) {
            Write-Output "STATIC: $($m.Name)"
        }
        foreach ($m in $fontType.GetMethods([System.Reflection.BindingFlags]"Public,Instance")) {
            if ($m.Name -match "Create|Request|GetOS") { Write-Output "INSTANCE: $($m.Name)" }
        }
        break
    }
}

# Can we find a font asset under RimWorld/Data? Search for .ttf/.otf
Write-Output "=== Font asset files under game data ==="
$dataRoot = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data"
Get-ChildItem -Path $dataRoot -Recurse -Include *.ttf,*.otf -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { Write-Output $_.FullName }
