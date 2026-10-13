# Static verification for the exam-book upgrade.
# Checks: XML well-formedness, defName uniqueness, parent linkage, and that every
# referenced RulePackDef / SkillDef / texPath actually exists in vanilla Core.
$ErrorActionPreference = 'Stop'

$modDefs = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Defs\ThingDefs.xml'
$coreRoot = 'E:\SteamLibrary\steamapps\common\RimWorld\Data\Core\Defs'

$fail = 0

# 1. XML well-formedness
try {
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($modDefs)
    Write-Output "XML_WELLFORMED=OK"
} catch {
    Write-Output "XML_WELLFORMED=FAIL :: $($_.Exception.Message)"
    $fail++
    exit 1
}

# 2. defName uniqueness + parent linkage + collect books
$defNames = @{}
$books = New-Object System.Collections.Generic.List[object]
$nonBookNoCat = New-Object System.Collections.Generic.List[string]

foreach ($td in $xml.Defs.ThingDef) {
    $dn = $td.defName
    if ([string]::IsNullOrEmpty($dn)) { continue }
    if ($defNames.ContainsKey($dn)) {
        Write-Output "DUPLICATE_DEFNAME=$dn"
        $fail++
    }
    $defNames[$dn] = $true

    if ($dn -match '^PC_Exam_(Textbook|SkillBook|TheoryBook|ResearchDoc)(?:_[A-Za-z]+)?$') {
        $parent = $td.GetAttribute('ParentName')
        if ($parent -ne 'PC_ExamBookBase') {
            Write-Output "BOOK_MISSING_PARENT=$dn (ParentName='$parent')"
            $fail++
        }
        $books.Add([pscustomobject]@{ Name = $dn; Node = $td })
    }
}
Write-Output "BOOK_COUNT=$($books.Count)"

# 3. the abstract parent must exist and be abstract
$parentNode = $null
foreach ($td in $xml.Defs.ThingDef) {
    if ($td.GetAttribute('Name') -eq 'PC_ExamBookBase') { $parentNode = $td }
}
if ($parentNode -eq $null) {
    Write-Output "PARENT_DEF=MISSING"
    $fail++
} else {
    $abs = $parentNode.GetAttribute('Abstract')
    $tc = $parentNode.thingClass
    Write-Output "PARENT_DEF=OK (Abstract=$abs, thingClass=$tc)"
    if ($abs -ne 'True') { Write-Output "PARENT_NOT_ABSTRACT"; $fail++ }
    if ($tc -ne 'Book') { Write-Output "PARENT_THINGCLASS_WRONG=$tc"; $fail++ }
    $cats = @($parentNode.thingCategories.li)
    if ($cats -notcontains 'Books') { Write-Output "PARENT_MISSING_BOOKS_CATEGORY"; $fail++ } else { Write-Output "PARENT_CATEGORY=Books OK" }
    $compClasses = @()
    foreach ($c in $parentNode.comps.li) { $compClasses += [string]$c.InnerText }
    if (($compClasses -join '|') -notmatch 'CompQuality') { Write-Output "PARENT_MISSING_COMPQUALITY"; $fail++ } else { Write-Output "PARENT_COMPQUALITY=OK" }
}

# 4. index vanilla Core: RulePackDef names, SkillDef names, known book texPaths
$rulePacks = @{}
$skillDefs = @{}
foreach ($f in Get-ChildItem -Path $coreRoot -Recurse -Filter *.xml -File) {
    try {
        $cx = New-Object System.Xml.XmlDocument
        $cx.Load($f.FullName)
    } catch { continue }
    if ($cx.Defs -eq $null) { continue }
    foreach ($rp in $cx.Defs.RulePackDef) { if ($rp.defName) { $rulePacks[[string]$rp.defName] = $true } }
    foreach ($sd in $cx.Defs.SkillDef) { if ($sd.defName) { $skillDefs[[string]$sd.defName] = $true } }
}
Write-Output "CORE_RULEPACKS=$($rulePacks.Count) CORE_SKILLDEFS=$($skillDefs.Count)"

