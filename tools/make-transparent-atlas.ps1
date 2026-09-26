# Generates the fully transparent linked atlas used by hidden gas pipes (VE convention: an invisible
# Building_Pipe graphic). This is a functional asset, not art.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'Textures\Things\Building\Linked'
New-Item -ItemType Directory -Force $dir | Out-Null
$path = Join-Path $dir 'GV_HiddenPipe_Atlas.png'
$bmp = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    for ($x = 0; $x -lt 64; $x++) { for ($y = 0; $y -lt 64; $y++) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)) } }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
} finally { $bmp.Dispose() }
Write-Output "Wrote $path"
