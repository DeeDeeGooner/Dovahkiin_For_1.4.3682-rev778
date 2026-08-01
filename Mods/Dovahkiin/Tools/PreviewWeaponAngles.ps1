# WEAPON HOLD ANGLE SWEEP - stop guessing, look.
#
# Two attempts at Call of Valor's sword angle have now failed in play: -70 (inherited from the
# axe) read as "tilts in the wrong direction", and +70 read as "upside down or stabbing him in
# the throat". The notebook's own rule applies - if a fix does not work twice, the DIAGNOSIS is
# wrong, not the number. What was wrong is that I do not know how the angle in
# Thing_DragonAspectOverlay maps to what appears on screen, and I kept reasoning about it.
#
# So: render every angle and look. The notebook already says this in as many words - "verify a
# rotation SIGN by rendering it, never by reasoning about it".
#
# THE CALIBRATION ROW IS WHAT MAKES THIS TRUSTWORTHY. This preview draws in GDI+, the game draws
# in Unity, and the two conventions need not agree. So the AXE is drawn at the same sweep, and
# the axe's correct pose is KNOWN - it is signed off, "head up and back, a shouldered greataxe",
# and the game holds it at -70. Whichever cell of the axe row reproduces that pose tells us the
# offset between this sheet's angles and the game's, and the sword's answer can then be
# converted rather than hoped for.

Add-Type -AssemblyName System.Drawing

$MODROOT   = Split-Path -Parent $PSScriptRoot
$TEXDIR    = Join-Path $MODROOT "Textures\Things\Pawn\CallOfValor"
$EQUIP     = Join-Path $MODROOT "Textures\Things\Item\Equipment"
$BODY_DIR  = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$HEAD_DIR  = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Gloomy Face Mod\Textures\Things\Pawn\Humanlike\Heads\Male"
$HEAD_KIND = "Male_Average_Pointy"

$OUTDIR = if ($env:DOVAH_PREVIEW) { $env:DOVAH_PREVIEW } else { $PSScriptRoot }
if (-not (Test-Path $OUTDIR)) { New-Item -ItemType Directory -Force $OUTDIR | Out-Null }
# DOVAH_FACING picks which facing to sweep: south (default), north or west.
# The code holds THREE separate angles - one shared by south and east, one for north, one for
# west - so each needs its own sweep. Deriving the other two from the first is exactly the
# guessing this tool exists to replace.
$FACING = if ($env:DOVAH_FACING) { $env:DOVAH_FACING } else { "south" }
$OUTFILE = Join-Path $OUTDIR ("weapon_angles_" + $FACING + ".png")

# West is drawn from the EAST sprite mirrored - what Graphic_Multi does for free from three
# files - so the body, head, armour and helm all come from east and the whole cell is flipped.
$SPRITE_ROT = if ($FACING -eq "west") { "east" } else { $FACING }
$MIRROR = ($FACING -eq "west")

# Same constants as the C# and PreviewAncientDragonborn.ps1
$CELLPX    = 256.0
$REF_WIDTH = 1.5
$HEAD_DZ   = 0.34
$C_INVIS   = @(191, 237, 250)     # InvisibilityMatPool's colour
$INVIS_A   = 0.5
$C_WHITE   = @(255, 255, 255)

# Thing_DragonAspectOverlay.DrawAt's own weapon placement, south facing:
#   axeLocal.x = +0.34 * scale/RefBodyWidth ; axeLocal.z = -0.06 * scale/RefBodyWidth
# z is negative = DOWN the screen, hence +0.06 in screen y below.
#   north:     axeLocal.x = -0.34   west: -0.30   (read out of DrawAt, not guessed)
$WEAPON_DX = 0.34
if ($FACING -eq "north") { $WEAPON_DX = -0.34 }
elseif ($FACING -eq "west") { $WEAPON_DX = -0.30 }
$WEAPON_DY = 0.06

# BaseHeadOffsetAt's x component applies to east and west only, sign flipped on west.
$HEAD_DX = 0.0
if ($FACING -eq "east") { $HEAD_DX = 0.04 }
elseif ($FACING -eq "west") { $HEAD_DX = -0.04 }

$ANGLES = @( (-120), (-90), (-70), (-45), (-20), (0), (20), (45), (70), (90), (120), (145), (180), (215) )

function RGB($red, $green, $blue, $alpha = 255) {
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$red))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$green))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$blue))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$alpha))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}

function LoadPng([string]$path) {
  if (-not (Test-Path $path)) { Write-Output "MISSING: $path"; return $null }
  return New-Object System.Drawing.Bitmap $path
}

