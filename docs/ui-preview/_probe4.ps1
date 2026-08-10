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

# PlanetTile struct 构造与转换
$pt = $asm.GetType("RimWorld.Planet.PlanetTile", $false)
if ($pt) {
    Write-Output "==== RimWorld.Planet.PlanetTile ===="
    foreach ($m in $pt.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly)) {
        if ($m.Name -match "op_|ctor") {
            Write-Output ("  " + $m.Name + "(" + (($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ",") + ") : " + $m.ReturnType.Name)
        }
    }
    Write-Output "  fields:"
    foreach ($f in $pt.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly)) {
        Write-Output ("    " + $f.FieldType.Name + " " + $f.Name)
    }
}

# WorldObject (base) 成员
$wo = $asm.GetType("Verse.WorldObject", $false)
if ($null -eq $wo) { $wo = $asm.GetType("RimWorld.Planet.WorldObject", $false) }
if ($wo) {
    Write-Output "==== WorldObject ===="
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($p in $wo.GetProperties($flags)) { Write-Output ("  PROP " + $p.PropertyType.Name + " " + $p.Name) }
    foreach ($f in $wo.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
}
