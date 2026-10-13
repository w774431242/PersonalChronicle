$ErrorActionPreference = 'Stop'
$assetRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assetRecords = Join-Path $assetRoot 'docs/验证/公有资产与机构资金贯通_20261011'
$assetMod = Join-Path $assetRoot 'Assemblies/PersonalChronicle.dll'
$assetTestMod = Join-Path $assetRoot 'Tests/bin/Release/net48/PersonalChronicle.dll'
$assetReport = Join-Path $assetRoot 'Tests/TestResults/PublicEconomy-Regression.trx'
[xml]$assetResults = Get-Content -LiteralPath $assetReport -Raw -Encoding UTF8
$assetCounters = $assetResults.TestRun.ResultSummary.Counters
$assetSkipped = @($assetResults.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted).Count
if ([int]$assetCounters.failed -ne 0 -or [int]$assetCounters.passed -lt 1310 -or $assetResults.TestRun.ResultSummary.outcome -ne 'Completed') {
    throw 'Regression report does not prove a complete passing run.'
}
if ([DateTimeOffset]::Parse($assetResults.TestRun.Times.finish).UtcDateTime -lt (Get-Item -LiteralPath $assetMod).LastWriteTimeUtc) {
    throw 'Regression report predates this build.'
}
$assetHash = (Get-FileHash -LiteralPath $assetMod).Hash
if ($assetHash -ne (Get-FileHash -LiteralPath $assetTestMod).Hash) { throw 'Tested mod differs from deployment candidate.' }

# Compare actual PDB compiler source checksums, not just source modification times.
Add-Type -Path 'C:/Users/Administrator/.nuget/packages/mono.cecil/0.11.5/lib/netstandard2.0/Mono.Cecil.dll'
$assetReader = [Mono.Cecil.ReaderParameters]::new()
$assetReader.ReadSymbols = $true
$assetCompiledFiles = @()
foreach ($assetRelative in @('Assemblies/PersonalChronicle.dll','Tests/bin/Release/net48/PersonalChronicle.Tests.dll')) {
    $assetAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $assetRoot $assetRelative), $assetReader)
    try {
        $assetDocuments = @{}
        foreach ($assetType in $assetAssembly.MainModule.GetTypes()) {
            foreach ($assetMethod in $assetType.Methods) {
                foreach ($assetPoint in $assetMethod.DebugInformation.SequencePoints) {
                    $assetDocuments[$assetPoint.Document.Url] = $assetPoint.Document
                }
            }
        }
        foreach ($assetDocument in $assetDocuments.Values) {
            if (-not (Test-Path -LiteralPath $assetDocument.Url)) { throw "Missing compiled source: $($assetDocument.Url)" }
            $assetHashKind = [string]$assetDocument.HashAlgorithm
            $assetPdbHash = [BitConverter]::ToString($assetDocument.Hash).Replace('-','')
            if ((Get-FileHash -LiteralPath $assetDocument.Url -Algorithm $assetHashKind).Hash -ne $assetPdbHash) {
                throw "Source differs from compiled PDB: $($assetDocument.Url)"
            }
            $assetCompiledFiles += [ordered]@{assembly=$assetRelative;source=[IO.Path]::GetRelativePath($assetRoot,$assetDocument.Url).Replace('\','/');algorithm=$assetHashKind;hash=$assetPdbHash}
        }
    } finally { $assetAssembly.Dispose() }
}
$assetChecksumPath = Join-Path $assetRecords 'compiled-source-checksums.json'
$assetCompiledFiles | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $assetChecksumPath -Encoding UTF8

foreach ($assetLocaleFile in @('PawnEconomyPublicAssets.xml','TreasuryOperations.xml')) {
    [xml]$assetZh = Get-Content -LiteralPath (Join-Path $assetRoot "Languages/ChineseSimplified/Keyed/$assetLocaleFile") -Raw -Encoding UTF8
    [xml]$assetEn = Get-Content -LiteralPath (Join-Path $assetRoot "Languages/English/Keyed/$assetLocaleFile") -Raw -Encoding UTF8
    $assetZhKeys = @($assetZh.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name | Sort-Object)
    $assetEnKeys = @($assetEn.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name | Sort-Object)
    if (Compare-Object $assetZhKeys $assetEnKeys) { throw 'Locale keys differ.' }
    foreach ($assetKey in $assetZhKeys) {
        $assetZhArgs = @([regex]::Matches($assetZh.LanguageData.$assetKey, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        $assetEnArgs = @([regex]::Matches($assetEn.LanguageData.$assetKey, '\{\d+\}') | ForEach-Object Value | Sort-Object)
        if ($assetZhArgs.Count -ne $assetEnArgs.Count -or ($assetZhArgs.Count -and (Compare-Object $assetZhArgs $assetEnArgs))) {
            throw "Locale parameters differ: $assetKey"
        }
    }
}
$assetFiles = @((Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Source/PersonalChronicle') -Recurse -File -Filter '*.cs'),
    (Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Tests') -File -Filter '*.cs'),
    (Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Languages') -Recurse -File -Filter '*.xml'),
    (Get-ChildItem -LiteralPath (Join-Path $assetRoot 'Defs') -Recurse -File -Filter '*.xml')) | ForEach-Object { $_ }
$assetFiles += @('Source/PersonalChronicle.csproj','Tests/PersonalChronicle.Tests.csproj','About/About.xml',
    '_scripts/verify-public-assets.ps1','_scripts/deploy-public-assets.ps1') | ForEach-Object { Get-Item -LiteralPath (Join-Path $assetRoot $_) }
$assetSnapshot = @($assetFiles | Sort-Object FullName -Unique | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($assetRoot,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$assetSnapshot | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $assetRecords 'frozen-inputs.json') -Encoding UTF8
[ordered]@{
    changeId='CHG-20261011-PUBLIC-ASSET-LINKS';verifiedUtc=[DateTime]::UtcNow.ToString('o')
    testsPassed=[int]$assetCounters.passed;testsFailed=[int]$assetCounters.failed;testsSkipped=$assetSkipped
    codeVerified=$true;testVerified=$true;gameVerified=$false;modSha256=$assetHash;compiledSourceDocuments=$assetCompiledFiles.Count
    sourceFiles=$assetSnapshot
    testReports=@([ordered]@{path='Tests/TestResults/PublicEconomy-Regression.trx';sha256=(Get-FileHash -LiteralPath $assetReport).Hash},
        [ordered]@{path='docs/验证/公有资产与机构资金贯通_20261011/compiled-source-checksums.json';sha256=(Get-FileHash -LiteralPath $assetChecksumPath).Hash})
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $assetRecords 'verification.json') -Encoding UTF8
Write-Output "PASS: $($assetCounters.passed) passed; $($assetCounters.failed) failed; $assetSkipped skipped; $($assetCompiledFiles.Count) PDB source documents match; bilingual keys match; DLL $assetHash."