function DrawGround($gfx, [double]$originX, [double]$originY, [double]$size, [int]$salt) {
  $back = New-Object System.Drawing.SolidBrush (RGB 122 106 84 255)
  $gfx.FillRectangle($back, [single]$originX, [single]$originY, [single]$size, [single]$size)
  $back.Dispose()
  $step = 11
  for ($gy = 0; $gy -lt $size; $gy += $step) {
    for ($gx = 0; $gx -lt $size; $gx += $step) {
      $noise = [Math]::Sin(($gx + 1) * 12.9898 + ($gy + 1) * 78.233 + $salt * 37.719) * 43758.5453
      $noise = $noise - [Math]::Floor($noise)
      $delta = [int](($noise - 0.5) * 42.0)
      $tile = New-Object System.Drawing.SolidBrush (RGB (122 + $delta) (106 + $delta) (84 + [int]($delta * 0.8)) 255)
      $gfx.FillRectangle($tile, [single]($originX + $gx), [single]($originY + $gy), [single]$step, [single]$step)
      $tile.Dispose()
    }
  }
}

function DrawTex($gfx, $img, [double]$centreX, [double]$centreY, [double]$width, [double]$height,
                 $tint, [double]$alpha, [double]$angle = 0.0, [bool]$mirror = $false) {
  if ($img -eq $null -or $alpha -le 0.01) { return }
  $matrix = New-Object System.Drawing.Imaging.ColorMatrix
  $matrix.Matrix00 = [single]($tint[0] / 255.0)
  $matrix.Matrix11 = [single]($tint[1] / 255.0)
  $matrix.Matrix22 = [single]($tint[2] / 255.0)
  $matrix.Matrix33 = [single]$alpha
  $matrix.Matrix44 = [single]1.0
  $attrs = New-Object System.Drawing.Imaging.ImageAttributes
  $attrs.SetColorMatrix($matrix)
  $saved = $gfx.Save()
  $gfx.TranslateTransform([single]$centreX, [single]$centreY)
  if ($angle -ne 0.0) { $gfx.RotateTransform([single]$angle) }
  # Mirror AFTER the rotate, the same order Graphic_Multi's west uses - flipping first would
  # also flip the direction the rotation turns.
  if ($mirror) { $gfx.ScaleTransform([single](-1.0), [single]1.0) }
  $rect = New-Object System.Drawing.Rectangle ([int](-$width / 2)), ([int](-$height / 2)), ([int]$width), ([int]$height)
  $gfx.DrawImage($img, $rect, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $attrs)
  $gfx.Restore($saved)
  $attrs.Dispose()
}

# --- assets --------------------------------------------------------------------
$bodyImg   = LoadPng (Join-Path $BODY_DIR "Naked_Male_$SPRITE_ROT.png")
$headImg   = LoadPng (Join-Path $HEAD_DIR "${HEAD_KIND}_$SPRITE_ROT.png")
$armourImg = LoadPng (Join-Path $TEXDIR "DragonAspect_L2_Male_$SPRITE_ROT.png")
$helmImg   = LoadPng (Join-Path $TEXDIR "DragonAspectHelm_$SPRITE_ROT.png")
$swordImg  = LoadPng (Join-Path $EQUIP "ValorGreatsword.png")
$axeImg    = LoadPng (Join-Path $EQUIP "DovahkiinAncientAxe.png")
foreach ($asset in @($bodyImg, $headImg, $armourImg, $helmImg, $swordImg, $axeImg)) {
  if ($asset -eq $null) { Write-Output "ABORT: an asset failed to load"; exit 1 }
}

# --- sheet ---------------------------------------------------------------------
$CELLBOX = 250.0
$PAD = 12
$COLS = 7
$rowsPerWeapon = [int][Math]::Ceiling($ANGLES.Count / [double]$COLS)
$sheetWidth  = [int](($PAD * ($COLS + 1)) + ($CELLBOX * $COLS))
$blockHeight = [int](($CELLBOX + 34) * $rowsPerWeapon)
$sheetHeight = [int](70 + $blockHeight + 56 + $blockHeight + 60)
$sheet = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($sheet)
$gfx.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gfx.Clear((RGB 24 24 28 255))

$fontTitle = New-Object System.Drawing.Font "Segoe UI", 19, ([System.Drawing.FontStyle]::Bold)
$fontHead  = New-Object System.Drawing.Font "Segoe UI", 14, ([System.Drawing.FontStyle]::Bold)
$fontBody  = New-Object System.Drawing.Font "Segoe UI", 12
$brWhite = New-Object System.Drawing.SolidBrush (RGB 240 240 240 255)
$brGold  = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)
$brDim   = New-Object System.Drawing.SolidBrush (RGB 178 178 186 255)
$brGood  = New-Object System.Drawing.SolidBrush (RGB 120 220 130 255)

