# Generates the functional placeholder textures listed in docs/texture-spec.md (everything except the hidden-pipe
# atlas, which make-transparent-atlas.ps1 owns and this script never touches). These are clearly labelled
# programmer-art stand-ins so the mod can be playtested visually; they are NOT final art. Re-running overwrites
# every file it generates, so real art should replace this script's output, not be mixed into it.
#
# Pipe atlases double as a row-order calibration chart: every 64x64 tile carries its index, laid out with
#   index = north*1 + east*2 + south*4 + west*8,  col = index % 4,  row = floor(index / 4)  (row 0 = top of the PNG)
# so placing a small pipe cluster in game shows directly whether row 0 lands at the top or bottom of the atlas.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$script:Written = New-Object System.Collections.Generic.List[string]

# ---------------------------------------------------------------------------------------------------------------
# Data
# ---------------------------------------------------------------------------------------------------------------
$Gases = [ordered]@{
    Toxin       = '#8CC72E'
    Sedative    = '#A88CF2'
    Haywire     = '#40B3FF'
    Insecticide = '#FF9E26'
}
$Palette = @{
    Ink        = '#1A1A1A'   # outlines
    Compounder = '#6E8F8C'   # muted grey-teal, not gas-tinted
    Sensor     = '#8C979F'   # neutral grey, not gas-tinted
    Accent     = '#3FB8AF'   # neutral UI accent
    Steel      = '#A7B0B8'
    Off        = '#8A8A8A'
    Danger     = '#E0483E'
    Wall       = '#2E2E2E'
    WallHatch  = '#666666'
}

# Graphic_Multi rotation files. West is east mirrored by the game, so it is never authored.
# Dir = the way a front-facing building faces; wall-mounted things invert it (the file name names the wall side).
$Rotations = @(
    @{ Suffix = 'north'; Letter = 'N'; Dir = 'up' }
    @{ Suffix = 'east';  Letter = 'E'; Dir = 'right' }
    @{ Suffix = 'south'; Letter = 'S'; Dir = 'down' }
)
$Opposite = @{ up = 'down'; down = 'up'; right = 'left'; left = 'right' }

# Kind: Front = front-facing Graphic_Multi, Wall = wall attachment Graphic_Multi, Ceiling/Floor = Graphic_Single.
# Size = N/S size (W,H); Graphic_Multi east files use the rotated size. File may contain {0} for the gas name.
$Buildings = @(
    @{ Kind = 'Front';   Dir = 'Things/Building/GasVentilation/Manifold';    File = 'GV_Manifold_{0}';    PerGas = $true;  Size = 128, 128; Label = 'MANIFOLD' }
    @{ Kind = 'Front';   Dir = 'Things/Building/GasVentilation/VentWall';    File = 'GV_VentWall_{0}';    PerGas = $true;  Size = 128, 128; Label = 'VENT' }
    @{ Kind = 'Wall';    Dir = 'Things/Building/GasVentilation/VentMounted'; File = 'GV_VentMounted_{0}'; PerGas = $true;  Size = 128, 128; Label = 'VENT' }
    @{ Kind = 'Ceiling'; Dir = 'Things/Building/GasVentilation/VentCeiling'; File = 'GV_VentCeiling_{0}'; PerGas = $true;  Size = 128, 128; Label = 'CEIL' }
    @{ Kind = 'Floor';   Dir = 'Things/Building/GasVentilation/VentFloor';   File = 'GV_VentFloor_{0}';   PerGas = $true;  Size = 128, 128; Label = 'FLOOR' }
    @{ Kind = 'Front';   Dir = 'Things/Building/GasVentilation';             File = 'GV_GasCompounder';   PerGas = $false; Size = 448, 192; Label = 'COMPOUNDER'; Color = $Palette.Compounder }
    @{ Kind = 'Wall';    Dir = 'Things/Building/GasVentilation';             File = 'GV_IntruderSensor';  PerGas = $false; Size = 128, 128; Label = 'SENSOR';     Color = $Palette.Sensor }
)

