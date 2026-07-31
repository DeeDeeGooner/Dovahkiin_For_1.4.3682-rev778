# =================================================================================
# PREVIEW: THE ANCIENT DRAGONBORN
# =================================================================================
# Composites the summon exactly as Thing_DragonAspectOverlay.DrawAt draws him with
# level 3 and drawAxe true, over lit ground, with the pawn rendered the way vanilla
# invisibility actually renders it.
#
# WHY THIS EXISTS SEPARATELY FROM GenerateDragonAspect.ps1's PREVIEW SHEET
#
# That sheet answers "does each body type's armour fit that body". It draws the pawn
# OPAQUE and has no axe, so it has never shown the Ancient Dragonborn at all - he is
# an INVISIBLE pawn wearing the level-3 armour and carrying the spectral halberd.
#
# It is also WRONG ABOUT THE HELM, which matters. It scales the helm image to 0.62 of
# the cell and offsets it by eye, with a comment admitting the offset is approximate.
# The game draws the helm on the FULL body mesh at the pawn's real head offset, so the
# helm on screen is about 1.6x wider than that sheet ever showed. This one uses the
# real numbers.
#
# EVERY NUMBER BELOW IS READ OUT OF THE SHIPPING CODE OR THE GAME, NOT CHOSEN:
#
#   - orbit/flare/ring fractions ....... Thing_DragonAspectOverlay consts
#   - Ember / Azure .................... Thing_DragonAspectOverlay statics
#   - the 21-slot particle table ....... Thing_DragonAspectOverlay.Slots, verbatim
#   - Hash01 ........................... same function, so the frames reproduce
#   - axe size / offsets / angles ...... DrawAt's axe block
#   - head offset (0.04, 0.34) ......... Core BodyTypes.xml, BodyTypeDef Male
#   - invisible pawn (0.75,0.93,0.98,0.5) InvisibilityMatPool static ctor IL
#   - body art ......................... Beautiful Bodies (mireia.bodies, ACTIVE)
#   - head art ......................... Gloomy Face (gloomy.gloomyfacemk2.1.4, ACTIVE)
#
# TWO THINGS THIS CANNOT REPRODUCE, and they are labelled on the sheet:
#   1. Vanilla invisibility is a SHADER with a noise texture. The colour and the 50%
#      alpha are exact; the noise/distortion is not reproducible outside Unity.
#   2. The aura uses ShaderDatabase.MoteGlow, which is ADDITIVE. GDI+ only does alpha
#      blending, so the aura reads softer here than in game, never brighter.
#
# LAYOUT NOTE, learned the hard way twice on this sheet: the halberd reaches about 1.1
# tiles from the pawn's centre (offset 0.34 + half of a 1.5-unit quad), so a ground
# patch the same size as the pawn quad CANNOT contain it - the first two versions had
# the axe running over the labels and into the next cell. Ground is drawn at 1.5x the
# pawn quad and everything is clipped to it.
#
# PowerShell traps this project has already paid for, honoured here:
#   - ',' binds tighter than '*', so every numeric array element is parenthesised
#   - variable names are CASE-INSENSITIVE: no count/collection sharing a word
#   - [Math]::Max(0, $double) truncates - use [double]0.0
# =================================================================================
Add-Type -AssemblyName System.Drawing

$MOD  = Split-Path -Parent $PSScriptRoot
$TEX  = Join-Path $MOD "Textures\Things\Pawn\DragonAspect"
# DOVAH_OVERLAY_DIR previews a DIFFERENT overlay set - Call of Valor's champion uses the same
# geometry with a spectral palette, and this shows him without writing anything into the mod.
if ($env:DOVAH_OVERLAY_DIR -and (Test-Path $env:DOVAH_OVERLAY_DIR)) { $TEX = $env:DOVAH_OVERLAY_DIR }
$AXET = Join-Path $MOD "Textures\Things\Item\Equipment\DovahkiinAncientAxe.png"
# DOVAH_AXE_OVERRIDE lets a candidate weapon be previewed in his hand WITHOUT writing it into
# the mod. Judging a weapon on its own and judging it held are different questions, and the
# shipping texture must not be touched to answer the second one.
if ($env:DOVAH_AXE_OVERRIDE -and (Test-Path $env:DOVAH_AXE_OVERRIDE)) { $AXET = $env:DOVAH_AXE_OVERRIDE }
$BODY_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$HEAD_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Gloomy Face Mod\Textures\Things\Pawn\Humanlike\Heads\Male"
$HEAD_KIND = "Male_Average_Pointy"   # the user's Dovahkiin "Leonid", per the notebook

