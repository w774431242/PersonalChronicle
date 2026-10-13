# Reset-repair: restore refactor state broken by accidental `git reset --hard`.
# Dry run by default. Use -Apply to modify files (backs up to _backup\reset_recovery first).
# ASCII only (PS 5.1 safe). Parses csc errors:
#   CS0260 -> add `partial` to class decl
#   CS0111/CS0102 -> delete duplicated member block from the TRACKED file
# Rule: keep the new untracked partial file's copy, delete the tracked file's copy.
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$root   = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$dotnet = 'C:\dotnet8\dotnet.exe'
$proj   = 'Source\PersonalChronicle.csproj'
$srcDir = Join-Path $root 'Source\PersonalChronicle'
$bakDir = Join-Path $root '_backup\reset_recovery'
Set-Location $root

# ---- untracked file set (these are the NEW partial files; keep their copies) ----
$untracked = @((cmd /c "git status --porcelain 2>nul") | Where-Object { $_ -match '^\?\?' } |
    ForEach-Object { (($_.Substring(3).Trim()) -replace '"','') -replace '/','\' })
function Test-Untracked([string]$rel) {
    $r = $rel -replace '/','\'
    foreach ($u in $script:untracked) {
        if ($u -eq $r) { return $true }
        if ($u.EndsWith('\') -and $r.StartsWith($u)) { return $true }
    }
    return $false
}

# ---- build ----
Write-Host '== building =='
$out = & $dotnet build $proj -c Release /p:Platform=x64 2>&1 | Out-String
$errLines = @($out -split "`r?`n" | Where-Object { $_ -match '\(\d+,\d+\): error CS\d+' })
Write-Host ("total errors: " + $errLines.Count)

$lq = [string][char]0x201C
$rq = [string][char]0x201D

# cache: class name -> candidate tracked files declaring it
$classFiles = @{}

function Get-ClassCandidates([string]$cls) {
    if ($classFiles.ContainsKey($cls)) { return $classFiles[$cls] }
    $list = New-Object System.Collections.ArrayList
    $pat = '\b(class|struct)\s+' + [regex]::Escape($cls) + '\b'
    Get-ChildItem $srcDir -Recurse -Filter *.cs | ForEach-Object {
        $rel = $_.FullName.Substring($root.Length + 1)
        if (Test-Untracked $rel) { return }
        $txt = [System.IO.File]::ReadAllText($_.FullName)
        if ($txt -match $pat) { [void]$list.Add($_.FullName) }
    }
    $classFiles[$cls] = $list
    return $list
}

# ---- parse errors into actions ----
$actions = New-Object System.Collections.ArrayList
foreach ($e in $errLines) {
    if ($e -notmatch '^(.+?)\((\d+),\d+\): error (CS\d+):') { continue }
    $file = $matches[1]; $ln = [int]$matches[2]; $code = $matches[3]

    if ($code -eq 'CS0260') {
        [void]$actions.Add(@{ Act='partial'; File=$file; Line=$ln; Name='' ; Code=$code })
        continue
    }
    if ($code -ne 'CS0111' -and $code -ne 'CS0102') { continue }

    # member name from the declaration line at the error location
    $all = [System.IO.File]::ReadAllLines($file)
    if ($ln -gt $all.Count) { continue }
    $decl = $all[$ln-1]
    $name = ''
    if ($decl -match '(\w+)\s*\(')        { $name = $matches[1] }
    elseif ($decl -match '\b(class|struct|enum|interface)\s+(\w+)') { $name = $matches[2] }
    elseif ($decl -match '(\w+)\s*(=|;)') { $name = $matches[1] }
    if ($name -eq '') {
        [void]$actions.Add(@{ Act='NONAME'; File=$file; Line=$ln; Name=''; Code=$code })
        continue
    }

    # class name from the message text (first quoted token)
    $cls = ''
    if ($e -match ([regex]::Escape($lq) + '(.+?)' + [regex]::Escape($rq))) { $cls = $matches[1] }

    $rel = $file.Substring($root.Length + 1)
    if (-not (Test-Untracked $rel)) {
        # error sits in the tracked file -> delete here
        [void]$actions.Add(@{ Act='del'; File=$file; Line=$ln; Name=$name; Code=$code })
        continue
    }

    # error sits in the NEW file -> delete the tracked twin (same class, same member name)
    $target = $null
    foreach ($f in (Get-ClassCandidates $cls)) {
        $ls = [System.IO.File]::ReadAllLines($f)
        for ($i = 0; $i -lt $ls.Count; $i++) {
            $t = $ls[$i]
            if ($t -match '//') { continue }
            if ($t -match ('^\s*(public|private|internal|protected)\b') -and
                $t -match ('\b' + [regex]::Escape($name) + '\b')) {
                $target = @{ File=$f; Line=($i+1) }; break
            }
        }
        if ($target) { break }
    }
    if ($target) { [void]$actions.Add(@{ Act='del'; File=$target.File; Line=$target.Line; Name=$name; Code=$code }) }
    else         { [void]$actions.Add(@{ Act='NOTFOUND'; File=$file; Line=$ln; Name=$name + ' (class ' + $cls + ')'; Code=$code }) }
}

# dedupe on File+Line
$uniq = $actions | Sort-Object { $_.File + ':' + $_.Line } -Unique
$del = @($uniq | Where-Object { $_.Act -eq 'del' })
$par = @($uniq | Where-Object { $_.Act -eq 'partial' })
$bad = @($uniq | Where-Object { $_.Act -eq 'NOTFOUND' -or $_.Act -eq 'NONAME' })

Write-Host ("actions: del=" + $del.Count + " partial=" + $par.Count + " unresolved=" + $bad.Count)
Write-Host '== partial to add =='
$par | ForEach-Object { Write-Host ("  " + $_.File.Substring($root.Length+1) + " :" + $_.Line) }
Write-Host '== members to delete =='
$del | ForEach-Object { Write-Host ("  " + $_.File.Substring($root.Length+1) + " :" + $_.Line + "  " + $_.Name) }
if ($bad.Count -gt 0) { Write-Host '== UNRESOLVED =='; $bad | ForEach-Object { Write-Host ("  " + $_.File.Substring($root.Length+1) + " :" + $_.Line + "  " + $_.Name) } }

if (-not $Apply) { Write-Host 'DRY RUN - no files modified.'; exit 0 }

# ---- apply ----
function Read-LinesEnc([string]$p, [ref]$enc) {
    $b = [System.IO.File]::ReadAllBytes($p)
    $bom = ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
    $enc.Value = New-Object System.Text.UTF8Encoding($bom)
    return [System.IO.File]::ReadAllLines($p)
}

# backup each touched file once
$touched = @($del | ForEach-Object { $_.File } | Sort-Object -Unique) +
           @($par | ForEach-Object { $_.File } | Sort-Object -Unique)
$touched = $touched | Sort-Object -Unique
foreach ($f in $touched) {
    $rel = $f.Substring($root.Length + 1)
    $dst = Join-Path $bakDir $rel
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item $f $dst -Force
}
Write-Host ("backed up " + $touched.Count + " files to " + $bakDir)

# deletions: bottom-up per file so line numbers stay valid
$delGrouped = $del | Group-Object { $_.File }
foreach ($g in $delGrouped) {
    $path = $g.Group[0].File
    $enc = New-Object System.Text.UTF8Encoding($false)
    $lines = Read-LinesEnc $path ([ref]$enc)
    $ranges = @($g.Group | Sort-Object { $_.Line } -Descending)
    $keep = New-Object System.Collections.Generic.List[int]
    $skipUntil = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $inDel = $false
        foreach ($r in $ranges) {
            if ($i -eq $r.Line - 1) {
                # extend upward over comments/attributes
                $start = $i
                while ($start -gt 0 -and $lines[$start-1].TrimStart() -match '^(//|\[)') { $start-- }
                # find block end by brace depth
                $end = $i; $depth = 0; $opened = $false
                for ($j = $start; $j -lt $lines.Count; $j++) {
                    $o = ([regex]::Matches($lines[$j], '\{')).Count
                    $c = ([regex]::Matches($lines[$j], '\}')).Count
                    $depth += $o - $c
                    if ($o -gt 0) { $opened = $true }
                    if (($opened -and $depth -le 0) -or (-not $opened -and $lines[$j] -match ';\s*$')) { $end = $j; break }
                }
                if ($start -le $i -and $end -ge $start) { $skipUntil = $end; $inDel = $true }
                break
            }
        }
        if ($inDel) { continue }
        if ($i -le $skipUntil) { continue }
        [void]$keep.Add($i)
    }
    $newLines = foreach ($k in $keep) { $lines[$k] }
    [System.IO.File]::WriteAllLines($path, $newLines, $enc)
    Write-Host ("deleted " + $ranges.Count + " member(s) from " + $path.Substring($root.Length+1))
}

foreach ($p in $par) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    $lines = Read-LinesEnc $p.File ([ref]$enc)
    $t = $lines[$p.Line - 1]
    if ($t -notmatch '\bpartial\b') {
        $lines[$p.Line - 1] = $t -replace '\b(class|struct)\b', 'partial $1'
        [System.IO.File]::WriteAllLines($p.File, $lines, $enc)
        Write-Host ("added partial -> " + $p.File.Substring($root.Length+1) + " :" + $p.Line)
    }
}

# rebuild summary
$out2 = & $dotnet build $proj -c Release /p:Platform=x64 2>&1 | Out-String
$err2 = @($out2 -split "`r?`n" | Where-Object { $_ -match '\(\d+,\d+\): error CS\d+' })
$warn2 = @($out2 -split "`r?`n" | Where-Object { $_ -match '\(\d+,\d+\): warning CS\d+' })
Write-Host ("AFTER: errors=" + $err2.Count + " warnings=" + $warn2.Count)
$err2 | Select-Object -First 15 | ForEach-Object { Write-Host $_ }
