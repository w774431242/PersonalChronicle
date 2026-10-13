# Dump parameter names of GhostDrawer.DrawGhostThing + IL call targets of Designator_Place.DrawGhost.
$script:managed = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
$script:resolving = $false
$OnResolve = [System.ResolveEventHandler] {
    param($sender, $e)
    if ($script:resolving) { return $null }
    $script:resolving = $true
    try {
        $n = (New-Object System.Reflection.AssemblyName($e.Name)).Name
        $p = Join-Path $script:managed ($n + '.dll')
        if (Test-Path $p) { return [System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($p) }
    } catch {}
    finally { $script:resolving = $false }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($OnResolve)
$asm = [System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $script:managed 'Assembly-CSharp.dll'))
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'

$gd = $asm.GetType('Verse.GhostDrawer')
$mi = $gd.GetMethod('DrawGhostThing', $flags)
Write-Host "== GhostDrawer.DrawGhostThing parameters =="
foreach ($p in $mi.GetParameters()) { Write-Host ("  " + $p.ParameterType.Name + " " + $p.Name) }

Write-Host ""
Write-Host "== Designator_Place.DrawGhost IL call targets =="
$dp = $asm.GetType('RimWorld.Designator_Place')
$dg = $dp.GetMethod('DrawGhost', $flags)
$body = $dg.GetMethodBody()
$il = $body.GetILAsByteArray()
$mod = $dg.Module
# crude IL scan: opcodes 0x28 (call), 0x6F (callvirt), 0x73 (newobj) followed by 4-byte token
for ($i = 0; $i -lt $il.Length - 4; $i++) {
    $op = $il[$i]
    if ($op -eq 0x28 -or $op -eq 0x6F -or $op -eq 0x73) {
        $tok = [int]$il[$i+1] -bor ([int]$il[$i+2] -shl 8) -bor ([int]$il[$i+3] -shl 16) -bor ([int]$il[$i+4] -shl 24)
        try {
            $mb = $mod.ResolveMethod($tok)
            Write-Host ("  " + $mb.DeclaringType.Name + "::" + $mb.Name)
        } catch {}
        $i += 4
    }
}

Write-Host ""
Write-Host "== Blueprint / Sketch ghost calls (SketchThing.DrawGhost) =="
$st = $asm.GetType('RimWorld.SketchThing')
if ($null -ne $st) {
    $m2 = $st.GetMethod('DrawGhost', $flags)
    if ($null -ne $m2) {
        $il2 = $m2.GetMethodBody().GetILAsByteArray()
        for ($i = 0; $i -lt $il2.Length - 4; $i++) {
            $op = $il2[$i]
            if ($op -eq 0x28 -or $op -eq 0x6F -or $op -eq 0x73) {
                $tok = [int]$il2[$i+1] -bor ([int]$il2[$i+2] -shl 8) -bor ([int]$il2[$i+3] -shl 16) -bor ([int]$il2[$i+4] -shl 24)
                try {
                    $mb = $mod.ResolveMethod($tok)
                    if ($mb.DeclaringType.Name -like '*Ghost*') { Write-Host ("  " + $mb.DeclaringType.Name + "::" + $mb.Name) }
                } catch {}
                $i += 4
            }
        }
    }
}
[System.AppDomain]::CurrentDomain.remove_AssemblyResolve($OnResolve)
