# Deploys the mod (build + package + junction, same as deploy-dev.ps1) and launches RimWorld
# interactively, boots to the normal main menu (no -quicktest), with:
#   - a separate, persistent save-data profile (tools/.dev-profile) so your real saves and your
#     real ~100-mod list are never touched;
#   - only Harmony, the DLCs, VEF and this mod active, so you're not debugging against your full
#     modlist;
#   - dev mode already on, so the architect menu / debug tools are one click away.
# The profile persists between runs (unlike the smoke-test profile, which is wiped every time),
# so a test colony you build survives a re-launch.
#
# By default it also requests a DevHarness persistent scenario (-Scenario, default "playground"):
# when you start a NEW colony, the mod's research is finished, starter materials are dropped next to
# your colonists and a small sealed demo room (toxin manifold -> pipe -> vent On) is built in the
# south-west corner, then the game is handed back to you (paused). Loaded saves are left alone.
# -Scenario showcase instead builds the four-room trailer-footage set (see ShowcaseScenario.cs).
# -NoPlayground skips requesting any scenario at all.
param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [switch]$SkipDeploy,
    [switch]$NoPlayground,
    [string]$Scenario = 'playground'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
    Write-Output 'RimWorld is already running.'
    exit 2
}
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
    Write-Output 'WARNING: Steam is not running. Harmony and VEF are Workshop mods and may fail to load.'
}

if (-not $SkipDeploy) {
    & (Join-Path $PSScriptRoot 'deploy-dev.ps1') -RimWorldDir $RimWorldDir
}

$profileDir = Join-Path $root 'tools\.dev-profile'
$configDir = Join-Path $profileDir 'Config'
New-Item -ItemType Directory -Force $configDir | Out-Null

$version = (Get-Content (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim()
$modsConfigPath = Join-Path $configDir 'ModsConfig.xml'
if (-not (Test-Path $modsConfigPath)) {
    $modsConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>$version</version>
  <activeMods>
    <li>brrainz.harmony</li>
    <li>ludeon.rimworld</li>
    <li>ludeon.rimworld.royalty</li>
    <li>ludeon.rimworld.ideology</li>
    <li>ludeon.rimworld.biotech</li>
    <li>ludeon.rimworld.odyssey</li>
    <li>oskarpotocki.vanillafactionsexpanded.core</li>
    <li>xmaxer.gasventilation</li>
    <li>xmaxer.gasventilation.devharness</li>
  </activeMods>
  <knownExpansions>
    <li>ludeon.rimworld.royalty</li>
    <li>ludeon.rimworld.ideology</li>
    <li>ludeon.rimworld.biotech</li>
    <li>ludeon.rimworld.odyssey</li>
  </knownExpansions>
</ModsConfigData>
"@
    Set-Content -Encoding UTF8 $modsConfigPath $modsConfig
    Write-Output "Wrote a fresh dev ModsConfig.xml: $modsConfigPath"
} else {
    Write-Output "Reusing existing dev profile mod list: $modsConfigPath"
    Write-Output '(delete tools/.dev-profile, or edit that file by hand, to change it)'
    # The dev harness (inert without a request.txt) runs the playground; older dev profiles predate it.
    $existing = Get-Content $modsConfigPath -Raw
    if ($existing -notmatch '(?i)<li>xmaxer\.gasventilation\.devharness</li>') {
        $patched = $existing -replace '(?i)(\s*)(<li>xmaxer\.gasventilation</li>)', '$1$2$1<li>xmaxer.gasventilation.devharness</li>'
        Set-Content -Encoding UTF8 $modsConfigPath $patched.TrimEnd()
        Write-Output 'Added the dev harness mod (needed for the playground) to the dev profile mod list.'
    }
}

$prefsPath = Join-Path $configDir 'Prefs.xml'
if (-not (Test-Path $prefsPath)) {
    $prefs = @"
<?xml version="1.0" encoding="utf-8"?>
<PlayerPrefs>
  <devMode>True</devMode>
  <runInBackground>True</runInBackground>
  <pauseOnError>True</pauseOnError>
  <resetModsConfigOnCrash>False</resetModsConfigOnCrash>
</PlayerPrefs>
"@
    Set-Content -Encoding UTF8 $prefsPath $prefs
    Write-Output "Wrote a fresh dev Prefs.xml (dev mode on): $prefsPath"
}

$harnessDir = Join-Path $profileDir 'GasVentHarness'
$requestPath = Join-Path $harnessDir 'request.txt'
if ($NoPlayground) {
    Remove-Item -Force $requestPath -ErrorAction SilentlyContinue
    Write-Output 'Scenario disabled: new colonies start untouched.'
} else {
    New-Item -ItemType Directory -Force $harnessDir | Out-Null
    Set-Content -Encoding ASCII $requestPath $Scenario
    Write-Output "Scenario '$Scenario' requested on a new colony."
}

Write-Output ''
Write-Output "Launching RimWorld with a separate dev profile: $profileDir"
Write-Output 'This does not touch your real RimWorld saves, settings or mod list.'
Start-Process -FilePath (Join-Path $RimWorldDir 'RimWorldWin64.exe') -ArgumentList "-savedatafolder=$profileDir"
