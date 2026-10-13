# Probe ResearchManager internals. ASCII only.
$ErrorActionPreference = 'Continue'
$managed = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
[System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll')) | Out-Null
$a = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance'

$rm = $a.GetType('RimWorld.ResearchManager')
$rm.GetFields($flags) | Where-Object { $_.Name -like '*urrent*' } | ForEach-Object {
    Write-Host ('field: ' + $_.FieldType.Name + ' ' + $_.Name + '  public=' + $_.IsPublic)
}
$rm.GetMethods($flags) | Where-Object { $_.Name -eq 'FinishProject' } | ForEach-Object {
    Write-Host ('method: ' + $_.ToString())
}

$rpd = $a.GetType('RimWorld.ResearchProjectDef')
$rpd.GetProperties() | Where-Object { $_.Name -like '*inish*' -or $_.Name -like '*rogress*' } | ForEach-Object {
    Write-Host ('prop: ' + $_.Name + '  (' + $_.PropertyType.Name + ')')
}