$OUT = $env:DOVAH_PREVIEW
if (-not $OUT) { $OUT = $PSScriptRoot }

# --- geometry, all in world units then converted once ---------------------------
# 256, deliberately: every source sprite is a 256 frame, so at this size the main cells
# do NO resampling at all. At 208 the armour's scale field went through a 0.81 downscale
# and the regular scale grid aliased into a false fishnet that is not in the texture -
# and this art is signed off, so a preview must not invent a defect in it.
$CELL      = 256.0                  # px for one 1.5-world-unit body quad
$GROUND    = 384.0                  # 1.5x, so the halberd fits inside its own cell
$REF_WIDTH = 1.5                    # RefBodyWidth in the C#
$SCALE     = 1.5                    # HumanlikeBodyWidthForPawn for an ordinary adult

$ORBIT_INNER = 0.159
$ORBIT_OUTER = 0.241
$FLARE_INNER = 0.229
$FLARE_OUTER = 0.311
$RING_AZURE  = 1.276
$RING_EMBER  = 0.897

$HEAD_DX = 0.04              # BodyTypeDef Male headOffset.x, east/west only
$HEAD_DZ = 0.34              # headOffset.y, every rotation

$AXE_SIZE = 1.5              # graphicData.drawSize.x on the axe def

$C_EMBER = @(240, 118, 28)
$C_AZURE = @(72, 152, 238)
$C_WHITE = @(255, 255, 255)
$C_INVIS = @(191, 237, 250)  # 0.75/0.93/0.98 * 255
$INVIS_A = 0.5               # the alpha in InvisibilityMatPool's colour

# Thing_DragonAspectOverlay.Slots, verbatim: cycles-per-loop, phase, window, outward.
# Phases were SEARCHED, not chosen - hand-picked ones left frames with nothing alight.
$SLOTS = @(
  @(1, 0.342, 0.30, 0), @(2, 0.429, 0.24, 0), @(2, 0.153, 0.22, 0),
  @(1, 0.504, 0.28, 0), @(3, 0.333, 0.18, 0), @(2, 0.169, 0.22, 0),
  @(1, 0.850, 0.32, 0), @(2, 0.034, 0.20, 0), @(2, 0.834, 0.24, 0),
  @(1, 0.908, 0.26, 0), @(3, 0.779, 0.17, 0), @(1, 0.855, 0.29, 0),
  @(2, 0.647, 0.21, 0), @(2, 0.588, 0.23, 0),
  @(2, 0.375, 0.24, 1), @(1, 0.570, 0.28, 1), @(3, 0.586, 0.18, 1),
  @(2, 0.949, 0.22, 1), @(1, 0.209, 0.26, 1), @(2, 0.066, 0.23, 1),
  @(3, 0.864, 0.19, 1)
)

function RGB($r, $g, $b, $a = 255) {
  # Clamp HERE, at the one place colours are constructed. Raising the plate alpha once
  # threw 410 FromArgb exceptions because alpha is multiplied downstream in several
  # independent places; clamping at call sites only ever fixes the one you found.
  $ri = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$r))
  $gi = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$g))
  $bi = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$b))
  $ai = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$a))
  return [System.Drawing.Color]::FromArgb($ai, $ri, $gi, $bi)
}

# The same deterministic hash the game uses, so a preview frame IS a game frame.
function Hash01([int]$a, [int]$b, [int]$c) {
  $x = [Math]::Sin($a * 12.9898 + $b * 78.233 + $c * 37.719) * 43758.5453
  return $x - [Math]::Floor($x)
}

function LoadPng([string]$path) {
  if (-not (Test-Path $path)) { Write-Output "MISSING: $path"; return $null }
  return New-Object System.Drawing.Bitmap $path
}

