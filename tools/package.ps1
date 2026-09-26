# Copies only shipping content into dist/GasVentilationSystems and validates it.
param([string]$OutDir = 'dist')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root "$OutDir\GasVentilationSystems"
if (Test-Path $target) { Remove-Item -Recurse -Force $target }
New-Item -ItemType Directory -Force $target | Out-Null

$include = @('About', '1.6', 'Compat', 'Languages', 'Textures', 'Sounds', 'LoadFolders.xml', 'LICENSE')
foreach ($item in $include) {
    $src = Join-Path $root $item
    if (Test-Path $src) { Copy-Item $src -Destination $target -Recurse -Force }
}

if (-not (Test-Path (Join-Path $target '1.6\Assemblies\GasVentilation.dll'))) { throw 'GasVentilation.dll missing: build first.' }
$allowed = @('GasVentilation.dll', '0MultiplayerAPI.dll')
$bad = Get-ChildItem $target -Recurse -Filter *.dll | Where-Object { $allowed -notcontains $_.Name }
if ($bad) { throw "Unexpected DLLs in package: $(($bad | ForEach-Object Name) -join ', ')" }
$forbidden = Get-ChildItem $target -Recurse -Include *.pdb, *.cs, *.csproj, *.user, *.sln, *.slnx | Select-Object -First 1
if ($forbidden) { throw "Forbidden file in package: $($forbidden.FullName)" }
Write-Output "Packaged to $target"
