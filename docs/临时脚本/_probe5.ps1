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

foreach ($tn in @("RimWorld.StockGenerator_Category", "RimWorld.StockGenerator_SingleDef")) {
    $t = $asm.GetType($tn, $false)
    if ($null -eq $t) { Write-Output "TYPE $tn NOT FOUND"; continue }
    Write-Output ("==== " + $tn + " base=" + $t.BaseType.FullName + " ====")
    $flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly
    foreach ($f in $t.GetFields($flags)) {
        $vis = if ($f.IsPublic) { "public" } elseif ($f.IsFamily) { "protected" } else { "nonpublic" }
        Write-Output ("  FIELD [" + $vis + "] " + $f.FieldType.FullName + " " + $f.Name)
    }
}