# Rough LIT GROUND. Judging a translucent overlay on a dark backdrop is not a preview,
# it is a different question - that mistake cost two playtest rounds on this armour.
function DrawGround($gfx, [double]$x, [double]$y, [double]$size, [int]$salt) {
  $br = New-Object System.Drawing.SolidBrush (RGB 122 106 84 255)
  $gfx.FillRectangle($br, [single]$x, [single]$y, [single]$size, [single]$size)
  $br.Dispose()
  $step = 11
  for ($gy = 0; $gy -lt $size; $gy += $step) {
    for ($gx = 0; $gx -lt $size; $gx += $step) {
      $hsh = [Math]::Sin(($gx + 1) * 12.9898 + ($gy + 1) * 78.233 + $salt * 37.719) * 43758.5453
      $hsh = $hsh - [Math]::Floor($hsh)
      $d = [int](($hsh - 0.5) * 42.0)
      $c = RGB (122 + $d) (106 + $d) (84 + [int]($d * 0.8)) 255
      $b2 = New-Object System.Drawing.SolidBrush $c
      $gfx.FillRectangle($b2, [single]($x + $gx), [single]($y + $gy), [single]$step, [single]$step)
      $b2.Dispose()
    }
  }
}

# Draw an image tinted and alpha-scaled: the ColorMatrix multiply is what the game's
# MaterialPropertyBlock colour does. $mirror covers west from the east sprite, which is
# what Graphic_Multi does for free with only three files on disk.
function DrawTex($gfx, $img, [double]$cx, [double]$cy, [double]$w, [double]$h,
                 $col, [double]$alpha, [bool]$mirror = $false, [double]$angle = 0.0) {
  if ($img -eq $null -or $alpha -le 0.01) { return }
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single]($col[0] / 255.0)
  $cm.Matrix11 = [single]($col[1] / 255.0)
  $cm.Matrix22 = [single]($col[2] / 255.0)
  $cm.Matrix33 = [single]$alpha
  $cm.Matrix44 = [single]1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $st = $gfx.Save()
  $gfx.TranslateTransform([single]$cx, [single]$cy)
  if ($angle -ne 0.0) { $gfx.RotateTransform([single]$angle) }
  if ($mirror) { $gfx.ScaleTransform([single](-1.0), [single]1.0) }
  $r = New-Object System.Drawing.Rectangle ([int](-$w / 2)), ([int](-$h / 2)), ([int]$w), ([int]$h)
  $gfx.DrawImage($img, $r, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $gfx.Restore($st)
  $ia.Dispose()
}

# One crescent, placed the way the game places it: position and spin are INDEPENDENT,
# which is the whole reason the sprite is centred in its own frame rather than offset
# within it. Baking the orbit into the sprite welds a particle's position to its facing.
function DrawFlare($gfx, $img, [double]$cx, [double]$cy, [double]$size, [double]$posDeg,
                   [double]$orbit, [double]$spinDeg, $col, [double]$alpha, [bool]$mirror) {
  if ($img -eq $null -or $alpha -le 0.01) { return }
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single]($col[0] / 255.0)
  $cm.Matrix11 = [single]($col[1] / 255.0)
  $cm.Matrix22 = [single]($col[2] / 255.0)
  $cm.Matrix33 = [single]$alpha
  $cm.Matrix44 = [single]1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $st = $gfx.Save()
  $gfx.TranslateTransform([single]$cx, [single]$cy)
  $gfx.RotateTransform([single]$posDeg)
  $gfx.TranslateTransform([single]0.0, [single](-$orbit))
  $gfx.RotateTransform([single]$spinDeg)
  if ($mirror) { $gfx.ScaleTransform([single](-1.0), [single]1.0) }
  $r = New-Object System.Drawing.Rectangle ([int](-$size / 2)), ([int](-$size / 2)), ([int]$size), ([int]$size)
  $gfx.DrawImage($img, $r, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $gfx.Restore($st)
  $ia.Dispose()
}

