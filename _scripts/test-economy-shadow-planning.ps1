$ErrorActionPreference = 'Stop'
$planningRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$planningEvidencePath = Join-Path $planningRoot 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B3.1_实装验证.json'
$planningInputs = (Get-Content -LiteralPath $planningEvidencePath -Raw -Encoding UTF8 | ConvertFrom-Json).sourceFiles
$planningBefore = @{}
foreach ($planningFile in $planningInputs) { $planningBefore[$planningFile.path] = (Get-FileHash -LiteralPath (Join-Path $planningRoot $planningFile.path)).Hash }
$planningBeforeDll = (Get-FileHash -LiteralPath (Join-Path $planningRoot 'Assemblies/PersonalChronicle.dll')).Hash
$planningStarted = [DateTime]::UtcNow.ToString('o')
& dotnet test (Join-Path $planningRoot 'Tests/PersonalChronicle.Tests.csproj') -c Release --no-restore --logger 'trx;LogFileName=EconomyLanding-B3-Full.trx' -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Full regression failed' }
if ((Get-FileHash -LiteralPath (Join-Path $planningRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $planningBeforeDll) { throw 'DLL changed during test; report not bound' }
if ((Get-FileHash -LiteralPath (Join-Path $planningRoot 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash -ne $planningBeforeDll) { throw 'Test host DLL differs' }
foreach ($planningFile in $planningInputs) {
    if ((Get-FileHash -LiteralPath (Join-Path $planningRoot $planningFile.path)).Hash -ne $planningBefore[$planningFile.path]) { throw "Source changed during test: $($planningFile.path)" }
}
[ordered]@{ startedUtc=$planningStarted; finishedUtc=[DateTime]::UtcNow.ToString('o'); modSha256=$planningBeforeDll;
    trxSha256=(Get-FileHash -LiteralPath (Join-Path $planningRoot 'Tests/TestResults/EconomyLanding-B3-Full.trx')).Hash;
    sourceHashes=$planningBefore; beforeAfterHashesMatched=$true } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $planningRoot '_verify_tmp/Economy-B3-tested-build.json') -Encoding UTF8