# ---------------------------------------------------------------------------------------------------------------
# GDI+ helpers
# ---------------------------------------------------------------------------------------------------------------
function Get-Color([string]$Hex, [int]$Alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($Hex)
    [System.Drawing.Color]::FromArgb($Alpha, $c.R, $c.G, $c.B)
}

# Factor < 1 darkens toward black, > 1 lightens toward white (2 = white).
function Get-Shade([System.Drawing.Color]$Color, [double]$Factor, [int]$Alpha = -1) {
    if ($Alpha -lt 0) { $Alpha = $Color.A }
    $f = { param($v) if ($Factor -le 1) { [int]($v * $Factor) } else { [int]($v + (255 - $v) * ($Factor - 1)) } }
    [System.Drawing.Color]::FromArgb($Alpha, (& $f $Color.R), (& $f $Color.G), (& $f $Color.B))
}

function New-Rect([double]$X, [double]$Y, [double]$W, [double]$H) { [System.Drawing.RectangleF]::new($X, $Y, $W, $H) }

function New-Canvas([int]$Width, [int]$Height) {
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    [pscustomobject]@{ Bitmap = $bmp; G = $g; W = $Width; H = $Height }
}

# $RelPath is relative to the repo root, without extension.
function Save-Canvas($Canvas, [string]$RelPath) {
    $path = Join-Path $root ($RelPath.Replace('/', '\') + '.png')
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    $Canvas.G.Dispose()
    try { $Canvas.Bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $Canvas.Bitmap.Dispose() }
    $script:Written.Add($path)
}

function New-RoundedRectPath([System.Drawing.RectangleF]$R, [double]$Radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min([Math]::Min($Radius * 2, $R.Width), $R.Height)
    $p.AddArc((New-Rect $R.Left ($R.Top) $d $d), 180, 90)
    $p.AddArc((New-Rect ($R.Right - $d) $R.Top $d $d), 270, 90)
    $p.AddArc((New-Rect ($R.Right - $d) ($R.Bottom - $d) $d $d), 0, 90)
    $p.AddArc((New-Rect $R.Left ($R.Bottom - $d) $d $d), 90, 90)
    $p.CloseFigure()
    $p
}

function New-Pen([System.Drawing.Color]$Color, [double]$Width, [switch]$Round, [switch]$Dashed) {
    $pen = New-Object System.Drawing.Pen $Color, ([single]$Width)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    if ($Round) {
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    }
    if ($Dashed) { $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash }
    $pen
}

# Fills and/or outlines a GraphicsPath, then disposes it.
function Paint-Path($G, $Path, $Fill = $null, $Outline = $null, [double]$Width = 2) {
    if ($null -ne $Fill) { $b = New-Object System.Drawing.SolidBrush $Fill; $G.FillPath($b, $Path); $b.Dispose() }
    if ($null -ne $Outline) { $p = New-Pen $Outline $Width; $G.DrawPath($p, $Path); $p.Dispose() }
    $Path.Dispose()
}

function Paint-Ellipse($G, [double]$Cx, [double]$Cy, [double]$Rx, [double]$Ry, $Fill = $null, $Outline = $null, [double]$Width = 2) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse((New-Rect ($Cx - $Rx) ($Cy - $Ry) ($Rx * 2) ($Ry * 2)))
    Paint-Path $G $path $Fill $Outline $Width
}

function Paint-Polygon($G, [double[]]$Coords, $Fill = $null, $Outline = $null, [double]$Width = 2) {
    $pts = for ($i = 0; $i -lt $Coords.Length; $i += 2) { [System.Drawing.PointF]::new($Coords[$i], $Coords[$i + 1]) }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]$pts)
    Paint-Path $G $path $Fill $Outline $Width
}

function Paint-Line($G, [double]$X1, [double]$Y1, [double]$X2, [double]$Y2, $Color, [double]$Width, [switch]$Round) {
    $p = New-Pen $Color $Width -Round:$Round
    $G.DrawLine($p, [single]$X1, [single]$Y1, [single]$X2, [single]$Y2)
    $p.Dispose()
}