# The crescents at one moment t of the 3.4s loop, running the REAL slot loop rather
# than a hand-picked set - so the count and spread on the sheet are what the game
# produces, not what looked good here. Returns how many were alight.
function DrawCrescents($gfx, [double]$cx, [double]$cy, [double]$t, $flare, $plain, [double]$px) {
  $lit = 0
  # No sprites means no aura - either the textures are missing, or DOVAH_NO_AURA stripped
  # them for Call of Valor, who has none. Returning 0 keeps the caption's "N crescents
  # alight" honest instead of reporting a count of things that were never drawn.
  if ($null -eq $flare -or $null -eq $plain) { return 0 }
  for ($i = 0; $i -lt $SLOTS.Count; $i++) {
    $k       = [int]$SLOTS[$i][0]
    $phase   = [double]$SLOTS[$i][1]
    $window  = [double]$SLOTS[$i][2]
    $outward = ([double]$SLOTS[$i][3]) -gt 0.5

    $pos = $t * $k + $phase
    $u = $pos - [Math]::Floor($pos)
    if ($u -ge $window) { continue }
    $cycle = ([int][Math]::Floor($pos)) % $k

    $life = $u / $window
    $vis = [Math]::Sin([Math]::PI * $life)
    $vis = $vis * [Math]::Sqrt([Math]::Max([double]0.0, $vis))
    if ($vis -le 0.02) { continue }

    # Re-roll EVERYTHING per appearance. Rolling per slot instead is what made the
    # effect read as a rota: each slot always came back at its own fixed angle.
    $hAng  = Hash01 $i $cycle 1
    $hRow  = Hash01 $i $cycle 2
    $hCol  = Hash01 $i $cycle 20
    $hMir  = Hash01 $i $cycle 4
    $hDir  = Hash01 $i $cycle 5
    $hSize = Hash01 $i $cycle 6
    $hSpin = Hash01 $i $cycle 7
    $hTumb = Hash01 $i $cycle 8

    $angle = $hAng * 360.0 + ($hDir - 0.5) * 90.0 * $life
    if ($outward) {
      $spin = -90.0 + ($hSpin - 0.5) * 64.0 + ($hTumb - 0.5) * 40.0 * $life
    } else {
      $spin = $hSpin * 360.0 + ($hTumb - 0.5) * 70.0 * $life
    }

    $outer = $hRow -gt 0.5
    $orbF = if ($outer) { $ORBIT_OUTER } else { $ORBIT_INNER }
    $flrF = if ($outer) { $FLARE_OUTER } else { $FLARE_INNER }
    $orbit = $orbF * $SCALE * (0.94 + 0.12 * $life) * $px
    $size  = $flrF * $SCALE * (0.90 + $hSize * 0.22) * (0.92 + 0.14 * $life) * $px

    # Half blended, a quarter flat ember, a quarter flat azure. The blended sprite
    # carries its own gradient and is drawn WHITE - tinting it multiplies one end to mud.
    if ($hCol -lt 0.50) { $img = $flare; $tint = $C_WHITE }
    elseif ($hCol -lt 0.75) { $img = $plain; $tint = $C_EMBER }
    else { $img = $plain; $tint = $C_AZURE }

    DrawFlare $gfx $img $cx $cy $size $angle $orbit $spin $tint $vis ($hMir -gt 0.5)
    $lit++
  }
  return $lit
}

