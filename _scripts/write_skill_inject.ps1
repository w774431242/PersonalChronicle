$ErrorActionPreference = "Stop"
$root = "E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统"
$cnPath = Join-Path $root "Languages\ChineseSimplified\Keyed\Archive.xml"
$text = [IO.File]::ReadAllText($cnPath)
$map = @{}
$desc = @{}
[regex]::Matches($text, '<Professional\.Skill\.(ProfessionalSkill_[^.<]+)\.Label>([^<]*)</Professional\.Skill\.\1\.Label>') | ForEach-Object {
    $map[$_.Groups[1].Value] = $_.Groups[2].Value
}
[regex]::Matches($text, '<Professional\.Skill\.(ProfessionalSkill_[^.<]+)\.Desc>([^<]*)</Professional\.Skill\.\1\.Desc>') | ForEach-Object {
    $desc[$_.Groups[1].Value] = $_.Groups[2].Value
}
$rows = New-Object System.Collections.Generic.List[string]
foreach ($k in ($map.Keys | Sort-Object)) {
    $rows.Add("  <$k.label>$($map[$k])</$k.label>") | Out-Null
    if ($desc.ContainsKey($k)) {
        $rows.Add("  <$k.description>$($desc[$k])</$k.description>") | Out-Null
    }
}
$dir2 = Join-Path $root "Languages\ChineseSimplified\DefInjected\ProfessionalSkillDef"
New-Item -ItemType Directory -Force -Path $dir2 | Out-Null
$body = "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<LanguageData>`n" + ($rows -join "`n") + "`n</LanguageData>`n"
[IO.File]::WriteAllText((Join-Path $dir2 "Skills.xml"), $body)
Write-Host "wrote $($rows.Count) rows for $($map.Count) skills"
