# Runs RimWorld with an isolated profile, the packaged mod and the dev harness, then checks results.
# Exit codes: 0 = all passed, 1 = failures, 2 = blocked (game running or Steam not running).
param(
    [string[]]$Scenarios = @('boot'),
    [int]$TimeoutMinutes = 25,
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
)
$ErrorActionPreference = 'Stop'
# `powershell -File ... -Scenarios a,b` passes one literal string "a,b"; split it so both call styles work.
$Scenarios = @($Scenarios | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$root = Split-Path -Parent $PSScriptRoot

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { Write-Output 'BLOCKED: RimWorld is already running.'; exit 2 }
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { Write-Output 'BLOCKED: Steam is not running (Workshop mods Harmony/VEF would not load).'; exit 2 }

& (Join-Path $PSScriptRoot 'deploy-dev.ps1') -RimWorldDir $RimWorldDir

$profileDir = Join-Path $root 'tools\.smoke-profile'
if (Test-Path $profileDir) { Remove-Item -Recurse -Force $profileDir }
$configDir = Join-Path $profileDir 'Config'
$harnessDir = Join-Path $profileDir 'GasVentHarness'
New-Item -ItemType Directory -Force $configDir, $harnessDir | Out-Null
Set-Content -Encoding ASCII (Join-Path $harnessDir 'request.txt') ($Scenarios -join "`r`n")

$version = (Get-Content (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim()
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
Set-Content -Encoding UTF8 (Join-Path $configDir 'ModsConfig.xml') $modsConfig
$prefs = @"
<?xml version="1.0" encoding="utf-8"?>
<PlayerPrefs>
  <devMode>True</devMode>
  <runInBackground>True</runInBackground>
  <pauseOnError>False</pauseOnError>
  <pauseOnLoad>False</pauseOnLoad>
  <resetModsConfigOnCrash>False</resetModsConfigOnCrash>
  <volumeMaster>0</volumeMaster>
  <volumeGame>0</volumeGame>
  <volumeMusic>0</volumeMusic>
  <volumeAmbient>0</volumeAmbient>
  <volumeUI>0</volumeUI>
</PlayerPrefs>
"@
Set-Content -Encoding UTF8 (Join-Path $configDir 'Prefs.xml') $prefs

$log = Join-Path $profileDir 'run.log'
$gameArgs = @("-savedatafolder=$profileDir", '-quicktest', '-logFile', "`"$log`"", '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720')
$proc = Start-Process -FilePath (Join-Path $RimWorldDir 'RimWorldWin64.exe') -ArgumentList $gameArgs -PassThru
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $proc.Kill()
    Write-Output "FAILED: timed out after $TimeoutMinutes minutes. Log: $log"
    exit 1
}

$ok = $true
$resultsFile = Join-Path $harnessDir 'results.jsonl'
if (-not (Test-Path (Join-Path $harnessDir 'done.txt'))) { Write-Output 'FAILED: harness did not finish (no done.txt).'; $ok = $false }
if (Test-Path $resultsFile) {
    foreach ($line in Get-Content $resultsFile) {
        $r = $line | ConvertFrom-Json
        if ($r.summary) {
            if ($r.unexpectedErrors.Count -gt 0) { $ok = $false; Write-Output "FAILED: $($r.unexpectedErrors.Count) unexpected logged error(s):"; $r.unexpectedErrors | ForEach-Object { Write-Output "  $_" } }
        } else {
            Write-Output ("{0}: {1} ({2} ticks)" -f $r.scenario, $r.status, $r.ticks)
            if ($r.metrics) { $r.metrics | ForEach-Object { Write-Output "  metric: $_" } }
            if ($r.status -ne 'Passed') { $ok = $false; $r.failures | ForEach-Object { Write-Output "  - $_" } }
        }
    }
} else { Write-Output 'FAILED: no results.jsonl'; $ok = $false }

# "same packageId multiple times": duplicate Workshop subscriptions of unrelated mods on the dev machine.
$allow = 'Could not load Texture2D|Failed to find any textures at|Could not load UnityEngine.Texture2D|MatFrom with null sourceTex|Tried loading mod with the same packageId multiple times'
$patterns = 'Exception|Config error in|XML error|Could not resolve cross-reference|Patch operation .* failed|Could not find type named'
if (Test-Path $log) {
    $hits = Select-String -Path $log -Pattern $patterns | Where-Object { $_.Line -notmatch $allow }
    if ($hits) { $ok = $false; Write-Output 'FAILED: suspicious log lines:'; $hits | Select-Object -First 40 | ForEach-Object { Write-Output "  $($_.LineNumber): $($_.Line)" } }
}
Write-Output "Log: $log"
Write-Output "Screenshots/results: $harnessDir"
if ($ok) { Write-Output 'SMOKE TEST PASSED'; exit 0 } else { exit 1 }
