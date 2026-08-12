# 临时反射探测 v3：从已知类型反射依赖类型真实成员
$ErrorActionPreference = "Stop"
$managed = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"

$handler = [System.ResolveEventHandler]{
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    $candidate = Join-Path $managed ($name + ".dll")
    if (Test-Path $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    $candidate2 = Join-Path $managed ($name + ".winmd")
    if (Test-Path $candidate2) { return [System.Reflection.Assembly]::LoadFrom($candidate2) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed "Assembly-CSharp.dll"))

function Dump-Props($t, $label) {
    if ($null -eq $t) { Write-Output "TYPE $label : NULL"; return }
    Write-Output ("==== " + $label + " (struct=" + $t.IsValueType + ", asm=" + $t.Assembly.GetName().Name + ") ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($f in $t.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
    foreach ($p in $t.GetProperties($flags)) { Write-Output ("  PROP " + $p.PropertyType.Name + " " + $p.Name) }
    foreach ($m in $t.GetMethods($flags) | Where-Object { -not $_.IsSpecialName }) {
        if ($m.Name -match "GetTile|IsCoastal|GetAvgAnnual") { Write-Output ("  METH " + $m.ReturnType.Name + " " + $m.Name + "(" + (($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ",") + ")") }
    }
}

# 从 Map 反射依赖类型
$mapType = $asm.GetType("Verse.Map", $false)
if ($null -eq $mapType) { Write-Output "Map NOT FOUND"; exit }
$parentProp = $mapType.GetProperty("Parent")
if ($parentProp) { Dump-Props $parentProp.PropertyType "Map.Parent -> MapParent" }
$tileInfoProp = $mapType.GetProperty("TileInfo")
if ($tileInfoProp) { Dump-Props $tileInfoProp.PropertyType "Map.TileInfo -> Tile" }
$tileProp = $mapType.GetProperty("Tile")
if ($tileProp) { Dump-Props $tileProp.PropertyType "Map.Tile -> PlanetTile" }

# WorldGrid 与 GenTemperature（试其他程序集）
$asm2 = $asm
foreach ($tn in @("Verse.WorldGrid", "Verse.GenTemperature", "Verse.WorldObject")) {
    $t = $asm2.GetType($tn, $false)
    if ($null -ne $t) { Dump-Props $t $tn } else { Write-Output "TYPE $tn : NOT FOUND in Assembly-CSharp" }
}

# StockGenerator 系列
foreach ($tn in @("RimWorld.StockGenerator", "RimWorld.StockGenerator_Category", "RimWorld.StockGenerator_Tag", "RimWorld.StockGenerator_SingleDef", "RimWorld.TraderKindDef")) {
    $t = $asm2.GetType($tn, $false)
    if ($null -ne $t) { Dump-Props $t $tn } else { Write-Output "TYPE $tn : NOT FOUND" }
}
