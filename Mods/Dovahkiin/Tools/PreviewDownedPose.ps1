# DOWNED-POSE PREVIEW - what commit a84f284 changed, and what a WRONG version looks like.
#
# The notebook flags the helm as "the one to watch": if the offset rotation is wrong it sits
# BESIDE her rather than on her head. This sheet draws that failure deliberately, in cell 4,
# so there is a picture of what to look for rather than a sentence about it.
#
# Positions here are EXACT - they are the same arithmetic as Thing_DragonAspectOverlay.DrawAt,
# and a position has no sign ambiguity. Sprite ROTATION direction was checked by rendering it
# (notebook: "verify a rotation SIGN by rendering it, never by reasoning about it"), and the
# body's own head lands on the same side as the rotated helm offset, which is the check.
#
# The AXE is deliberately not here. The preview harness's hold angles were tuned by eye
# against the game (145 south, where the C# holds -70), so its angle convention is empirical
# rather than derived, and "+ bodyAngle" in that convention cannot be trusted without a
# playtest. Saying so beats shipping a picture that might disagree with the game.

Add-Type -AssemblyName System.Drawing

# Sibling of PreviewAncientDragonborn.ps1, and like it this READS ONLY - it regenerates
# nothing and cannot touch the signed-off textures, so it is safe to run at any time. Unlike
# GenerateDragonAspect.ps1, which REWRITES the 36 shipping Dragon Aspect textures.
#
# Kept in Tools/ deliberately: WriteAnimatedGif.ps1 was written twice and lost twice before
# anyone committed it, and Call of Valor's hero will want this exact check when he is built -
# he gets downed too, and he wears the same overlay machinery.
$MODROOT  = Split-Path -Parent $PSScriptRoot
$TEXDIR   = Join-Path $MODROOT "Textures\Things\Pawn\DragonAspect"
$BODY_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$HEAD_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Gloomy Face Mod\Textures\Things\Pawn\Humanlike\Heads\Male"
$HEAD_KIND = "Male_Average_Pointy"

# DOVAH_PREVIEW picks the output folder, the same convention PreviewAncientDragonborn.ps1 uses.
$OUTDIR = if ($env:DOVAH_PREVIEW) { $env:DOVAH_PREVIEW } else { $PSScriptRoot }
if (-not (Test-Path $OUTDIR)) { New-Item -ItemType Directory -Force $OUTDIR | Out-Null }
$OUTFILE = Join-Path $OUTDIR "downed_preview.png"

# Same constants as PreviewAncientDragonborn.ps1 / the C#
$CELLPX    = 256.0    # one 1.5-world-unit body quad
$GROUNDPX  = 384.0
$REF_WIDTH = 1.5
$HEAD_DZ   = 0.34     # BodyTypeDef Male headOffset.y - BaseHeadOffsetAt's z
$C_WHITE   = @(255, 255, 255)

# BodyAngle() for a downed humanlike is LayingFacing().AsAngle - Rot4.West (270) or
# Rot4.East (90), chosen by thingIDNumber parity. 90 here; 270 is the same picture mirrored.
$BODY_ANGLE = 90.0

