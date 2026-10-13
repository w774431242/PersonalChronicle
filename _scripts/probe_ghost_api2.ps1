# Reflect full members of ghost-related types (namespaced).
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

foreach ($tn in @('Verse.GhostDrawer', 'Verse.GhostUtility', 'RimWorld.Designator_Place', 'Verse.Graphic')) {
    $t = $asm.GetType($tn)
    if ($null -eq $t) { Write-Host "== $tn : NOT FOUND"; continue }
    Write-Host ""
    Write-Host "== $tn =="
    try { $t.GetMethods($flags) | ForEach-Object { Write-Host ("  M " + $_.ToString()) } } catch { Write-Host ("  err " + $_.Exception.Message) }
    try { $t.GetFields($flags) | ForEach-Object { Write-Host ("  F " + $_.ToString()) } } catch {}
    try { $t.GetProperties($flags) | ForEach-Object { Write-Host ("  P " + $_.ToString()) } } catch {}
}
[System.AppDomain]::CurrentDomain.remove_AssemblyResolve($OnResolve)