# Bold text centred in $Box, shrunk until it fits, drawn as a filled path with an outline for contrast.
function Draw-Label($G, [string]$Text, [System.Drawing.RectangleF]$Box, [double]$MaxEm, $Fill = [System.Drawing.Color]::White,
                    $Outline = [System.Drawing.Color]::Black, [double]$OutlineWidth = 3) {
    $family = New-Object System.Drawing.FontFamily 'Arial'
    $fmt = [System.Drawing.StringFormat]::GenericTypographic
    $em = $MaxEm
    while ($true) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddString($Text, $family, [int][System.Drawing.FontStyle]::Bold, [single]$em, [System.Drawing.PointF]::new(0, 0), $fmt)
        $b = $path.GetBounds()
        if ($em -le 6 -or (($b.Width + $OutlineWidth) -le $Box.Width -and ($b.Height + $OutlineWidth) -le $Box.Height)) { break }
        $path.Dispose()
        $em -= 1
    }
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate([single]($Box.X + ($Box.Width - $b.Width) / 2 - $b.X), [single]($Box.Y + ($Box.Height - $b.Height) / 2 - $b.Y))
    $path.Transform($m)
    $m.Dispose()
    if ($OutlineWidth -gt 0) { $p = New-Pen $Outline ($OutlineWidth * 2); $G.DrawPath($p, $path); $p.Dispose() }
    $br = New-Object System.Drawing.SolidBrush $Fill
    $G.FillPath($br, $path)
    $br.Dispose(); $path.Dispose(); $family.Dispose()
}

# Block arrow of overall length $Size centred on (Cx, Cy), pointing up/right/down/left.
function Draw-Arrow($G, [double]$Cx, [double]$Cy, [double]$Size, [string]$Dir,
                    $Fill = [System.Drawing.Color]::White, $Outline = [System.Drawing.Color]::Black) {
    $angle = @{ up = 0; right = 90; down = 180; left = 270 }[$Dir] * [Math]::PI / 180
    $cos = [Math]::Cos($angle); $sin = [Math]::Sin($angle)
    $unit = 0, -0.5, 0.42, -0.02, 0.16, -0.02, 0.16, 0.5, -0.16, 0.5, -0.16, -0.02, -0.42, -0.02
    $coords = New-Object double[] $unit.Length
    for ($i = 0; $i -lt $unit.Length; $i += 2) {
        $coords[$i] = $Cx + ($unit[$i] * $cos - $unit[$i + 1] * $sin) * $Size
        $coords[$i + 1] = $Cy + ($unit[$i] * $sin + $unit[$i + 1] * $cos) * $Size
    }
    Paint-Polygon $G $coords $Fill $Outline ([Math]::Max(2, $Size * 0.05))
}

# Small white disc with the rotation letter (N/E/S).
function Draw-Badge($G, [double]$Cx, [double]$Cy, [double]$Radius, [string]$Letter) {
    Paint-Ellipse $G $Cx $Cy $Radius $Radius ([System.Drawing.Color]::White) (Get-Color $Palette.Ink) ([Math]::Max(2, $Radius * 0.15))
    $box = New-Rect ($Cx - $Radius * 0.7) ($Cy - $Radius * 0.7) ($Radius * 1.4) ($Radius * 1.4)
    Draw-Label $G $Letter $box ($Radius * 1.4) (Get-Color $Palette.Ink) ([System.Drawing.Color]::White) 0
}

# Arrow in the upper part of $Area and the label in a band below it; used inside building bodies/devices.
function Draw-ArrowAndLabel($G, [System.Drawing.RectangleF]$Area, [string]$Dir, [string]$Label) {
    $labelBox = New-Rect ($Area.X + $Area.Width * 0.06) ($Area.Y + $Area.Height * 0.68) ($Area.Width * 0.88) ($Area.Height * 0.26)
    $arrowSize = [Math]::Min($Area.Width * 0.85, $Area.Height * 0.62) * 0.8
    Draw-Arrow $G ($Area.X + $Area.Width / 2) ($Area.Y + $Area.Height * 0.36) $arrowSize $Dir
    Draw-Label $G $Label $labelBox ($labelBox.Height) -OutlineWidth ([Math]::Max(2, $labelBox.Height * 0.1))
}