$titleText = "WEAPON HOLD ANGLE - every value, facing " + $FACING.ToUpper() + ". Pick the number, do not reason about it."
$gfx.DrawString($titleText, $fontTitle, $brWhite, [single]$PAD, [single]10)

# Draw one weapon's sweep. Returns the y below it.
function DrawSweep($gfx, [double]$topY, $weaponImg, [double]$weaponDrawSize, [string]$label,
                   [int]$calibrationAngle, $brushHeader) {
  $gfx.DrawString($label, $fontHead, $brushHeader, [single]$PAD, [single]$topY)
  $rowTop = $topY + 24
  for ($idx = 0; $idx -lt $ANGLES.Count; $idx++) {
    $col = $idx % $COLS
    $row = [int][Math]::Floor($idx / $COLS)
    $cellX = $PAD + ($col * ($CELLBOX + $PAD))
    $cellY = $rowTop + ($row * ($CELLBOX + 34))

    DrawGround $gfx $cellX $cellY $CELLBOX (7 + $idx)
    $oldClip = $gfx.Clip
    $gfx.SetClip((New-Object System.Drawing.RectangleF ([single]$cellX), ([single]$cellY), ([single]$CELLBOX), ([single]$CELLBOX)))

    # the sprites are 256 frames drawn into a CELLBOX box, so scale everything by that ratio
    $shrink = $CELLBOX / $CELLPX
    $pxPerUnit = ($CELLPX / $REF_WIDTH) * $shrink
    $centreX = $cellX + $CELLBOX / 2.0
    $centreY = $cellY + $CELLBOX / 2.0
    $drawnCell = $CELLPX * $shrink

    # the invisible pawn under the armour, exactly as the summon renders
    $headX = $centreX + ($HEAD_DX * $pxPerUnit)
    $headY = $centreY - ($HEAD_DZ * $pxPerUnit)
    DrawTex $gfx $bodyImg $centreX $centreY $drawnCell $drawnCell $C_INVIS $INVIS_A 0.0 $MIRROR
    DrawTex $gfx $headImg $headX $headY $drawnCell $drawnCell $C_INVIS $INVIS_A 0.0 $MIRROR
    DrawTex $gfx $armourImg $centreX $centreY $drawnCell $drawnCell $C_WHITE 1.0 0.0 $MIRROR
    DrawTex $gfx $helmImg $headX $headY $drawnCell $drawnCell $C_WHITE 1.0 0.0 $MIRROR

    $angle = [double]$ANGLES[$idx]
    DrawTex $gfx $weaponImg ($centreX + ($WEAPON_DX * $pxPerUnit)) ($centreY + ($WEAPON_DY * $pxPerUnit)) `
            ($weaponDrawSize * $pxPerUnit) ($weaponDrawSize * $pxPerUnit) $C_WHITE 1.0 $angle

    $gfx.Clip = $oldClip
    $tag = "" + [int]$angle
    $brush = $brDim
    if ([int]$angle -eq $calibrationAngle) { $tag = $tag + "   <- the GAME's current value"; $brush = $brGood }
    $gfx.DrawString($tag, $fontBody, $brush, [single]$cellX, [single]($cellY + $CELLBOX + 5))
  }
  return ($rowTop + ($rowsPerWeapon * ($CELLBOX + 34)))
}

$afterSword = DrawSweep $gfx 44.0 $swordImg 1.25 "CALL OF VALOR'S GREATSWORD - which of these is right?" 70 $brGold
$gfx.DrawString("CALIBRATION - the AXE, whose correct pose is already SIGNED OFF: head up and back, a shouldered greataxe.",
  $fontHead, $brWhite, [single]$PAD, [single]($afterSword + 8))
$gfx.DrawString("Whichever axe cell looks right tells us how this sheet's numbers map to the game's. Tell me BOTH cells.",
  $fontBody, $brDim, [single]$PAD, [single]($afterSword + 30))
$afterAxe = DrawSweep $gfx ($afterSword + 52) $axeImg 1.5 "" (-70) $brGold

$gfx.DrawString("Weapon placed exactly where the code puts it: 0.34 cells to his right, 0.06 down. Armour, helm and the",
  $fontBody, $brDim, [single]$PAD, [single]($afterAxe + 6))
$gfx.DrawString("invisible pawn underneath are the real shipping textures at the real invisibility colour.",
  $fontBody, $brDim, [single]$PAD, [single]($afterAxe + 26))

$sheet.Save($OUTFILE, [System.Drawing.Imaging.ImageFormat]::Png)
$gfx.Dispose(); $sheet.Dispose()
Write-Output "WROTE: $OUTFILE"
