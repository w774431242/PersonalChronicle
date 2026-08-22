$asm = [System.Reflection.Assembly]::LoadFrom('E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll')
$t = $asm.GetType('Verse.Thing')
$f = $t.GetField('thingIDNumber', [System.Reflection.BindingFlags]'NonPublic,Instance,Public')
if ($f) { Write-Output ("thingIDNumber: IsFamily=" + $f.IsFamily + " IsPrivate=" + $f.IsPrivate + " IsAssembly=" + $f.IsAssembly + " IsPublic=" + $f.IsPublic) }
