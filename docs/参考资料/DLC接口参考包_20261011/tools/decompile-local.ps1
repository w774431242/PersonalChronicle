param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Get-Command ilspycmd -ErrorAction Stop | Out-Null
$assembly = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$types = @('Verse.ModsConfig','Verse.ModLister','Verse.ModMetaData','Verse.ModContentPack',
    'Verse.LoadedModManager','Verse.PlayDataLoader','Verse.ModLoadFolders','Verse.LoadFolder',
    'Verse.DirectXmlToObjectNew','Verse.DirectXmlLoader','Verse.DirectXmlToObject',
    'Verse.DirectXmlCrossRefLoader','Verse.XmlInheritance','Verse.DefDatabase`1',
    'Verse.PatchOperationFindMod','RimWorld.ExpansionDef')
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($type in $types) {
    $output = Join-Path $OutputDirectory ($type.Replace('`','_') + '.decompiled.cs.txt')
    $content = & ilspycmd -t $type $assembly
    if ($LASTEXITCODE -ne 0) { throw "ilspycmd failed: $type" }
    [IO.File]::WriteAllLines($output, [string[]]$content, [Text.UTF8Encoding]::new($false))
}
Write-Output "DecompiledLocalOnly types=$($types.Count) assemblySha256=$((Get-FileHash -LiteralPath $assembly).Hash)"