# One full composite cell. $x,$y is the top-left of the GROUND patch; the pawn is
# centred in it at $CELL scale. Everything is clipped to the ground so a long weapon
# cannot run into the neighbouring cell or over the caption.
#
# Draw order is by the real y offsets in DrawAt: ring -0.004, armour 0, helm +0.005,
# axe +0.006 (but -0.006 and therefore BEHIND, facing north), crescents +0.010. The
# pawn itself sits on a lower altitude layer than any of them.
function DrawCell($gfx, [double]$x, [double]$y, [string]$rot,
                  $bodies, $heads, $armour, $helms, $ring, $flare, $plain, $axe,
                  [int]$level, [bool]$invisible, [bool]$withAxe, [double]$t, [int]$salt,
                  [bool]$showPawn = $true, [double]$cellPx = 0.0, [double]$groundPx = 0.0) {
  # NOT $cell / $ground. PowerShell variable names are CASE-INSENSITIVE, so a parameter
  # named $cell IS the script-scope $CELL - the assignment below would set the constant
  # to the parameter's own zero, every cell would draw at size 0, and the sheet came back
  # blank with no error at all. The notebook has this trap twice already; this is the
  # nastier flavour, a PARAMETER shadowing a global of the same word.
  if ($cellPx -le 0.0) { $cellPx = $CELL }
  if ($groundPx -le 0.0) { $groundPx = $cellPx * 1.5 }

  DrawGround $gfx $x $y $groundPx $salt
  $oldClip = $gfx.Clip
  $gfx.SetClip((New-Object System.Drawing.RectangleF ([single]$x), ([single]$y), ([single]$groundPx), ([single]$groundPx)))

  $px = $cellPx / $REF_WIDTH
  $cx = $x + $groundPx / 2.0
  $cy = $y + $groundPx / 2.0

  # west is east mirrored - what Graphic_Multi does from three files
  $mirror = ($rot -eq "west")
  $src = if ($mirror) { "east" } else { $rot }

  $pawnCol = if ($invisible) { $C_INVIS } else { $C_WHITE }
  $pawnA   = if ($invisible) { $INVIS_A } else { 1.0 }

  # head offset: BaseHeadOffsetAt(rot). x applies to east/west only, sign flipped west.
  $hdx = 0.0
  if ($rot -eq "east") { $hdx = $HEAD_DX }
  elseif ($rot -eq "west") { $hdx = -$HEAD_DX }
  $headCx = $cx + $hdx * $px
  $headCy = $cy - $HEAD_DZ * $px

  # facing north, the axe is drawn BEHIND the pawn so it does not cover their back
  if ($withAxe -and $rot -eq "north") {
    DrawTex $gfx $axe ($cx - 0.34 * $px) ($cy + 0.06 * $px) ($AXE_SIZE * $px) ($AXE_SIZE * $px) $C_WHITE 1.0 $false 205.0
  }

  if ($showPawn) {
    DrawTex $gfx $bodies[$src] $cx $cy $cellPx $cellPx $pawnCol $pawnA $mirror
    DrawTex $gfx $heads[$src] $headCx $headCy $cellPx $cellPx $pawnCol $pawnA $mirror
  }

  # two bands of underglow, azure wide and ember tight, breathing out of phase. They
  # sit BEHIND the armour. Collapsing them to one ring flattens the whole effect.
  if ($level -ge 3 -and $ring -ne $null) {
    $twoPi = 2.0 * [Math]::PI
    $aW = $RING_AZURE * $SCALE * (1.0 + 0.05 * [Math]::Sin($twoPi * $t)) * $px
    DrawTex $gfx $ring $cx $cy $aW $aW $C_AZURE (0.72 + 0.28 * [Math]::Sin($twoPi * $t))
    $eW = $RING_EMBER * $SCALE * (1.0 + 0.06 * [Math]::Sin($twoPi * $t + [Math]::PI)) * $px
    DrawTex $gfx $ring $cx $cy $eW $eW $C_EMBER (0.62 + 0.26 * [Math]::Sin($twoPi * $t + [Math]::PI))
  }

  DrawTex $gfx $armour[$src] $cx $cy $cellPx $cellPx $C_WHITE 1.0 $mirror

  if ($level -ge 3) {
    # The helm is drawn on the FULL BODY MESH at the real head offset - NOT scaled to
    # a fraction of the cell, which is what the older sheet did. The art is sized
    # inside its own frame to match a head plus horns.
    DrawTex $gfx $helms[$src] $headCx $headCy $cellPx $cellPx $C_WHITE 1.0 $mirror
  }

  if ($withAxe -and $rot -ne "north") {
    $adx = if ($rot -eq "west") { -0.30 } else { 0.34 }
    $ang = if ($rot -eq "west") { 200.0 } else { 145.0 }
    # DOVAH_AXE_ANGLE overrides the south/east hold angle, so alternatives can be compared
    # without editing the shipping code. DrawAt's 145 was eyeballed, never measured - the
    # preset says so - and with a weapon that has a distinctive pommel it reads head-DOWN.
    if ($env:DOVAH_AXE_ANGLE -and $rot -ne "west") { $ang = [double]$env:DOVAH_AXE_ANGLE }
    DrawTex $gfx $axe ($cx + $adx * $px) ($cy + 0.06 * $px) ($AXE_SIZE * $px) ($AXE_SIZE * $px) $C_WHITE 1.0 $false $ang
  }

  $n = 0
  if ($level -ge 3) {
    $n = DrawCrescents $gfx $cx $cy $t $flare $plain $px
  }

  $gfx.Clip = $oldClip
  return $n
}