# ---------------------------------------------------------------------------------------------------------------
# Texture painters
# ---------------------------------------------------------------------------------------------------------------
function New-PipeAtlas([string]$RelPath, $LineColor, $EdgeColor) {
    $c = New-Canvas 256 256
    $tile = 64
    for ($index = 0; $index -lt 16; $index++) {
        $ox = ($index % 4) * $tile; $oy = [Math]::Floor($index / 4) * $tile
        $cx = $ox + $tile / 2; $cy = $oy + $tile / 2
        $ends = @()
        if ($index -band 1) { $ends += , @($cx, $oy) }            # north
        if ($index -band 2) { $ends += , @(($ox + $tile), $cy) }  # east
        if ($index -band 4) { $ends += , @($cx, ($oy + $tile)) }  # south
        if ($index -band 8) { $ends += , @($ox, $cy) }            # west
        # Two passes (dark edge, then colour) so joins between segments stay clean. Flat caps meet the tile edge.
        foreach ($pass in @(@{ Color = $EdgeColor; Width = 16; Hub = 11 }, @{ Color = $LineColor; Width = 10; Hub = 8 })) {
            foreach ($e in $ends) { Paint-Line $c.G $cx $cy $e[0] $e[1] $pass.Color $pass.Width }
            $hub = if ($ends.Count -eq 0) { $pass.Hub - 3 } else { $pass.Hub }
            Paint-Ellipse $c.G $cx $cy $hub $hub $pass.Color
        }
        # Calibration number in the (always empty) top-left quadrant of the tile.
        Draw-Label $c.G ([string]$index) (New-Rect ($ox + 1) ($oy + 1) 25 23) 22 -OutlineWidth 2.5
    }
    Save-Canvas $c $RelPath
}

function New-FrontBuilding([string]$RelPath, [int]$W, [int]$H, $Color, $Rotation, [string]$Label) {
    $c = New-Canvas $W $H
    $min = [Math]::Min($W, $H)
    $inset = [Math]::Round($min * 0.06)
    $body = New-Rect $inset $inset ($W - 2 * $inset) ($H - 2 * $inset)
    Paint-Path $c.G (New-RoundedRectPath $body ($min * 0.12)) $Color (Get-Color $Palette.Ink) ([Math]::Max(3, $min * 0.035))
    Draw-ArrowAndLabel $c.G $body $Rotation.Dir $Label
    $r = $min * 0.1
    Draw-Badge $c.G ($body.X + $r + 4) ($body.Y + $r + 4) $r $Rotation.Letter
    Save-Canvas $c $RelPath
}

# Wall attachment: the file's rotation names the wall side; the device sits off the wall and faces the room.
function New-WallMounted([string]$RelPath, [int]$Size, $Color, $Rotation, [string]$Label) {
    $c = New-Canvas $Size $Size
    $band = [Math]::Round($Size * 0.23)
    $wall, $room = switch ($Rotation.Dir) {
        'up'    { (New-Rect 0 0 $Size $band), (New-Rect 0 $band $Size ($Size - $band)) }
        'right' { (New-Rect ($Size - $band) 0 $band $Size), (New-Rect 0 0 ($Size - $band) $Size) }
        'down'  { (New-Rect 0 ($Size - $band) $Size $band), (New-Rect 0 0 $Size ($Size - $band)) }
    }
    $hatch = New-Object System.Drawing.Drawing2D.HatchBrush ([System.Drawing.Drawing2D.HatchStyle]::WideUpwardDiagonal),
        (Get-Color $Palette.WallHatch), (Get-Color $Palette.Wall)
    $c.G.FillRectangle($hatch, $wall); $hatch.Dispose()
    $p = New-Pen (Get-Color $Palette.Ink) 3; $c.G.DrawRectangle($p, $wall.X, $wall.Y, $wall.Width, $wall.Height); $p.Dispose()
    $gap = $Size * 0.09
    $device = New-Rect ($room.X + $gap) ($room.Y + $gap) ($room.Width - 2 * $gap) ($room.Height - 2 * $gap)
    Paint-Path $c.G (New-RoundedRectPath $device ($Size * 0.1)) $Color (Get-Color $Palette.Ink) 4
    Draw-ArrowAndLabel $c.G $device $Opposite[$Rotation.Dir] $Label
    $r = $Size * 0.09
    Draw-Badge $c.G ($device.X + $r * 0.6) ($device.Y + $r * 0.6) $r $Rotation.Letter
    Save-Canvas $c $RelPath
}

