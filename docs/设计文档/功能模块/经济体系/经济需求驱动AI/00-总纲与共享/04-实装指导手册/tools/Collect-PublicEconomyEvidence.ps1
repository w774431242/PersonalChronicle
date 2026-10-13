param([string]$GameRoot='E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../../../../../..')).Path
$cecil=Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll'
Add-Type -Path $cecil
$dll=Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
try {
    $wanted=@('RimWorld.TradeDeal','RimWorld.TradeSession','RimWorld.Tradeable','RimWorld.TradeCurrency','Verse.ModSettings','Verse.GameComponent','Verse.ThingOwner')
    $types=@(foreach($name in $wanted) {
        $target=$asm.MainModule.Types | Where-Object FullName -eq $name | Select-Object -First 1
        if (!$target) { throw "Missing type: $name" }
        [ordered]@{type=$name; baseType=[string]$target.BaseType;
            properties=@($target.Properties | ForEach-Object ToString);
            methods=@($target.Methods | Where-Object { $_.IsPublic -or $_.IsFamily } | ForEach-Object ToString)}
    })
    $trade=$asm.MainModule.Types | Where-Object FullName -eq 'RimWorld.TradeDeal' | Select-Object -First 1
    $execute=$trade.Methods | Where-Object Name -eq 'TryExecute' | Select-Object -First 1
    if (!$execute -or !$execute.HasBody) {throw 'TradeDeal.TryExecute body missing'}
    $calls=@($execute.Body.Instructions | Where-Object {$_.OpCode.Code -in @('Call','Callvirt')} | ForEach-Object {
        [ordered]@{offset=$_.Offset;target=$_.Operand.ToString()}
    })
    foreach($expected in @('UpdateCurrencyCount','LimitCurrencyCountToFunds','ResolveTrade','Reset')) {
        if (!($calls | Where-Object {$_.target -match "::$expected\("})) {throw "Expected call missing: $expected"}
    }
    $paths=@(
        '_research/vanilla/Assembly-CSharp/RimWorld/TradeDeal.cs',
        '_research/vanilla/Assembly-CSharp/RimWorld/TradeSession.cs',
        '_research/vanilla/Assembly-CSharp/RimWorld/Tradeable.cs',
        'Source/PersonalChronicle/ChronicleSettings.cs',
        'Source/PersonalChronicle/Settings/SettingsCatalog.cs',
        'Source/PersonalChronicle/Settings/SettingsDrawer.cs',
        'Source/PersonalChronicle/Settings/SettingsText.cs',
        'Source/PersonalChronicle/Domain/PawnEconomy/EconomyModel.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/EconomyService.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs'
    )
    $result=[ordered]@{
        collectedUtc=[DateTime]::UtcNow.ToString('o');
        gameVersion=(Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim();
        method='Read-only current game DLL metadata and selected TradeDeal.TryExecute IL calls; decompiled-source and current-project source hashes. No game assembly execution, runtime modification or gameplay verification.';
        assembly=[ordered]@{path=$dll;sha256=(Get-FileHash -LiteralPath $dll).Hash;mvid=$asm.MainModule.Mvid.ToString()};
        types=$types; tradeExecution=[ordered]@{signature=$execute.ToString();calls=$calls};
        files=@($paths | ForEach-Object {
            $resolved=(Resolve-Path -LiteralPath (Join-Path $repo $_)).Path
            [ordered]@{path=$resolved;sha256=(Get-FileHash -LiteralPath $resolved).Hash}
        });
        limits=@('No verified complete net-income callback exists in this evidence.',
            'Prefix quote quantities may be normalized inside execution; success does not prove settlement custody.',
            'Decompiled source may differ from current DLL; exact transfer/delivery hooks need a prototype.',
            'New design names and currency rules are proposed mod contracts, not vanilla APIs.');
        publicSources=@(
            'https://harmony.pardeike.net/v2/articles/patching-prefix.html',
            'https://harmony.pardeike.net/v2/articles/patching-postfix.html',
            'https://spdskatr.github.io/RWModdingResources/saving-guide'
        )
    }
    $dest=Join-Path $PSScriptRoot '../evidence/公有制度与外贸_接口证据.json'
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $dest -Encoding UTF8
    Write-Output "Verified $($types.Count) types, $($calls.Count) IL calls and $($paths.Count) source fingerprints."
} finally { $asm.Dispose() }
