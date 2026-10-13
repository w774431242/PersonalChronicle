# Probe RimWorld 1.6 engine API signatures for planned Harmony patches. ASCII only.
$ErrorActionPreference = 'Continue'
$managed = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
[System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll')) | Out-Null
$a = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))

$job = $a.GetType('Verse.Job')
if ($job) {
    Write-Host ('Job.workGiverDef field : ' + (($job.GetField('workGiverDef') -ne $null)))
    Write-Host ('Job.workGiver field    : ' + (($job.GetField('workGiver') -ne $null)))
    $jt = $a.GetType('Verse.Pawn_JobTracker')
    if ($jt) {
        $jt.GetMethods() | Where-Object { $_.Name -eq 'EndJob' } | ForEach-Object {
            Write-Host ('Pawn_JobTracker.EndJob : ' + $_.ToString())
        }
    }
}
$frame = $a.GetType('RimWorld.Frame')
if ($frame) {
    $m = $frame.GetMethod('CompleteConstruction')
    if ($m) { Write-Host ('Frame.CompleteConstruction : ' + $m.ToString()) }
    else { Write-Host 'Frame.CompleteConstruction : NOT FOUND' }
}
$rm = $a.GetType('RimWorld.ResearchManager')
if ($rm) {
    $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance'
    $rm.GetMethods($flags) | Where-Object { $_.Name -like '*esearch*' } | ForEach-Object {
        Write-Host ('ResearchManager.' + $_.Name + ' : ' + $_.ToString())
    }
}