function RGB($red, $green, $blue, $alpha = 255) {
  # clamp at the ONE place colours are constructed - notebook rule
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

# Rough LIT GROUND. Judging a translucent overlay on a dark backdrop is a different
# question - that mistake cost this project two playtest rounds.
function DrawGround($gfx, [double]$originX, [double]$originY, [double]$size, [int]$salt) {
  $backBrush = New-Object System.Drawing.SolidBrush (RGB 122 106 84 255)
  $gfx.FillRectangle($backBrush, [single]$originX, [single]$originY, [single]$size, [single]$size)
  $backBrush.Dispose()
  $step = 11
  for ($gy = 0; $gy -lt $size; $gy += $step) {
    for ($gx = 0; $gx -lt $size; $gx += $step) {
      $noise = [Math]::Sin(($gx + 1) * 12.9898 + ($gy + 1) * 78.233 + $salt * 37.719) * 43758.5453
      $noise = $noise - [Math]::Floor($noise)
      $delta = [int](($noise - 0.5) * 42.0)
      $tileBrush = New-Object System.Drawing.SolidBrush (RGB (122 + $delta) (106 + $delta) (84 + [int]($delta * 0.8)) 255)
      $gfx.FillRectangle($tileBrush, [single]($originX + $gx), [single]($originY + $gy), [single]$step, [single]$step)
      $tileBrush.Dispose()
    }
  }
}

function DrawTex($gfx, $img, [double]$centreX, [double]$centreY, [double]$width, [double]$height,
                 $tint, [double]$alpha, [double]$angle = 0.0) {
  if ($img -eq $null -or $alpha -le 0.01) { return }
  $matrix = New-Object System.Drawing.Imaging.ColorMatrix
  $matrix.Matrix00 = [single]($tint[0] / 255.0)
  $matrix.Matrix11 = [single]($tint[1] / 255.0)
  $matrix.Matrix22 = [single]($tint[2] / 255.0)
  $matrix.Matrix33 = [single]$alpha
  $matrix.Matrix44 = [single]1.0
  $imgAttrs = New-Object System.Drawing.Imaging.ImageAttributes
  $imgAttrs.SetColorMatrix($matrix)
  $saved = $gfx.Save()
  $gfx.TranslateTransform([single]$centreX, [single]$centreY)
  if ($angle -ne 0.0) { $gfx.RotateTransform([single]$angle) }
  $rect = New-Object System.Drawing.Rectangle ([int](-$width / 2)), ([int](-$height / 2)), ([int]$width), ([int]$height)
  $gfx.DrawImage($img, $rect, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $imgAttrs)
  $gfx.Restore($saved)
  $imgAttrs.Dispose()
}

# Quaternion.AngleAxis(theta, Vector3.up) applied to a world vector (x, 0, z):
#   x' =  x*cos + z*sin
#   z' = -x*sin + z*cos
# RimWorld's camera looks down -Y, so world +X is screen right and world +Z is screen UP
# (screen y DECREASING). That is why the screen offset below negates z.
function RotateOffset([double]$worldX, [double]$worldZ, [double]$degrees) {
  $rad = $degrees * [Math]::PI / 180.0
  $cosA = [Math]::Cos($rad)
  $sinA = [Math]::Sin($rad)
  $outX = ($worldX * $cosA) + ($worldZ * $sinA)
  $outZ = (-$worldX * $sinA) + ($worldZ * $cosA)
  return @( ($outX), ($outZ) )    # every element parenthesised - ',' binds tighter than '*'
}

# One cell. $spriteAngle rotates the sprites; $offsetAngle rotates the HEAD OFFSET.
# Passing different values for the two is what produces the "half-fixed" failure picture.
function DrawPawnCell($gfx, [double]$originX, [double]$originY, $bodyImg, $headImg,
                      $armourImg, $helmImg, [double]$spriteAngle, [double]$offsetAngle,
                      [int]$salt) {
  DrawGround $gfx $originX $originY $GROUNDPX $salt
  $oldClip = $gfx.Clip
  $gfx.SetClip((New-Object System.Drawing.RectangleF ([single]$originX), ([single]$originY), ([single]$GROUNDPX), ([single]$GROUNDPX)))

  $pxPerUnit = $CELLPX / $REF_WIDTH
  $centreX = $originX + $GROUNDPX / 2.0
  $centreY = $originY + $GROUNDPX / 2.0

  # BaseHeadOffsetAt(south) is (0, 0, HEAD_DZ) - straight up out of the chest.
  $rotated = RotateOffset 0.0 $HEAD_DZ $offsetAngle
  $headX = $centreX + ($rotated[0] * $pxPerUnit)
  $headY = $centreY - ($rotated[1] * $pxPerUnit)

  # the pawn underneath - RimWorld lays body AND head over together
  DrawTex $gfx $bodyImg $centreX $centreY $CELLPX $CELLPX $C_WHITE 1.0 $spriteAngle
  $bodyRot = RotateOffset 0.0 $HEAD_DZ $spriteAngle
  DrawTex $gfx $headImg ($centreX + ($bodyRot[0] * $pxPerUnit)) ($centreY - ($bodyRot[1] * $pxPerUnit)) `
          $CELLPX $CELLPX $C_WHITE 1.0 $spriteAngle

  # the overlay
  DrawTex $gfx $armourImg $centreX $centreY $CELLPX $CELLPX $C_WHITE 1.0 $spriteAngle
  DrawTex $gfx $helmImg $headX $headY $CELLPX $CELLPX $C_WHITE 1.0 $spriteAngle

  $gfx.Clip = $oldClip
}

# --- assets ---------------------------------------------------------------------
$bodySouth   = LoadPng (Join-Path $BODY_DIR "Naked_Male_south.png")
$headSouth   = LoadPng (Join-Path $HEAD_DIR "${HEAD_KIND}_south.png")
$armourSouth = LoadPng (Join-Path $TEXDIR "DragonAspect_L2_Male_south.png")
$helmSouth   = LoadPng (Join-Path $TEXDIR "DragonAspectHelm_south.png")
foreach ($asset in @($bodySouth, $headSouth, $armourSouth, $helmSouth)) {
  if ($asset -eq $null) { Write-Output "ABORT: an asset failed to load"; exit 1 }
}

# --- sheet ----------------------------------------------------------------------
$PAD = 28
$CELL_COUNT = 4
$sheetWidth  = [int](($PAD * ($CELL_COUNT + 1)) + ($GROUNDPX * $CELL_COUNT))
# room for the cells, their captions, the ZOOM STRIP and the footer. The first version
# was GROUNDPX+250 and silently cropped the last three footer lines - a sheet that runs
# off its own canvas invites exactly the "you didn't mention X" round it is meant to avoid.
# Derived from the SAME expressions the layout uses below, not guessed - the first two
# versions guessed and cropped the footer both times.
$ZOOM_DRAWN = 190.0 * 2.0
$sheetHeight = [int](64 + $GROUNDPX + 150 + $ZOOM_DRAWN + 44 + 122)
$sheet = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($sheet)
$gfx.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gfx.Clear((RGB 24 24 28 255))

$fontTitle = New-Object System.Drawing.Font "Segoe UI", 20, ([System.Drawing.FontStyle]::Bold)
$fontHead  = New-Object System.Drawing.Font "Segoe UI", 16, ([System.Drawing.FontStyle]::Bold)
$fontBody  = New-Object System.Drawing.Font "Segoe UI", 13
$brushWhite = New-Object System.Drawing.SolidBrush (RGB 240 240 240 255)
$brushGood  = New-Object System.Drawing.SolidBrush (RGB 120 220 130 255)
$brushBad   = New-Object System.Drawing.SolidBrush (RGB 245 120 110 255)
$brushDim   = New-Object System.Drawing.SolidBrush (RGB 172 172 180 255)

$titleY = 14
$gfx.DrawString("Dragon Aspect, level 3 - the DOWNED-POSE fix (commit a84f284)", $fontTitle, $brushWhite, [single]$PAD, [single]$titleY)

$cellY = 64
# label, sprite angle, offset angle, colour of the label
$cells = @(
  @( ("1. STANDING"),                (0.0),          (0.0),          ("good") ),
  @( ("2. DOWNED - BEFORE the fix"), (0.0),          (0.0),          ("bad")  ),
  @( ("3. DOWNED - AFTER the fix"),  ($BODY_ANGLE),  ($BODY_ANGLE),  ("good") ),
  @( ("4. DOWNED - HALF-FIXED"),     ($BODY_ANGLE),  (0.0),          ("bad")  )
)

$captions = @(
  "Unchanged. The fix cannot`naffect an upright pawn -`nBodyAngle() returns 0.",
  "The body lay over; the armour`nand helm did not. This is what`nyou reported.",
  "Sprite AND head offset both`nrotated. Helm sits ON the head.`nThis is what to expect now.",
  "Sprite rotated, offset NOT.`nHelm floats beside her, over`nthe chest. WATCH FOR THIS."
)

for ($cellIdx = 0; $cellIdx -lt $CELL_COUNT; $cellIdx++) {
  $cellX = $PAD + ($cellIdx * ($GROUNDPX + $PAD))
  $entry = $cells[$cellIdx]
  $labelText   = $entry[0]
  $spriteAngle = [double]$entry[1]
  $offsetAngle = [double]$entry[2]
  $verdict     = $entry[3]

  # cell 2 draws the pawn LYING (angle 90) but the overlay UPRIGHT - the reported bug
  if ($cellIdx -eq 1) {
    DrawGround $gfx $cellX $cellY $GROUNDPX ($cellIdx + 3)
    $oldClip = $gfx.Clip
    $gfx.SetClip((New-Object System.Drawing.RectangleF ([single]$cellX), ([single]$cellY), ([single]$GROUNDPX), ([single]$GROUNDPX)))
    $pxPerUnit = $CELLPX / $REF_WIDTH
    $centreX = $cellX + $GROUNDPX / 2.0
    $centreY = $cellY + $GROUNDPX / 2.0
    $lying = RotateOffset 0.0 $HEAD_DZ $BODY_ANGLE
    DrawTex $gfx $bodySouth $centreX $centreY $CELLPX $CELLPX $C_WHITE 1.0 $BODY_ANGLE
    DrawTex $gfx $headSouth ($centreX + ($lying[0] * $pxPerUnit)) ($centreY - ($lying[1] * $pxPerUnit)) `
            $CELLPX $CELLPX $C_WHITE 1.0 $BODY_ANGLE
    DrawTex $gfx $armourSouth $centreX $centreY $CELLPX $CELLPX $C_WHITE 1.0 0.0
    DrawTex $gfx $helmSouth $centreX ($centreY - ($HEAD_DZ * $pxPerUnit)) $CELLPX $CELLPX $C_WHITE 1.0 0.0
    $gfx.Clip = $oldClip
  } else {
    DrawPawnCell $gfx $cellX $cellY $bodySouth $headSouth $armourSouth $helmSouth `
                 $spriteAngle $offsetAngle ($cellIdx + 3)
  }

  $labelBrush = if ($verdict -eq "good") { $brushGood } else { $brushBad }
  $gfx.DrawString($labelText, $fontHead, $labelBrush, [single]$cellX, [single]($cellY + $GROUNDPX + 8))
  $gfx.DrawString($captions[$cellIdx].Replace("`n", [Environment]::NewLine), $fontBody, $brushDim,
                  [single]$cellX, [single]($cellY + $GROUNDPX + 36))
}

