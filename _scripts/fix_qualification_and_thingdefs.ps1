$ErrorActionPreference = 'Stop'
$enc = [System.Text.UTF8Encoding]::new($false)
$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$qualPath = "$root\Defs\QualificationDefs.xml"
$thingPath = "$root\Defs\ThingDefs.xml"

# ===================== FIX-005: majorKey for 5 Q_Precision_* =====================
$q = Get-Content $qualPath -Encoding UTF8
if (($q -join "`n") -match '<majorKey>') {
    "FIX-005: majorKey already present, skip"
} else {
    $qout = @(); $inQ = $false
    foreach ($line in $q) {
        if ($line -match '<defName>(Q_Precision_\w+)</defName>') { $inQ = $true }
        $qout += $line
        if ($inQ -and $line -match '<professionalSkillDefName>[^<]*</professionalSkillDefName>') {
            $qout += '    <majorKey>Manufacturing</majorKey>'
            $inQ = $false
        }
        if ($line -match '</PersonalChronicle.Domain.Qualification.QualificationDef>') { $inQ = $false }
    }
    [System.IO.File]::WriteAllLines($qualPath, $qout, $enc)
    "FIX-005: added <majorKey>Manufacturing</majorKey> to 5 Q_Precision_*"
}

# ===================== FIX-006 + FIX-007: ThingDefs.xml =====================
$targets = @(
    'PC_Exam_PracticeKit',
    'PC_Exam_PracticeKit_Engineering', 'PC_Exam_PracticeKit_Manufacturing',
    'PC_Exam_PracticeKit_Agriculture', 'PC_Exam_PracticeKit_Forestry',
    'PC_Exam_PracticeKit_AnimalHusbandry', 'PC_Exam_PracticeKit_Medicine',
    'PC_Exam_PracticeKit_Weapons', 'PC_Exam_PracticeKit_Mining',
    'PC_Exam_PracticeKit_Research', 'PC_Exam_PracticeKit_Cooking',
    'PC_Exam_PracticeKit_Art', 'PC_Exam_PracticeKit_Management',
    'PC_Exam_Thesis', 'PC_Exam_Diploma', 'PC_Exam_Practical', 'PC_Exam_ThesisProposal'
)
$raw = [System.IO.File]::ReadAllText($thingPath)
$text = if ($raw.Length -ge 3 -and $raw[0] -eq 239 -and $raw[1] -eq 187 -and $raw[2] -eq 191) { $raw.Substring(3) } else { $raw }
$lines = $text -split "`r?`n"
$out = @(); $cur = $null; $doneComps = @{}; $doneCat = @{}
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '<defName>(PC_Exam_PracticeKit[^<]*)</defName>') { $cur = $matches[1] }
    elseif ($line -match '<defName>(PC_Exam_Thesis|PC_Exam_Diploma|PC_Exam_Practical|PC_Exam_ThesisProposal)</defName>') { $cur = $matches[1] }
    elseif ($line -match '</ThingDef>') {
        if ($cur -and ($targets -contains $cur) -and -not $doneCat.ContainsKey($cur)) {
            $out += '    <thingCategories>'
            $out += '      <li>ItemsMisc</li>'
            $out += '    </thingCategories>'
            $doneCat[$cur] = $true
        }
        $out += $line; $cur = $null; continue
    }
    elseif ($line -match '<thingClass>Thing</thingClass>' -and $cur -and $cur.StartsWith('PC_Exam_PracticeKit') -and -not $doneComps.ContainsKey($cur)) {
        $out += '    <thingClass>ThingWithComps</thingClass>'
        $out += '    <comps>'
        $out += '      <li>'
        $out += '        <compClass>CompQuality</compClass>'
        $out += '      </li>'
        $out += '    </comps>'
        $doneComps[$cur] = $true; continue
    }
    $out += $line
}
[System.IO.File]::WriteAllLines($thingPath, $out, $enc)
"FIX-006/007: applied to ThingDefs.xml ({0} comps, {1} categories)" -f $doneComps.Count, $doneCat.Count

# ===================== XML well-formedness =====================
try { $x = New-Object System.Xml.XmlDocument; $x.Load($qualPath); "QUAL_XML_OK" } catch { "QUAL_XML_FAIL: $_" }
try { $x2 = New-Object System.Xml.XmlDocument; $x2.Load($thingPath); "THING_XML_OK" } catch { "THING_XML_FAIL: $_" }
"DEF_FIX_DONE"
