# ASCII-only on purpose: a BOM-less .ps1 holding non-ASCII text is decoded as
# GBK by PowerShell 5.1, which silently corrupts any CJK literal it contains.
#
# PowerShell 5.1 reads BOM-less .ps1 files using the ANSI codepage (GBK here).
# A UTF-8 script containing CJK text therefore decodes into mojibake at parse
# time, and any of that text written back into sources becomes permanently
# corrupted. This is what damaged 22 .cs headers on 2026-08-30.
#
# Run this after adding/creating any .ps1 under _scripts\ to stamp a UTF-8 BOM.
$ErrorActionPreference = 'Continue'
$dir = Join-Path $PSScriptRoot '..'
$target = Join-Path $dir '_scripts'

$stamped = 0
Get-ChildItem $target -Filter *.ps1 | ForEach-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { return }
    $out = New-Object byte[] ($b.Length + 3)
    $out[0] = 0xEF; $out[1] = 0xBB; $out[2] = 0xBF
    [Array]::Copy($b, 0, $out, 3, $b.Length)
    [System.IO.File]::WriteAllBytes($_.FullName, $out)
    $stamped++
}
Write-Host ('ps1 BOM stamped: ' + $stamped)
