$ErrorActionPreference = 'Stop'
$asmPath = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
$asm = [System.Reflection.Assembly]::LoadFrom($asmPath)

$find = $asm.GetType('Verse.Find')
if ($null -ne $find) {
    Write-Output '== Verse.Find: LetterStack =='
    $find.GetProperties([System.Reflection.BindingFlags]'Public,Static') | Where-Object { $_.Name -match 'LetterStack' } | ForEach-Object { Write-Output "  property $($_.PropertyType.FullName) $($_.Name)" }
    $find.GetFields([System.Reflection.BindingFlags]'Public,Static') | Where-Object { $_.Name -match 'LetterStack' } | ForEach-Object { Write-Output "  field $($_.FieldType.FullName) $($_.Name)" }
}