# Drawn above pawns with a Transparent shader, so the PNG itself stays ~70% opaque.
function New-CeilingVent([string]$RelPath, [int]$Size, $Color, [string]$Label) {
    $c = New-Canvas $Size $Size
    $m = $Size / 2; $r = $Size * 0.42
    Paint-Ellipse $c.G $m $m $r $r (Get-Shade $Color 1 180) (Get-Shade $Color 0.45 200) ($Size * 0.05)
    Paint-Ellipse $c.G $m $m ($r * 0.62) ($r * 0.62) $null (Get-Shade $Color 0.45 170) ($Size * 0.03)
    Draw-Label $c.G $Label (New-Rect ($m - $r * 0.7) ($m - $Size * 0.12) ($r * 1.4) ($Size * 0.24)) ($Size * 0.22)
    Save-Canvas $c $RelPath
}

function New-FloorVent([string]$RelPath, [int]$Size, $Color, [string]$Label) {
    $c = New-Canvas $Size $Size
    $frame = [Math]::Round($Size * 0.08)
    $c.G.Clear((Get-Color '#3C4046'))
    $b = New-Object System.Drawing.SolidBrush $Color
    $c.G.FillRectangle($b, [single]$frame, [single]$frame, [single]($Size - 2 * $frame), [single]($Size - 2 * $frame)); $b.Dispose()
    $slats = 6
    for ($i = 1; $i -lt $slats; $i++) {
        $y = $frame + ($Size - 2 * $frame) * $i / $slats
        Paint-Line $c.G $frame $y ($Size - $frame) $y (Get-Shade $Color 0.35) ($Size * 0.035)
    }
    Draw-Label $c.G $Label (New-Rect ($Size * 0.12) ($Size * 0.37) ($Size * 0.76) ($Size * 0.26)) ($Size * 0.24)
    Save-Canvas $c $RelPath
}

# 64x64 vertical steel capsule; $Band adds a gas-coloured stripe across the middle.
function New-Canister([string]$RelPath, $Band = $null) {
    $c = New-Canvas 64 64
    $ink = Get-Color $Palette.Ink; $steel = Get-Color $Palette.Steel
    Paint-Path $c.G (New-RoundedRectPath (New-Rect 26 3 12 8) 2) (Get-Shade $steel 0.55) $ink 2
    $shell = New-Rect 19 9 26 51
    Paint-Path $c.G (New-RoundedRectPath $shell 13) $steel
    Paint-Line $c.G 25 18 25 50 (Get-Shade $steel 1.6) 3 -Round   # highlight
    if ($null -ne $Band) {
        $clip = New-RoundedRectPath $shell 13
        $c.G.SetClip($clip)
        $b = New-Object System.Drawing.SolidBrush $Band; $c.G.FillRectangle($b, 17, 27, 30, 14); $b.Dispose()
        $c.G.ResetClip(); $clip.Dispose()
        Paint-Line $c.G 19 27 45 27 $ink 1.5; Paint-Line $c.G 19 41 45 41 $ink 1.5
    }
    Paint-Path $c.G (New-RoundedRectPath $shell 13) $null $ink 2.5
    Save-Canvas $c $RelPath
}

# Horizontal pipe segment with flanges; -Dashed draws it as a dashed outline only ("hidden" pipe).
function Add-PipeGlyph($G, $Color, [double]$Cy = 32, [double]$Scale = 1, [switch]$Dashed) {
    $parts = @(
        (New-Rect 10 ($Cy - 8 * $Scale) 44 (16 * $Scale)),
        (New-Rect 6 ($Cy - 13 * $Scale) 9 (26 * $Scale)),
        (New-Rect 49 ($Cy - 13 * $Scale) 9 (26 * $Scale))
    )
    foreach ($r in $parts) {
        if ($Dashed) {
            $p = New-Pen $Color 3 -Dashed; $G.DrawRectangle($p, $r.X, $r.Y, $r.Width, $r.Height); $p.Dispose()
        } else {
            Paint-Path $G (New-RoundedRectPath $r 3) $Color (Get-Color $Palette.Ink) 2.5
        }
    }
}