# --- load every asset ----------------------------------------------------------
$ROTS = @("south", "north", "east")
$bodyImgs = @{}
$headImgs = @{}
$armImgs  = @{}
$helmImgs = @{}
foreach ($r in $ROTS) {
  $bodyImgs[$r] = LoadPng (Join-Path $BODY_DIR "Naked_Male_$r.png")
  $headImgs[$r] = LoadPng (Join-Path $HEAD_DIR "${HEAD_KIND}_$r.png")
  $armImgs[$r]  = LoadPng (Join-Path $TEX "DragonAspect_L2_Male_$r.png")
  $helmImgs[$r] = LoadPng (Join-Path $TEX "DragonAspectHelm_$r.png")
}
$ringImg  = LoadPng (Join-Path $TEX "DragonAspectAuraRing.png")
$flareImg = LoadPng (Join-Path $TEX "DragonAspectFlare.png")
$plainImg = LoadPng (Join-Path $TEX "DragonAspectFlarePlain.png")
$axeImg   = LoadPng $AXET

# DOVAH_NO_AURA strips the underglow ring and the crescent particles. Call of Valor has NO
# aura - that is the Dovahkiin's signature, and the user's rule - but he shares this
# harness, which was written for the Ancient Dragonborn and draws one unconditionally.
# Nulling the images rather than branching at each draw site: the ring is already
# null-guarded, and DrawCrescents now returns early, so a missing texture and a deliberate
# omission take the same path.
if ($env:DOVAH_NO_AURA) {
  if ($ringImg  -ne $null) { $ringImg.Dispose();  $ringImg  = $null }
  if ($flareImg -ne $null) { $flareImg.Dispose(); $flareImg = $null }
  if ($plainImg -ne $null) { $plainImg.Dispose(); $plainImg = $null }
  Write-Output "AURA: stripped (DOVAH_NO_AURA)"
}

# --- sheet ---------------------------------------------------------------------
# Captions are kept short and sit inside their own column. The first two versions ran
# them together into mush because the halberd overflowed the cell it belonged to.
$PAD = 30
$COLW = $GROUND
$sheetW = [int]($PAD * 4 + $COLW * 3)
$sheetH = [int]($GROUND * 3 + 450)   # 450 = three caption rows plus the whole footer
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($sheet)
$gfx.Clear((RGB 28 30 28 255))
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$fontH = New-Object System.Drawing.Font "Segoe UI", 17, ([System.Drawing.FontStyle]::Bold)
$fontS = New-Object System.Drawing.Font "Segoe UI", 11, ([System.Drawing.FontStyle]::Bold)
$fontT = New-Object System.Drawing.Font "Segoe UI", 10
$brWhite = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brGrey  = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brGold  = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)
$brWarn  = New-Object System.Drawing.SolidBrush (RGB 236 156 96 255)

$gfx.DrawString("THE ANCIENT DRAGONBORN - drawn with the game's own numbers", $fontH, $brWhite, [single]$PAD, [single]13)

function ColX([int]$i) { return $PAD + $i * ($COLW + $PAD) }

# ROW 1 - the three real facings (west is east mirrored, so it adds nothing)
$y1 = 56.0
$gfx.DrawString("HOW HE LOOKS - invisible pawn, level-3 armour, helm, aura, spectral halberd", $fontS, $brGold, [single]$PAD, [single]($y1 - 21))
$facings = @("south", "north", "east")
$litCounts = @()
for ($i = 0; $i -lt 3; $i++) {
  $x = ColX $i
  $n = DrawCell $gfx $x $y1 $facings[$i] $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $true $true 0.18 (11 + $i)
  $litCounts += $n
  $gfx.DrawString(("facing " + $facings[$i] + "   (" + $n + " crescents alight)"), $fontT, $brGrey, [single]$x, [single]($y1 + $GROUND + 4))
}

