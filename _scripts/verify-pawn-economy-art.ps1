param([string]$ImageReport = 'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08/图像检查.json')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root '../../RimWorldWin64_Data/Managed/Assembly-CSharp.dll'))
try {
    $graphic = $game.MainModule.GetType('Verse.Graphic_Multi')
    $rotation = $graphic.Methods | Where-Object Name -eq 'get_ShouldDrawRotated'
    if (-not @($rotation.Body.Instructions | Where-Object { [string]$_.Operand -match 'GraphicData::drawRotated' }).Count) { throw 'Native Graphic_Multi rotation contract changed' }
    $dataFields = @($game.MainModule.GetType('Verse.GraphicData').Fields | ForEach-Object Name)
    $def = [xml](Get-Content -Raw -LiteralPath (Join-Path $root 'Defs/PawnEconomy_Institutions.xml'))
    $base = $def.Defs.ThingDef | Where-Object Name -eq 'PC_PEA_CounterBase'
    if ($base.size -ne '(1,2)' -or $base.rotatable -ne 'true' -or $base.passability -ne 'PassThroughOnly') { throw 'Footprint/interaction changed' }
    if ($base.graphicData.graphicClass -ne 'Graphic_Multi' -or $base.graphicData.drawRotated -ne 'false' -or $base.graphicData.drawSize -ne '(2,2)' -or $base.graphicData.shaderType -ne 'Cutout') { throw 'Expected four-view graphic config' }
    foreach ($field in $base.graphicData.ChildNodes | Where-Object NodeType -eq Element) { if ($field.Name -notin $dataFields) { throw "Unknown native GraphicData field: $($field.Name)" } }
    $types = @('LaborOffice','WorkshopOffice','SettlementOffice','Shop','ExamCenter')
    $images = Get-Content -Raw -LiteralPath (Join-Path $root $ImageReport) | ConvertFrom-Json
    if (-not $images.checksPassed) { throw 'Image/group geometry gate has not passed' }
    foreach ($type in $types) {
        $name = 'PC_PEA_' + $type
        $node = $def.Defs.ThingDef | Where-Object defName -eq $name
        $path = 'Things/Building/PawnEconomy/' + $name
        if ($node.Count -gt 1 -or $null -eq $node -or $node.ParentName -ne 'PC_PEA_CounterBase' -or $node.graphicData.texPath -ne $path -or $node.uiIconPath -ne ($path+'_north')) { throw "Graphic/icon identity mismatch: $name" }
        $northHash = (Get-FileHash -LiteralPath (Join-Path $root ('Textures/' + $path + '_north.png'))).Hash
        foreach ($direction in @('north','east','south','west')) {
            $file = Join-Path $root ('Textures/' + $path + '_' + $direction + '.png')
            $image = @($images.files | Where-Object { $_.defName -eq $name -and $_.direction -eq $direction })
            if ($image.Count -ne 1 -or $image[0].sha256 -ne (Get-FileHash -LiteralPath $file).Hash -or -not $image[0].checksPassed) { throw "Image checks missing/stale: $name $direction" }
            if ($direction -ne 'north' -and $image[0].northReferenceSha256 -ne $northHash) { throw 'Direction references a different north master' }
        }
    }
    if ($images.files.Count -ne 20 -or @($images.files.sha256 | Sort-Object -Unique).Count -ne 20) { throw 'Expected 20 independently generated views' }
    $review = Get-Content -Raw -LiteralPath (Join-Path $root 'docs/设计文档/功能模块/资产规范/05-经济机构_2026-10-08/逐向复核.json') | ConvertFrom-Json
    foreach ($image in $images.files) {
        $item = @($review.assets | Where-Object { $_.defName -eq $image.defName -and $_.direction -eq $image.direction })
        if ($item.Count -ne 1 -or $item[0].sha256 -ne $image.sha256 -or $item[0].result -ne 'PASS') { throw "Visual direction review missing/stale: $($image.defName) $($image.direction)" }
    }
    # Deep-compare all original nonvisual Def content, including jobs and medical WorkGiver.
    $original = [xml](Get-Content -Raw -LiteralPath (Join-Path $root '_backups/economy-art_20261008/Defs/PawnEconomy_Institutions.xml'))
    foreach ($document in @($def,$original)) {
        foreach ($node in @($document.SelectNodes('/Defs/ThingDef/graphicData | /Defs/ThingDef/uiIconPath | /Defs/ThingDef/uiIconScale'))) { $null=$node.ParentNode.RemoveChild($node) }
    }
    if ($def.OuterXml -ne $original.OuterXml) { throw 'Nonvisual Def content changed' }
    Write-Host 'PASS: 20 unique RGBA views, five north-master sets, explicit graphic/icon binding, native Graphic_Multi, unchanged footprint and nonvisual Def content. In-game placement pending.'
} finally { $game.Dispose() }
