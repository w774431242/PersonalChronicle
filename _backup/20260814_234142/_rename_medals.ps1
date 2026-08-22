# Rename medal PNGs from AI timestamp names to canonical Medal_<series>_<title>_<Tier>.png
$dir = "e:\SteamLibrary\steamapps\common\RimWorld\11\PersonalChronicle\Textures\Medals"

$map = @(
  # Labor (gear, red enamel)
  @("08-35-09", "Medal_Labor_Model_Bronze.png"),
  @("08-35-13", "Medal_Labor_Model_Gold.png"),
  @("08-35-35", "Medal_Labor_Worker_Bronze.png"),
  @("08-35-36", "Medal_Labor_Worker_Silver.png"),
  @("08-35-38", "Medal_Labor_Worker_Gold.png"),
  @("08-36-00", "Medal_Labor_TechAce_Bronze.png"),
  @("08-36-01", "Medal_Labor_TechAce_Silver.png"),
  @("08-36-06", "Medal_Labor_TechAce_Gold.png"),
  # Combat (crossed swords + star, steel blue enamel)
  @("08-36-26", "Medal_Combat_Hero_Bronze.png"),
  @("08-36-28", "Medal_Combat_Hero_Silver.png"),
  @("08-36-30", "Medal_Combat_Hero_Gold.png"),
  @("08-36-52", "Medal_Combat_FirstClass_Bronze.png"),
  @("08-36-50", "Medal_Combat_FirstClass_Silver.png"),
  @("08-36-55", "Medal_Combat_FirstClass_Gold.png"),
  @("08-37-42", "Medal_Combat_Enlistee_Bronze.png"),
  @("08-37-44", "Medal_Combat_Enlistee_Silver.png"),
  @("08-37-46", "Medal_Combat_Enlistee_Gold.png"),
  # Support (wheat sheaf, ochre enamel)
  @("08-38-07", "Medal_Support_Quartermaster_Silver.png"),
  @("08-38-11", "Medal_Support_Quartermaster_Gold.png"),
  @("08-38-32", "Medal_Support_Thrifty_Bronze.png"),
  @("08-38-34", "Medal_Support_Thrifty_Silver.png"),
  @("08-38-36", "Medal_Support_Thrifty_Gold.png"),
  # Legacy (heraldic shield, grey enamel)
  @("08-38-54", "Medal_Legacy_Heirloom_Bronze.png"),
  @("08-38-58", "Medal_Legacy_Heirloom_Silver.png"),
  @("08-39-01", "Medal_Legacy_Heirloom_Gold.png"),
  @("08-39-20", "Medal_Legacy_KillerBlade_Bronze.png"),
  @("08-39-25", "Medal_Legacy_KillerBlade_Silver.png"),
  @("08-39-29", "Medal_Legacy_KillerBlade_Gold.png"),
  # Workshop (factory silhouette, bronze-green enamel)
  @("08-39-53", "Medal_Workshop_Famous_Bronze.png"),
  @("08-39-56", "Medal_Workshop_Famous_Silver.png"),
  @("08-40-02", "Medal_Workshop_Famous_Gold.png"),
  @("08-40-22", "Medal_Workshop_Glorious_Bronze.png"),
  @("08-40-25", "Medal_Workshop_Glorious_Silver.png"),
  @("08-40-29", "Medal_Workshop_Glorious_Gold.png"),
  # Rank (crown, purple-red enamel, gold only)
  @("08-40-49", "Medal_Rank_LabourGlory_Gold.png"),
  @("08-40-51", "Medal_Rank_RelicGlory_Gold.png")
)

$ok = 0; $miss = 0
foreach ($m in $map) {
  $pattern = "Hyper_realistic_photograph_of__2026-08-14T$($m[0]).png"
  $src = Get-ChildItem -Path $dir -Filter $pattern -File | Select-Object -First 1
  if ($src -and -not (Test-Path "$dir\$($m[1])")) {
    Rename-Item -Path $src.FullName -NewName $m[1]
    Write-Host "OK  $($m[0]) -> $($m[1])"
    $ok++
  } else {
    Write-Host "MISS $($m[0]) (src=$($src -ne $null) destExists=$(Test-Path "$dir\$($m[1])"))"
    $miss++
  }
}
Write-Host "--- done ok=$ok miss=$miss ---"
