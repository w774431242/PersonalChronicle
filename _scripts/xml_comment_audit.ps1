# Audit XML comment legality + wellformedness for engine-loaded XML.
# Rules checked (XML 1.0 spec + RimWorld Def loader):
#   1. "--" is ILLEGAL inside a comment body.
#   2. Nothing may precede the <?xml ...?> declaration except whitespace.
#   3. Whole file must be well-formed.
# ASCII only.
$ErrorActionPreference = 'Continue'
$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$targets = @('Defs', 'Languages', 'Patches')
$bad = New-Object System.Collections.Generic.List[string]
$notWell = New-Object System.Collections.Generic.List[string]
$decl = New-Object System.Collections.Generic.List[string]
$total = 0; $withComments = 0; $commentLines = 0

foreach ($t in $targets) {
    $dir = Join-Path $root $t
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem $dir -Recurse -Filter *.xml | ForEach-Object {
        $total++
        $enc = New-Object System.Text.UTF8Encoding($false)
        $text = [System.IO.File]::ReadAllText($_.FullName, $enc)
        $rel = $_.FullName.Substring($root.Length + 1)

        # wellformed
        try { [xml]$x = $text } catch { $notWell.Add($rel + '  :: ' + $_.Exception.Message); return }

        # comment bodies containing '--'
        $ms = [regex]::Matches($text, '<!--(?<body>.*?)-->', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        if ($ms.Count -gt 0) { $withComments++ }
        foreach ($m in $ms) {
            $body = $m.Groups['body'].Value
            $commentLines += (($body -split "`n").Count)
            if ($body.Contains('--')) { $bad.Add($rel + '  :: comment contains "--"') }
        }

        # something before xml declaration
        $first = $text.TrimStart()
        if ($first.StartsWith('<?xml')) {
            $idx = $text.IndexOf('<?xml')
            if (($text.Substring(0, $idx).Trim()) -ne '') { $decl.Add($rel) }
        }
    }
}

Write-Host ('xml files scanned : ' + $total)
Write-Host ('files w/ comments : ' + $withComments)
Write-Host ('comment lines     : ' + $commentLines)
Write-Host '=== ILLEGAL: "--" inside comment ==='
if ($bad.Count -eq 0) { Write-Host '  none' } else { $bad | Select-Object -First 20 | ForEach-Object { Write-Host ('  ' + $_) } }
Write-Host '=== NOT well-formed ==='
if ($notWell.Count -eq 0) { Write-Host '  none' } else { $notWell | Select-Object -First 20 | ForEach-Object { Write-Host ('  ' + $_) } }
Write-Host '=== content before <?xml declaration ==='
if ($decl.Count -eq 0) { Write-Host '  none' } else { $decl | ForEach-Object { Write-Host ('  ' + $_) } }
