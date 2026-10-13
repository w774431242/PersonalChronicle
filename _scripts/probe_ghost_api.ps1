# Reflect RimWorld Assembly-CSharp for ghost drawing APIs (pwsh / .NET 8).
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

foreach ($tn in @('GhostUtility', 'Designator_Place', 'Graphic_Linked', 'GhostTexData')) {
    $t = $asm.GetType($tn)
    if ($null -eq $t) { Write-Host "== $tn : NOT FOUND"; continue }
    Write-Host ""
    Write-Host "== $tn =="
    try { $t.GetMethods($flags) | ForEach-Object { Write-Host ("  M " + $_.ToString()) } } catch { Write-Host ("  methods err: " + $_.Exception.Message) }
    try { $t.GetFields($flags) | ForEach-Object { Write-Host ("  F " + $_.ToString()) } } catch {}
    try { $t.GetProperties($flags) | ForEach-Object { Write-Host ("  P " + $_.ToString()) } } catch {}
}

Write-Host ""
Write-Host "== methods named like *Ghost* (first 100) =="
$count = 0
foreach ($t in $asm.GetTypes()) {
    if ($count -ge 100) { break }
    try {
        foreach ($m in $t.GetMethods($flags)) {
            if ($m.Name -like '*Ghost*') { Write-Host ("  " + $t.Name + "::" + $m.ToString()); $count++ }
        }
    } catch {}
}
[System.AppDomain]::CurrentDomain.remove_AssemblyResolve($OnResolve)
