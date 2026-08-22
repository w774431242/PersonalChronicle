$ErrorActionPreference = 'SilentlyContinue'
$asmPath = "E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($asmPath)

# IRenameable 全部成员
$ir = $asm.GetType("Verse.IRenameable")
Write-Host "== IRenameable members =="
foreach ($m in $ir.GetMembers([System.Reflection.BindingFlags]"Public,Instance")) {
    if ($m.MemberType -eq 'Method') {
        $ps = ($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ","
        Write-Host ("  " + $m.MemberType + " " + $m.Name + "(" + $ps + ")")
    } else {
        Write-Host ("  " + $m.MemberType + " " + $m.Name)
    }
}

# Dialog_Rename`1 构造与方法
$dr = $asm.GetType("Verse.Dialog_Rename`1")
if ($dr) {
    Write-Host "== Dialog_Rename`1 =="
    foreach ($c in $dr.GetConstructors([System.Reflection.BindingFlags]"Public,NonPublic,Instance")) {
        $ps = ($c.GetParameters() | ForEach-Object { $_.ParameterType.Name + " " + $_.Name }) -join ","
        Write-Host ("  ctor(" + $ps + ")")
    }
    foreach ($m in $dr.GetMethods([System.Reflection.BindingFlags]"Public,Instance")) {
        Write-Host ("  method " + $m.Name)
    }
    Write-Host ("  generic args: " + ($dr.GetGenericArguments() | ForEach-Object { $_.Name }))
}

# 原版怎么打开改名：找 RenameUIUtility.DrawRenameButton 的实现引用的对话框
$ru = $asm.GetType("RimWorld.RenameUIUtility")
if ($ru) {
    $m = $ru.GetMethod("DrawRenameButton", [System.Reflection.BindingFlags]"Public,Static", $null, @([System.Reflection.Emit.OpCode].GetElementType(), $asm.GetType("Verse.IRenameable")), $null)
}
