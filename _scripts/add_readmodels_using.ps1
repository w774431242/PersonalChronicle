# Insert 'using PersonalChronicle.Application.ReadModels;' into files that reference bare ReadModels types.
# ASCII only.
$ErrorActionPreference = 'Stop'
$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$src = Join-Path $root 'Source\PersonalChronicle'
$using = 'using PersonalChronicle.Application.ReadModels;'

$o = & 'C:\dotnet8\dotnet.exe' build (Join-Path $root 'Source\PersonalChronicle.csproj') -c Release /p:Platform=x64 2>&1 | Out-String
$files = @($o -split "`r?`n" |
    Where-Object { $_ -match 'error CS0246' -and $_ -match 'ReadModels' } |
    ForEach-Object { if ($_ -match '^(.+?)\(\d+,\d+\)') { $matches[1] } } |
    Sort-Object -Unique)
Write-Host ("files to patch: " + $files.Count)

foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $enc = New-Object System.Text.UTF8Encoding($bom)
    $lines = [System.IO.File]::ReadAllLines($f)
    $has = $false
    foreach ($l in $lines) { if ($l -match 'using\s+PersonalChronicle\.Application\.ReadModels') { $has = $true; break } }
    if ($has) { continue }
    # insert after the last top-of-file using statement (before first namespace/type line)
    $idx = -1
    for ($i = 0; $i -lt [Math]::Min($lines.Count, 40); $i++) {
        if ($lines[$i] -match '^\s*using\s+') { $idx = $i }
        elseif ($lines[$i] -match '^\s*(namespace|\[)') { break }
    }
    $out = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $out.Add($lines[$i])
        if ($i -eq $idx) { $out.Add($using) }
    }
    if ($idx -lt 0) { $out.Insert(0, $using) }
    [System.IO.File]::WriteAllLines($f, $out, $enc)
    Write-Host ("patched: " + $f.Substring($root.Length + 1))
}