# ROW 2 - the comparison that answers "what is different about HIM"
$y2 = $y1 + $GROUND + 46
$gfx.DrawString("SIDE BY SIDE - all south-facing, same ground, same scale", $fontS, $brGold, [single]$PAD, [single]($y2 - 21))

$x = ColX 0
DrawGround $gfx $x $y2 $GROUND 41
$px = $CELL / $REF_WIDTH
DrawTex $gfx $bodyImgs["south"] ($x + $GROUND/2) ($y2 + $GROUND/2) $CELL $CELL $C_WHITE 1.0
DrawTex $gfx $headImgs["south"] ($x + $GROUND/2) ($y2 + $GROUND/2 - $HEAD_DZ * $px) $CELL $CELL $C_WHITE 1.0
$gfx.DrawString("1. an ordinary colonist, no shout", $fontT, $brGrey, [single]$x, [single]($y2 + $GROUND + 4))

$x = ColX 1
DrawCell $gfx $x $y2 "south" $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $false $false 0.18 42 | Out-Null
# THIS CAPTION LIED UNDER AN OVERRIDE. DOVAH_OVERLAY_DIR swaps the WHOLE texture set, so
# with Call of Valor's overlay loaded this cell showed the champion's palette while still
# claiming to be the Dovahkiin - and the sheet was sent to the user in that state more than
# once. A caption that asserts something the harness can invalidate has to check.
# "A stale number in a document that says 'this file is right' is worse than no number";
# the same is true of a label.
if ($env:DOVAH_OVERLAY_DIR -and (Test-Path $env:DOVAH_OVERLAY_DIR)) {
  $gfx.DrawString("2. the OVERRIDDEN overlay on a VISIBLE pawn", $fontT, $brGrey, [single]$x, [single]($y2 + $GROUND + 4))
  $gfx.DrawString("    NOT the Dovahkiin - DOVAH_OVERLAY_DIR is set", $fontT, $brGold, [single]$x, [single]($y2 + $GROUND + 21))
} else {
  $gfx.DrawString("2. the DOVAHKIIN, Dragon Aspect L3", $fontT, $brGrey, [single]$x, [single]($y2 + $GROUND + 4))
  $gfx.DrawString("    visible pawn, no weapon drawn", $fontT, $brGrey, [single]$x, [single]($y2 + $GROUND + 21))
}

$x = ColX 2
DrawCell $gfx $x $y2 "south" $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $true $true 0.18 43 | Out-Null
$gfx.DrawString("3. the ANCIENT DRAGONBORN", $fontT, $brWarn, [single]$x, [single]($y2 + $GROUND + 4))
$gfx.DrawString("    invisible pawn + halberd", $fontT, $brWarn, [single]$x, [single]($y2 + $GROUND + 21))

# ROW 3 - what "fully hidden" would look like, a second aura moment, and the weapon
$y3 = $y2 + $GROUND + 64
$gfx.DrawString("THE OPEN QUESTION, AND THE WEAPON", $fontS, $brGold, [single]$PAD, [single]($y3 - 21))

$x = ColX 0
DrawCell $gfx $x $y3 "south" $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $true $true 0.18 44 $false | Out-Null
$gfx.DrawString("4. NO pawn drawn at all - what a", $fontT, $brWarn, [single]$x, [single]($y3 + $GROUND + 4))
$gfx.DrawString("    'fully hidden' summon would look like", $fontT, $brWarn, [single]$x, [single]($y3 + $GROUND + 21))

$x = ColX 1
$n = DrawCell $gfx $x $y3 "south" $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $true $true 0.62 45 | Out-Null
$gfx.DrawString("5. another moment of the 3.4s aura loop", $fontT, $brGrey, [single]$x, [single]($y3 + $GROUND + 4))
$gfx.DrawString("    the crescents re-roll every appearance", $fontT, $brGrey, [single]$x, [single]($y3 + $GROUND + 21))