function New-Icon([string]$RelPath, [scriptblock]$Paint) {
    $c = New-Canvas 64 64
    & $Paint $c.G
    Save-Canvas $c $RelPath
}

# ---------------------------------------------------------------------------------------------------------------
# Generate
# ---------------------------------------------------------------------------------------------------------------
$ink = Get-Color $Palette.Ink
$accent = Get-Color $Palette.Accent

# Pipe atlases (4 gas-tinted + 1 shared blueprint). GV_HiddenPipe_Atlas is deliberately not generated here.
foreach ($gas in $Gases.Keys) {
    $col = Get-Color $Gases[$gas]
    New-PipeAtlas "Textures/Things/Building/Linked/GV_Pipe_${gas}_Atlas" $col (Get-Shade $col 0.35)
}
New-PipeAtlas 'Textures/Things/Building/Linked/GV_Pipe_Blueprint_Atlas' (Get-Color '#BFD9FF' 170) (Get-Color '#5A8FD8' 200)

# Buildings
foreach ($def in $Buildings) {
    $variants = if ($def.PerGas) { @($Gases.Keys) } else { @('') }   # '' = the single shared, non-gas variant
    foreach ($gas in $variants) {
        $color = if ($gas) { Get-Color $Gases[$gas] } else { Get-Color $def.Color }
        $base = "Textures/$($def.Dir)/" + ($def.File -f $gas)
        switch ($def.Kind) {
            'Front' {
                foreach ($rot in $Rotations) {
                    $w, $h = if ($rot.Suffix -eq 'east') { $def.Size[1], $def.Size[0] } else { $def.Size[0], $def.Size[1] }
                    New-FrontBuilding "${base}_$($rot.Suffix)" $w $h $color $rot $def.Label
                }
            }
            'Wall'    { foreach ($rot in $Rotations) { New-WallMounted "${base}_$($rot.Suffix)" $def.Size[0] $color $rot $def.Label } }
            'Ceiling' { New-CeilingVent $base $def.Size[0] $color $def.Label }
            'Floor'   { New-FloorVent $base $def.Size[0] $color $def.Label }
        }
    }
}

# Items
New-Canister 'Textures/Things/Item/GasVentilation/GV_CanisterEmpty'
foreach ($gas in $Gases.Keys) { New-Canister "Textures/Things/Item/GasVentilation/GV_Canister_$gas" (Get-Color $Gases[$gas]) }

# Architect icons
foreach ($gas in $Gases.Keys) {
    $col = Get-Color $Gases[$gas]
    New-Icon "Textures/UI/Icons/GasVentilation/GV_Pipe_$gas" { param($g) Add-PipeGlyph $g $col }
    New-Icon "Textures/UI/Icons/GasVentilation/GV_HiddenPipe_$gas" { param($g) Add-PipeGlyph $g $col -Dashed }
}