# known vanilla book texture families (from Core/Defs/Books/BookDefs.xml)
$knownTex = @(
    'Things/Item/Book/Textbook/Textbook',
    'Things/Item/Book/Textbook/Textbook_Open',
    'Things/Item/Book/Textbook/Textbook_Vertical',
    'Things/Item/Book/Schematic/Schematic',
    'Things/Item/Book/Schematic/Schematic_Open',
    'Things/Item/Book/Schematic/Schematic_Vertical',
    'Things/Item/Book/Novel/Novel',
    'Things/Item/Book/Novel/Novel_Open',
    'Things/Item/Book/Novel/Novel_Vertical'
)

# 5. per-book checks
$badPack = 0
$badSkill = 0
$badTex = 0
$noDoer = 0
$bookCompMissing = 0

foreach ($b in $books) {
    $n = $b.Node
    $dn = $b.Name

    # find CompProperties_Book
    $bookComp = $null
    foreach ($c in $n.comps.li) {
        if ($c.GetAttribute('Class') -eq 'CompProperties_Book') { $bookComp = $c }
    }
    if ($bookComp -eq $null) { Write-Output "NO_BOOK_COMP=$dn"; $bookCompMissing++; $fail++; continue }

    $namer = [string]$bookComp.nameMaker
    $descr = [string]$bookComp.descriptionMaker
    if (-not $rulePacks.ContainsKey($namer)) { Write-Output "BAD_NAMER=$dn -> $namer"; $badPack++; $fail++ }
    if (-not $rulePacks.ContainsKey($descr)) { Write-Output "BAD_DESCRIPTIONMAKER=$dn -> $descr"; $badPack++; $fail++ }

    # texPaths
    $tp = [string]$n.graphicData.texPath
    if ($knownTex -notcontains $tp) { Write-Output "BAD_TEXPATH=$dn -> $tp"; $badTex++; $fail++ }
    if ($bookComp.openGraphic) {
        $op = [string]$bookComp.openGraphic.texPath
        if ($knownTex -notcontains $op) { Write-Output "BAD_OPENGRAPHIC=$dn -> $op"; $badTex++; $fail++ }
    }
    if ($bookComp.verticalGraphic) {
        $vp = [string]$bookComp.verticalGraphic.texPath
        if ($knownTex -notcontains $vp) { Write-Output "BAD_VERTICALGRAPHIC=$dn -> $vp"; $badTex++; $fail++ }
    }

    # doers + skills
    $doers = @($bookComp.doers.li)
    if ($doers.Count -eq 0) { Write-Output "NO_DOERS=$dn"; $noDoer++; $fail++; continue }
    foreach ($d in $doers) {
        if ($d.GetAttribute('Class') -ne 'BookOutcomeProperties_GainSkillExp') { continue }
        $sk = @($d.skills.li)
        if ($sk.Count -eq 0) { Write-Output "NO_SKILL=$dn"; $badSkill++; $fail++; continue }
        foreach ($s in $sk) {
            $sv = [string]$s
            if (-not $skillDefs.ContainsKey($sv)) { Write-Output "BAD_SKILL=$dn -> $sv"; $badSkill++; $fail++ }
        }
    }
}

Write-Output "BAD_RULEPACK=$badPack BAD_SKILL=$badSkill BAD_TEXPATH=$badTex NO_DOERS=$noDoer NO_BOOK_COMP=$bookCompMissing"

# 6. confirm no leftover book Def still declares thingClass=Thing
$leftover = 0
foreach ($b in $books) {
    $tc = [string]$b.Node.thingClass
    if ($tc -eq 'Thing') { Write-Output "LEFTOVER_THINGCLASS=$($b.Name)"; $leftover++; $fail++ }
}
Write-Output "LEFTOVER_THINGCLASS_COUNT=$leftover"

if ($fail -eq 0) { Write-Output "RESULT=ALL_CHECKS_PASSED" } else { Write-Output "RESULT=FAILURES=$fail" }
