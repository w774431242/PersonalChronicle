param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][string]$CecilPath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
# Read metadata without Assembly.Load, game execution, or static constructors.
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$assemblyPath = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$wanted = @(
    'Verse.ModsConfig','Verse.ModLister','Verse.ModMetaData','Verse.ModContentPack',
    'Verse.LoadedModManager','Verse.PlayDataLoader','Verse.DirectXmlLoader',
    'Verse.DirectXmlToObject','Verse.DirectXmlToObjectNew','Verse.XmlInheritance',
    'Verse.DirectXmlCrossRefLoader','Verse.DefDatabase`1','Verse.Def',
    'Verse.ModLoadFolders','Verse.LoadFolder','Verse.PatchOperation',
    'Verse.PatchOperationFindMod','Verse.LoadableXmlAsset',
    'RimWorld.ExpansionDef','RimWorld.ExpansionDefOf','RimWorld.ExpansionStatus'
)
$bodyMethods = @('LoadAllActiveMods','ParseAndProcessXML','InitializeMods',
    'RecacheActiveMods','IsActive','get_ShouldLoad','LoadDataFromXmlCustom',
    'ValidateMayRequires','DefFromNodeNew','DefFromNode','get_Status',
    'get_RoyaltyActive','get_IdeologyActive','get_BiotechActive','get_AnomalyActive','get_OdysseyActive')
function Read-DlcType($type) {
    $methods = @(
        foreach ($method in $type.Methods) {
            $calls = @(); $strings = @(); $il = @()
            if ($method.HasBody) {
                foreach ($instruction in $method.Body.Instructions) {
                    if ($instruction.OpCode.Name -in @('call','callvirt','newobj','ldftn','ldvirtftn')) {
                        $calls += [ordered]@{offset=$instruction.Offset; opcode=$instruction.OpCode.Name; target=$instruction.Operand.FullName}
                    }
                    if ($instruction.OpCode.Name -eq 'ldstr') { $strings += [string]$instruction.Operand }
                    if ($method.Name -in $bodyMethods) { $il += $instruction.ToString() }
                }
            }
            [ordered]@{name=$method.Name; signature=$method.FullName; public=$method.IsPublic;
                static=$method.IsStatic; calls=$calls; strings=$strings; selectedIL=$il}
        }
    )
    [ordered]@{name=$type.FullName; fields=@($type.Fields | ForEach-Object FullName);
        properties=@($type.Properties | ForEach-Object FullName); methods=$methods;
        nested=@($type.NestedTypes | ForEach-Object { Read-DlcType $_ })}
}
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $assemblyPath).Path)
try {
    $types = @(foreach ($name in $wanted) {
        $type = $assembly.MainModule.GetType($name)
        if (!$type) { throw "Required type missing in this game version: $name" }
        Read-DlcType $type
    })
    $absentProbes = @(foreach ($name in @('Verse.DlcManager','RimWorld.DlcManager','RimWorld.DlcDefOf')) {
        [ordered]@{type=$name; present=($null -ne $assembly.MainModule.GetType($name))}
    })
    $result = [ordered]@{schemaVersion=1; evidence='Cecil metadata and IL; no runtime execution';
        assemblyPath=(Resolve-Path -LiteralPath $assemblyPath).Path;
        assemblySha256=(Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash;
        assemblyIdentity=$assembly.Name.FullName; moduleMvid=$assembly.MainModule.Mvid.ToString();
        cecilSha256=(Get-FileHash -LiteralPath $CecilPath -Algorithm SHA256).Hash;
        types=$types; absentProbes=$absentProbes}
    $parent = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllText($OutputPath, ($result | ConvertTo-Json -Depth 50), [Text.UTF8Encoding]::new($false))
    Write-Output "MetadataWritten types=$($types.Count) path=$OutputPath"
} finally { $assembly.Dispose() }