# Gizmo icons
New-Icon 'Textures/UI/Commands/GV_VentMode_Off' {
    param($g)
    $grey = Get-Color $Palette.Off
    Paint-Ellipse $g 32 32 24 24 $null $grey 6
    Paint-Line $g 15 49 49 15 $grey 6 -Round
}
New-Icon 'Textures/UI/Commands/GV_VentMode_On' { param($g) Paint-Ellipse $g 32 32 24 24 $accent $ink 3 }
New-Icon 'Textures/UI/Commands/GV_VentMode_Sensor' {
    param($g)
    Paint-Ellipse $g 32 32 25 25 $null $accent 5
    Paint-Ellipse $g 32 32 15 9 ([System.Drawing.Color]::White) $ink 2.5
    Paint-Ellipse $g 32 32 5 5 $accent $ink 1.5
}
New-Icon 'Textures/UI/Commands/GV_SensorArmed' {
    param($g)
    Paint-Polygon $g @(32, 5, 54, 13, 52, 36, 32, 59, 12, 36, 10, 13) $accent $ink 3
    Paint-Line $g 21 32 29 41 ([System.Drawing.Color]::White) 6 -Round
    Paint-Line $g 29 41 44 23 ([System.Drawing.Color]::White) 6 -Round
}
New-Icon 'Textures/UI/Commands/GV_SensorTargets' {
    param($g)
    $red = Get-Color $Palette.Danger
    Paint-Ellipse $g 32 32 19 19 $null $red 4
    foreach ($d in @(@(0, -1), @(1, 0), @(0, 1), @(-1, 0))) { Paint-Line $g (32 + $d[0] * 11) (32 + $d[1] * 11) (32 + $d[0] * 29) (32 + $d[1] * 29) $red 4 -Round }
    Paint-Ellipse $g 32 32 3.5 3.5 $red
}
New-Icon 'Textures/UI/Commands/GV_SensorLinger' {
    param($g)
    $sand = Get-Color '#E8C872'
    Paint-Polygon $g @(18, 10, 46, 10, 32, 32) (Get-Color '#FFFFFF' 60) $accent 3
    Paint-Polygon $g @(32, 32, 46, 54, 18, 54) $sand $accent 3
    Paint-Line $g 14 9 50 9 $ink 4 -Round
    Paint-Line $g 14 55 50 55 $ink 4 -Round
}
New-Icon 'Textures/UI/Commands/GV_SensorStopWhenDowned' {
    param($g)
    $body = Get-Color '#D9D9D9'
    Paint-Line $g 6 50 58 50 (Get-Color $Palette.Off) 3 -Round        # ground
    Paint-Ellipse $g 28 40 17 7 $body $ink 2.5                         # body lying on its side
    Paint-Ellipse $g 51 38 7 7 $body $ink 2.5                          # head
    Paint-Polygon $g @(10, 8, 22, 8, 22, 20, 10, 20) (Get-Color $Palette.Danger) $ink 2   # "stop" marker
}
New-Icon 'Textures/UI/Designators/GV_DeconstructGasPipes' {
    param($g)
    Add-PipeGlyph $g (Get-Color $Palette.Steel) 38 0.8
    $red = Get-Color $Palette.Danger
    Paint-Line $g 16 10 48 42 $ink 10 -Round; Paint-Line $g 48 10 16 42 $ink 10 -Round
    Paint-Line $g 16 10 48 42 $red 6 -Round;  Paint-Line $g 48 10 16 42 $red 6 -Round
}

# Workshop preview and mod icon (repo root About/, not Textures/)
$gasColors = @($Gases.Values | ForEach-Object { Get-Color $_ })
$preview = New-Canvas 640 360
$blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
$blend.Colors = [System.Drawing.Color[]]$gasColors
$blend.Positions = [single[]]@(0, 0.33, 0.67, 1)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Rect 0 0 640 360), $gasColors[0], $gasColors[3], 0.0
$grad.InterpolationColors = $blend
$preview.G.FillRectangle($grad, 0, 0, 640, 360); $grad.Dispose()
Paint-Path $preview.G (New-RoundedRectPath (New-Rect 50 105 540 150) 18) (Get-Color '#000000' 170)
Draw-Label $preview.G 'Gas Ventilation Systems' (New-Rect 70 118 500 70) 48
Draw-Label $preview.G 'placeholder preview - not final art' (New-Rect 70 200 500 34) 22 (Get-Color '#D0D0D0') -OutlineWidth 0
Save-Canvas $preview 'About/Preview'

$icon = New-Canvas 64 64
$frame = New-Rect 4 4 56 56
$clip = New-RoundedRectPath $frame 10
$icon.G.SetClip($clip)
for ($i = 0; $i -lt 4; $i++) {
    $b = New-Object System.Drawing.SolidBrush $gasColors[$i]
    $icon.G.FillRectangle($b, [single](4 + ($i % 2) * 28), [single](4 + [Math]::Floor($i / 2) * 28), 28, 28); $b.Dispose()
}
$icon.G.ResetClip(); $clip.Dispose()
Paint-Path $icon.G (New-RoundedRectPath $frame 10) $null $ink 3
Save-Canvas $icon 'About/ModIcon'

Write-Output "Wrote $($script:Written.Count) placeholder textures."
