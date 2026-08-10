$ErrorActionPreference = "Stop"
$managed = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
$handler = [System.ResolveEventHandler]{
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    $c = Join-Path $managed ($name + ".dll")
    if (Test-Path $c) { return [System.Reflection.Assembly]::LoadFrom($c) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed "Assembly-CSharp.dll"))

# StockGenerator_Category / SingleDef 的 ALL 字段（含继承）+ 基类层级
foreach ($tn in @("RimWorld.StockGenerator_Category", "RimWorld.StockGenerator_SingleDef", "RimWorld.StockGenerator_Clothes", "RimWorld.StockGenerator_Category_Scarce")) {
    $t = $asm.GetType($tn, $false)
    if ($null -eq $t) { Write-Output "TYPE $tn : NOT FOUND"; continue }
    Write-Output ("==== " + $tn + "  hierarchy: " + $t.FullName + " : " + $t.BaseType.FullName + " ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::FlattenHierarchy
    foreach ($f in $t.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
}

# Tile.temperature / elevation / PrimaryBiome 确认
$tileType = $asm.GetType("Verse.Tile", $false)
if ($tileType) {
    Write-Output ("==== Verse.Tile all fields ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($f in $tileType.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
}

# Settlement trader 字段
$settleType = $asm.GetType("RimWorld.Planet.Settlement", $false)
if ($settleType) {
    Write-Output ("==== Settlement all members (declared) ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($f in $settleType.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
    foreach ($p in $settleType.GetProperties($flags)) { Write-Output ("  PROP " + $p.PropertyType.Name + " " + $p.Name) }
}
