# Contract audit: find ThingDefs whose XML contract cannot satisfy what the code expects.
# Root cause pattern (from the exam-book bug): a Def missing thingClass/thingCategories/comps
# silently disables behaviour instead of erroring.
$ErrorActionPreference = 'Stop'

$defsDir = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs'
$srcDir  = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source'

# ---------- 1. Inventory every ThingDef ----------
$rows = New-Object System.Collections.Generic.List[object]
foreach ($f in Get-ChildItem -Path $defsDir -Recurse -Filter *.xml -File) {
    $x = New-Object System.Xml.XmlDocument
    try { $x.Load($f.FullName) } catch { continue }
    if ($x.Defs -eq $null) { continue }
    foreach ($td in $x.Defs.ThingDef) {
        $dn = [string]$td.defName
        if ([string]::IsNullOrEmpty($dn)) { continue }
        $tc = [string]$td.thingClass
        $parent = $td.GetAttribute('ParentName')
        $cats = @()
        if ($td.thingCategories) { $cats = @($td.thingCategories.li | ForEach-Object { [string]$_ }) }
        $compText = ''
        if ($td.comps) { $compText = ($td.comps.li | ForEach-Object { $_.OuterXml }) -join ' ' }
        $hasQuality = $compText -match 'CompQuality'
        $rows.Add([pscustomobject]@{
            File       = $f.Name
            DefName    = $dn
            ThingClass = $tc
            Parent     = $parent
            Categories = ($cats -join ',')
            HasQuality = $hasQuality
        })
    }
}

Write-Output "=== THINGDEF INVENTORY ($($rows.Count)) ==="
Write-Output ("{0,-46} {1,-22} {2,-10} {3,-14} {4}" -f 'DEFNAME', 'THINGCLASS', 'QUALITY', 'PARENT', 'CATEGORIES')
foreach ($r in $rows) {
    Write-Output ("{0,-46} {1,-22} {2,-10} {3,-14} {4}" -f $r.DefName, $r.ThingClass, $r.HasQuality, $r.Parent, $r.Categories)
}

Write-Output ""
Write-Output "=== A. thingClass=Thing (no Comp slot at all) ==="
$thingOnly = @($rows | Where-Object { $_.ThingClass -eq 'Thing' -and $_.Parent -eq '' })
foreach ($r in $thingOnly) { Write-Output ("  {0}  (file={1})" -f $r.DefName, $r.File) }
Write-Output "COUNT=$($thingOnly.Count)"

Write-Output ""
Write-Output "=== B. has thingCategories (storable by category) ==="
$withCat = @($rows | Where-Object { $_.Categories -ne '' -or $_.Parent -ne '' })
foreach ($r in $withCat) { Write-Output ("  {0}  cats=[{1}] parent=[{2}]" -f $r.DefName, $r.Categories, $r.Parent) }
Write-Output "COUNT=$($withCat.Count)"

Write-Output ""
Write-Output "=== C. Defs WITHOUT any category AND without parent (invisible to storage filters) ==="
$noCat = @($rows | Where-Object { $_.Categories -eq '' -and $_.Parent -eq '' })
foreach ($r in $noCat) { Write-Output ("  {0}  thingClass={1}" -f $r.DefName, $r.ThingClass) }
Write-Output "COUNT=$($noCat.Count)"

# ---------- 2. Code-side defName references ----------
Write-Output ""
Write-Output "=== D. defName string literals referenced from C# ==="
$refs = New-Object System.Collections.Generic.List[object]
foreach ($cs in Get-ChildItem -Path $srcDir -Recurse -Filter *.cs -File) {
    $txt = [System.IO.File]::ReadAllText($cs.FullName)
    foreach ($m in [regex]::Matches($txt, 'GetNamed(?:SilentFail)?<[^>]+>\("([^"]+)"\)')) {
        $refs.Add([pscustomobject]@{ Name = $m.Groups[1].Value; File = $cs.Name; Kind = 'typed' })
    }
    foreach ($m in [regex]::Matches($txt, 'DefDatabase<[^>]+>\.GetNamed\("([^"]+)"')) {
        $refs.Add([pscustomobject]@{ Name = $m.Groups[1].Value; File = $cs.Name; Kind = 'typed' })
    }
    # dynamic concatenations such as "PC_Exam_Textbook_" + major
    foreach ($m in [regex]::Matches($txt, '"(PC_Exam_[A-Za-z_]+_)"\s*\+')) {
        $refs.Add([pscustomobject]@{ Name = $m.Groups[1].Value + '<DYNAMIC>'; File = $cs.Name; Kind = 'prefix' })
    }
}
$uniq = $refs | Group-Object -Property Name | Sort-Object Name
foreach ($g in $uniq) {
    Write-Output ("  {0,-52} x{1}  (e.g. {2})" -f $g.Name, $g.Count, $g.Group[0].File)
}