# the halberd, and how he will actually be seen at play distance
$x = ColX 2
$gfx.DrawString("6. the spectral halberd", $fontT, $brGrey, [single]$x, [single]($y3 - 2))
DrawGround $gfx $x ($y3 + 14) 148 46
DrawTex $gfx $axeImg ($x + 74) ($y3 + 88) 148 148 $C_WHITE 1.0
DrawGround $gfx ($x + 156) ($y3 + 14) 148 47
DrawTex $gfx $axeImg ($x + 230) ($y3 + 88) 148 148 $C_WHITE 1.0 $false 145.0
$gfx.DrawString("as authored", $fontT, $brGrey, [single]$x, [single]($y3 + 166))
$gfx.DrawString("as he holds it", $fontT, $brGrey, [single]($x + 156), [single]($y3 + 166))

$gfx.DrawString("at play distance:", $fontT, $brGrey, [single]$x, [single]($y3 + 190))
$zoomSizes = @(96, 64, 48)
$zcx = $x
$zbase = $y3 + 208
foreach ($zs in $zoomSizes) {
  $zg = [double]$zs * 1.5
  DrawCell $gfx $zcx $zbase "south" $bodyImgs $headImgs $armImgs $helmImgs $ringImg $flareImg $plainImg $axeImg 3 $true $true 0.18 77 $true ([double]$zs) $zg | Out-Null
  $gfx.DrawString(("" + $zs + "px"), $fontT, $brGrey, [single]$zcx, [single]($zbase + $zg + 2))
  $zcx += $zg + 8
}

# footer - what is exact and what is not. A silent approximation is a lie in a preview.
$fy = $y3 + $GROUND + 48
$gfx.DrawString("EXACT - read from the shipping code, the art, and the game:", $fontS, $brGold, [single]$PAD, [single]$fy)
$exact = @(
  "the armour, helm, aura and halberd are the ACTUAL shipping PNGs, untouched",
  "sizes and offsets come from Thing_DragonAspectOverlay.DrawAt; the helm is on the full body mesh at BaseHeadOffsetAt (0.04, 0.34)",
  "the 21-slot crescent table and its hash are copied verbatim, so these are real frames of the loop, not posed ones",
  "the invisible pawn is (191,237,250) at 50% alpha - InvisibilityMatPool's own static constructor",
  "body art is Beautiful Bodies, head art is Gloomy Face - both ACTIVE in your mod list; the head is Male_Average_Pointy, your Dovahkiin's"
)
$ly = $fy + 23
foreach ($line in $exact) {
  $gfx.DrawString(("  -  " + $line), $fontT, $brGrey, [single]$PAD, [single]$ly)
  $ly += 18
}
$gfx.DrawString("APPROXIMATE - these cannot be reproduced outside the game:", $fontS, $brWarn, [single]$PAD, [single]($ly + 8))
$ly = $ly + 31
$approx = @(
  "vanilla invisibility is a SHADER with a noise texture. The colour and the 50% alpha are exact; the shimmer over it is not drawable here.",
  "the aura uses an ADDITIVE shader (MoteGlow). GDI+ can only alpha-blend, so in game the aura glows MORE than this - never less.",
  "the halberd's angle in his hand is our reading of the draw code. Only the game settles how it sits."
)
foreach ($line in $approx) {
  $gfx.DrawString(("  -  " + $line), $fontT, $brGrey, [single]$PAD, [single]$ly)
  $ly += 18
}

$gfx.Dispose()
$path = Join-Path $OUT "ancient_dragonborn_preview.png"
$sheet.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

foreach ($r in $ROTS) {
  if ($bodyImgs[$r] -ne $null) { $bodyImgs[$r].Dispose() }
  if ($headImgs[$r] -ne $null) { $headImgs[$r].Dispose() }
  if ($armImgs[$r]  -ne $null) { $armImgs[$r].Dispose() }
  if ($helmImgs[$r] -ne $null) { $helmImgs[$r].Dispose() }
}
if ($ringImg -ne $null) { $ringImg.Dispose() }
if ($flareImg -ne $null) { $flareImg.Dispose() }
if ($plainImg -ne $null) { $plainImg.Dispose() }
if ($axeImg -ne $null) { $axeImg.Dispose() }

Write-Output ("sheet {0}x{1}" -f $sheetW, $sheetH)
Write-Output ("crescents alight per facing: " + ($litCounts -join ", "))
Write-Output ("wrote " + $path)
Write-Output "DONE"

