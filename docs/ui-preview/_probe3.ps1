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

# Find.WorldGrid 类型
$findType = $asm.GetType("Verse.Find", $false)
if ($findType) {
    Write-Output "==== Verse.Find WorldGrid/World props ===="
    foreach ($p in $findType.GetProperties()) {
        if ($p.Name -match "World") { Write-Output ("  PROP " + $p.PropertyType.FullName + " " + $p.Name) }
    }
}

# WorldGrid 真实类型：从 Find 属性
$wgProp = $findType.GetProperty("WorldGrid")
if ($wgProp) {
    $wgType = $wgProp.PropertyType
    Write-Output ("==== " + $wgType.FullName + " ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($p in $wgType.GetProperties($flags)) { Write-Output ("  PROP " + $p.PropertyType.Name + " " + $p.Name) }
    foreach ($m in $wgType.GetMethods($flags) | Where-Object { -not $_.IsSpecialName }) { Write-Output ("  METH " + $m.ReturnType.Name + " " + $m.Name + "(" + (($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ",") + ")") }
}

# Tile struct 完整字段
$tileType = $asm.GetType("Verse.Tile", $false)
if ($tileType) {
    Write-Output "==== Verse.Tile fields/props (declared) ===="
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($f in $tileType.GetFields($flags)) { Write-Output ("  FIELD " + $f.FieldType.Name + " " + $f.Name) }
    foreach ($p in $tileType.GetProperties($flags)) { Write-Output ("  PROP " + $p.PropertyType.Name + " " + $p.Name) }
}