# --- ZOOM STRIP: the head region of cell 3 against cell 4, at 2x -----------------
# The whole judgement is "is the helm on the head or beside it", and at cell scale that is
# a ~40px call. Blow up just the head end of each and the answer is unmistakable.
$zoomY = $cellY + $GROUNDPX + 150
$gfx.DrawString("The head end, at 2x - this is the whole check:", $fontHead, $brushWhite, [single]$PAD, [single]($zoomY - 32))

$ZOOM_FACTOR = 2.0
$ZOOM_SRC = 190.0                       # px of source cell to magnify
$zoomDrawn = $ZOOM_SRC * $ZOOM_FACTOR
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

# cells 3 and 4 are indices 2 and 3; the head lies to the RIGHT of centre at 90 degrees
$zoomPairs = @( (2), (3) )
$zoomLabels = @( ("AFTER the fix - helm ON the head"), ("HALF-FIXED - helm beside the head") )
for ($zoomIdx = 0; $zoomIdx -lt 2; $zoomIdx++) {
  $srcCellIdx = $zoomPairs[$zoomIdx]
  $srcCellX = $PAD + ($srcCellIdx * ($GROUNDPX + $PAD))
  # centre the crop on the head: right of the cell centre, vertically centred
  $cropX = $srcCellX + ($GROUNDPX / 2.0) - ($ZOOM_SRC * 0.30)
  $cropY = $cellY + ($GROUNDPX / 2.0) - ($ZOOM_SRC / 2.0)
  $destX = $PAD + ($zoomIdx * ($zoomDrawn + $PAD * 2))
  $srcRect  = New-Object System.Drawing.RectangleF ([single]$cropX), ([single]$cropY), ([single]$ZOOM_SRC), ([single]$ZOOM_SRC)
  $destRect = New-Object System.Drawing.Rectangle ([int]$destX), ([int]$zoomY), ([int]$zoomDrawn), ([int]$zoomDrawn)
  $gfx.DrawImage($sheet, $destRect, $srcRect.X, $srcRect.Y, $srcRect.Width, $srcRect.Height, [System.Drawing.GraphicsUnit]::Pixel)
  $zoomBrush = if ($zoomIdx -eq 0) { $brushGood } else { $brushBad }
  $penBox = New-Object System.Drawing.Pen $zoomBrush.Color, ([single]2.0)
  $gfx.DrawRectangle($penBox, $destRect)
  $penBox.Dispose()
  $gfx.DrawString($zoomLabels[$zoomIdx], $fontHead, $zoomBrush, [single]$destX, [single]($zoomY + $zoomDrawn + 6))
}
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$footY = $zoomY + $zoomDrawn + 44
$gfx.DrawString("Level 3 armour + helm on the user's own pawn (Male body, Male_Average_Pointy head), over lit ground. Aura omitted -", $fontBody, $brushDim, [single]$PAD, [single]$footY)
$gfx.DrawString("it is a radial glow centred on the pawn, so rotation does not change it. Downed pawns lie at 90 or 270 degrees depending on", $fontBody, $brushDim, [single]$PAD, [single]($footY + 22))
$gfx.DrawString("pawn ID, so the other direction is this picture mirrored. The AXE is NOT shown: the preview's hold angles were tuned by eye", $fontBody, $brushDim, [single]$PAD, [single]($footY + 44))
$gfx.DrawString("against the game, so its angle convention cannot settle a downed weapon. That one genuinely needs the playtest.", $fontBody, $brushDim, [single]$PAD, [single]($footY + 66))

$sheet.Save($OUTFILE, [System.Drawing.Imaging.ImageFormat]::Png)
$gfx.Dispose()
$sheet.Dispose()
Write-Output "WROTE: $OUTFILE"
