# =====================================================================================
#  CALL OF VALOR - THE PORTAL CAST EFFECT.
#
#  The user's spec, 2026-07-31, verbatim in substance: "bright white waves circling the
#  TARGET cell like an opening portal, not a wave from the caster."
#
#  WHY THIS CANNOT BE Thing_ShoutWave
#  ----------------------------------
#  Every other shout in this mod is a Thing_ShoutWave. That class is built around ONE
#  idea - a front that travels along a radius. Read its own code:
#    - origin is hard-set to caster.Position in Spawn(), and the wave is spawned there
#    - BuildRings buckets cells by DISTANCE from that origin
#    - Tick draws band `head = progress * bands`, i.e. the front marches outward
#    - `inward` reverses that march, and nothing else
#  There is no rotation anywhere in it, and no way to put the effect on a cell that is
#  not the caster's. A portal is the opposite shape: it does not travel, it SPINS, and it
#  sits on the target. Bending the wave class to do that would give both jobs to one class
#  and put a rotation branch on the path every shout in the mod already runs through.
#
#  So this is its own effect. In game that is Thing_ValorPortal - a RealtimeOnly Thing
#  spawned on the target cell, overriding DrawAt, drawing rotated quads. Exactly the route
#  Thing_DragonAspectOverlay already uses for the aura, so it needs no Harmony patch and
#  nothing on the pawn render path: Matrix4x4.TRS(pos, AngleAxis(a, up), scale) +
#  Graphics.DrawMesh(MeshPool.plane10, ...) with a MaterialPropertyBlock for the tint.
#
#  THIS SCRIPT IS THE PROOF, AND IT IS PREVIEW-ONLY.
#  The project's own rule is to prove a render approach and SHOW it before building
#  anything around it. So this draws the art, then composites it frame by frame using the
#  same quad arithmetic the game will use, and writes an animated GIF. No C# is written
#  and no texture is installed until the user has seen it move.
#
#  ---------------------------------------------------------------------------------
#  PowerShell traps honoured, all of which have cost this project time:
#    - no single-letter variables (a loop index colliding with a capitalised constant has
#      silently produced empty output four separate times)
#    - every element of a numeric array literal parenthesised: `,` binds tighter than `*`
#    - [double] on Math::Max, or it picks the int overload and truncates
#    - no function parameter shares a WORD with a script constant, in any case
# =====================================================================================
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\WriteAnimatedGif.ps1"

$WRITE_TEXTURE = $false        # nothing goes into the mod until the shape is approved

$OUT_DIR = $env:DOVAH_PREVIEW
if (-not $OUT_DIR) { $OUT_DIR = $PSScriptRoot }
$MOD_ROOT = Split-Path -Parent $PSScriptRoot
$TEX_DIR  = Join-Path $MOD_ROOT "Textures\Things\Effects\ValorPortal"
$BODY_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$HEAD_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Gloomy Face Mod\Textures\Things\Pawn\Humanlike\Heads\Male"
$HEAD_KIND = "Male_Average_Pointy"

# =====================================================================================
#  TUNING. Every one of these belongs in DovahkiinTuningDef when the effect is built -
#  CLAUDE.md forbids inline balance numbers. They are here so the preview can be re-run.
# =====================================================================================
$SPRITE       = 256           # texture frame, same as every other sprite in the mod
$PORTAL_TICKS = 90            # 1.5s at 60 ticks/s
$PORTAL_RADIUS = 1.10         # CELLS. A pawn draws 1.5 cells wide, so this is a gateway
$ARRIVE_AT    = 0.60          # fraction of life at which he steps through

# The arc sprite's own geometry, as fractions of the frame's half-width. R_ARC is what
# ties the sprite to the transform: an arc baked at 0.70 of the half-frame, drawn on a
# quad of S world units, lands at world radius S * 0.70 / 2. That inversion is the only
# arithmetic connecting art to code, so it is named once and used everywhere.
$R_ARC   = 0.70
# THICKNESS IS THE KNOB THAT DECIDES WHETHER THIS READS AS WAVES AT ALL. It looks like a
# detail and is not. The arc's world thickness is T_ARC/R_ARC times its orbit radius, so a
# fat arc on the outer orbit is fat in absolute terms too. At 0.082 the three orbits'
# gaussian tails overlapped once the glow was bright enough to read white, and every frame
# past half-open blew out into ONE solid ring - the exact thing "waves circling" is not.
$T_ARC   = 0.060              # radial half-thickness at the arc's HEAD
$SWEEP   = 132.0              # total degrees of arc

# Orbits: radius multiple, how many arcs, degrees per second, opacity, phase offset.
# Adjacent orbits COUNTER-ROTATE. Co-rotating rings read as one disc turning; opposed
# ones read as a mechanism opening, which is the whole ask.
# Arc counts are 3/2/4 rather than all equal so the composite does not repeat every
# 360/N degrees - the aura hit exactly that and it read as a stutter, not as motion.
$ORBITS = @(
  @{ Radius = 0.45; Count = 3; Spin =  310.0; Alpha = 0.78; Phase =  0.0 },
  @{ Radius = 0.73; Count = 2; Spin = -215.0; Alpha = 1.00; Phase = 40.0 },
  @{ Radius = 1.02; Count = 4; Spin =  152.0; Alpha = 0.70; Phase = 18.0 }
)

# --- COLOUR. The sprites are authored WHITE and tinted here, never baked: baking a
# colour into a glow freezes it, and this one needed changing within an hour of the first
# render.
#
# WHY THE TINT IS COOL RATHER THAN PURE WHITE, WHICH LOOKS LIKE A MISTAKE AND IS NOT.
# A glow is ADDITIVE, and the ground it lands on is brown - roughly (122,106,84). Adding
# equal amounts of R, G and B to that reaches full red long before full blue, so a white
# glow at anything short of clipping reads CREAM. The first render came out warm gold on
# every frame that was not the flash, which is not "bright white waves" by any reading.
# Biasing the tint cool puts the extra light where the ground has least, so the sum comes
# out neutral. The fix belongs here and not in the preview, because the game's additive
# shader will do exactly the same thing over exactly the same terrain.
$TINT_ARC  = @(206, 234, 255)
$TINT_CORE = @(234, 246, 255)

# How much light the effect adds, over and above the sprite's own alpha. In game this is
# the tint's brightness on the MaterialPropertyBlock - the same knob, one layer up.
# Below about 1.4 the faint parts of the arcs never clear the ground's own brown.
$GLOW_GAIN = 1.42

# Preview scale. Their armour sheet draws a 256px texture into a 256px box, and a body
# quad is 1.5 cells - so that sheet is 170.67px per cell. Matched here so the two
# previews can be compared directly.
$PX_PER_CELL = 256.0 / 1.5
$FRAME   = 512                # 3 cells across
$FPS     = 20
$TAIL_FRAMES = 9              # dark ground at the end, so the loop is not a hard cut

function RGB($red, $green, $blue, $alpha = 255) {
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$red))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$green))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$blue))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$alpha))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}

function Clamp01([double]$value) {
  if ($value -lt 0.0) { return 0.0 }
  if ($value -gt 1.0) { return 1.0 }
  return $value
}

function Smooth([double]$value) {
  $clamped = Clamp01 $value
  return ($clamped * $clamped * (3.0 - (2.0 * $clamped)))
}

# =====================================================================================
#  THE SPRITES - built per pixel.
#
#  Per pixel rather than by stacking GDI+ shapes, and that is not a style choice: 220
#  concentric FillEllipse discs of low alpha give a SOLID disc, because every disc also
#  covers the centre and the alpha accumulates there. A radial falloff has to be
#  evaluated from the radius. No supersampling is needed either - these are all smooth
#  gradients, and a gradient has no edges to alias.
#
#  All three are authored WHITE, with the shape carried entirely in the alpha channel.
#  Baking a colour into a glow freezes it; tinting at draw time keeps the colour a
#  tunable rather than a regeneration.
# =====================================================================================
function BuildSprite([string]$kind) {
  $bmp = New-Object System.Drawing.Bitmap $SPRITE, $SPRITE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $rect = New-Object System.Drawing.Rectangle 0, 0, $SPRITE, $SPRITE
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $stride = $data.Stride
  $buffer = New-Object 'byte[]' ($stride * $SPRITE)

  $half = $SPRITE / 2.0
  $halfSweep = $SWEEP / 2.0
  $peak = 0.0
  $alphaField = New-Object 'double[]' ($SPRITE * $SPRITE)

  for ($py = 0; $py -lt $SPRITE; $py++) {
    $dy = (($py + 0.5) - $half) / $half
    for ($px = 0; $px -lt $SPRITE; $px++) {
      $dx = (($px + 0.5) - $half) / $half
      $rad = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
      $alpha = 0.0

      if ($kind -eq "arc") {
        # 0 degrees at 12 o'clock, increasing CLOCKWISE - which is what
        # Quaternion.AngleAxis(a, Vector3.up) does to a quad under RimWorld's top-down
        # camera, and what GDI+ RotateTransform(+a) does in a y-down frame. The preview
        # and the game therefore turn the same way. Verify a rotation sign by rendering
        # it, never by reasoning about it: the sheet draws a direction arrow for this.
        $angle = [Math]::Atan2($dx, -$dy) * 180.0 / [Math]::PI
        if ([Math]::Abs($angle) -le $halfSweep) {
          $signed = $angle / $halfSweep                 # -1 tail .. +1 head
          $headness = Clamp01((($signed + 1.0) / 2.0))
          # Taper the THICKNESS towards the tail as well as the brightness. A constant
          # band reads as a segment of pipe; a wave has to narrow behind its head. Same
          # rule that turned the aura's flares from beads into flames: much longer than
          # wide, and narrowing as it goes.
          $thick = $T_ARC * (0.34 + (0.66 * $headness))
          $offset = ($rad - $R_ARC) / $thick
          $radial = [Math]::Exp(-($offset * $offset))
          $ramp = [Math]::Pow($headness, 1.45)
          # round the leading edge off, or the head is a guillotine cut
          $headFade = Smooth((1.0 - $signed) / 0.16)
          $alpha = $radial * $ramp * $headFade
        }
      }
      elseif ($kind -eq "core") {
        # The way through: bright in the middle, with a hotter rim so it reads as an
        # opening rather than as a lens flare sitting on the ground.
        $inner = [Math]::Exp(-(($rad / 0.34) * ($rad / 0.34)))
        $rimOff = ($rad - 0.60) / 0.17
        $rim = [Math]::Exp(-($rimOff * $rimOff))
        $alpha = Clamp01(($inner * 0.82) + ($rim * 0.58))
      }
      else {
        # A hairline ring at the arcs' own radius, faint - it anchors the spinning arcs
        # to a fixed edge so the eye has something to read them against. Stroke only, no
        # fill: a filled disc with a hot centre reads as a stud on the ground, a hollow
        # ring reads as something opening in it.
        $ringOff = ($rad - $R_ARC) / 0.024
        $alpha = [Math]::Exp(-($ringOff * $ringOff)) * 0.55
      }

      $alphaField[($py * $SPRITE) + $px] = $alpha
      if ($alpha -gt $peak) { $peak = $alpha }
    }
  }

  # Renormalise. A shape built from gaussians loses most of its peak amplitude to the
  # falloff, so guessing a constant here gives a sprite that is either invisible or
  # clipped. Find the real maximum and scale to the intended alpha.
  $target = 252.0
  if ($peak -lt 0.0001) { $peak = 1.0 }
  $gain = $target / $peak
  for ($py = 0; $py -lt $SPRITE; $py++) {
    for ($px = 0; $px -lt $SPRITE; $px++) {
      $value = [int][Math]::Round([Math]::Min([double]255.0, ($alphaField[($py * $SPRITE) + $px] * $gain)))
      $index = ($py * $stride) + ($px * 4)
      $buffer[$index]     = [byte]255      # B - authored white
      $buffer[$index + 1] = [byte]255      # G
      $buffer[$index + 2] = [byte]255      # R
      $buffer[$index + 3] = [byte]$value   # A - the shape lives here
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($buffer, 0, $data.Scan0, $buffer.Length)
  $bmp.UnlockBits($data)
  return $bmp
}

# =====================================================================================
#  THE TIMELINE. One function per curve, so each is judgeable on its own.
# =====================================================================================

# How far open, as a multiple of the full radius. Eases out - a portal snaps open and
# settles, it does not arrive at its size linearly.
function OpenAt([double]$time) {
  if ($time -lt 0.40) {
    $eased = 1.0 - [Math]::Pow((1.0 - ($time / 0.40)), 3.0)
    return (0.18 + (0.82 * $eased))
  }
  if ($time -lt 0.78) { return 1.0 }
  return (1.0 + (0.20 * (($time - 0.78) / 0.22)))     # blows outward as it dies
}

# Overall opacity of the arcs.
function BrightAt([double]$time) {
  if ($time -lt 0.13) { return (Smooth ($time / 0.13)) }
  if ($time -lt 0.74) { return 1.0 }
  return (Smooth ((1.0 - $time) / 0.26))
}

# The core, plus the flash at the moment he steps through.
function CoreAt([double]$time) {
  # 0.55 rather than 0.72: at the higher value the core washed out the inner orbit for the
  # whole middle of the effect, so the innermost wave stopped existing right when the
  # portal is most open. The core is the way THROUGH, not the effect.
  $base = 0.0
  if ($time -lt 0.50) { $base = (Smooth (($time - 0.08) / 0.42)) * 0.55 }
  elseif ($time -lt 0.72) { $base = 0.55 }
  else { $base = 0.55 * (Smooth ((1.0 - $time) / 0.28)) }
  # a short spike either side of the arrival
  $flashDist = [Math]::Abs($time - $ARRIVE_AT) / 0.075
  if ($flashDist -lt 1.0) {
    $base = $base + ((1.0 - ($flashDist * $flashDist)) * 0.95)
  }
  return (Clamp01 $base)
}

# Wind-up: the arcs turn slowly at first and spin up as the portal opens.
function SpinRateAt([double]$time) {
  return (0.42 + (0.58 * (Smooth ($time / 0.42))))
}

# =====================================================================================
#  COMPOSITING
#
#  The portal is drawn into its own transparent layer and then added onto the scene with
#  saturating arithmetic, because a glow in game is ADDITIVE and GDI+ has no additive
#  blend. Alpha-blending it instead would darken the ground under the effect, which is
#  the opposite of what light does, and would flatter nothing - it would make the effect
#  look weaker than it is. Thirty lines to stop the preview lying.
# =====================================================================================
function AddLayer($targetBmp, $layerBmp) {
  $wide = $targetBmp.Width; $high = $targetBmp.Height
  $rect = New-Object System.Drawing.Rectangle 0, 0, $wide, $high
  $dstData = $targetBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $srcData = $layerBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $count = $dstData.Stride * $high
  $dstBytes = New-Object 'byte[]' $count
  $srcBytes = New-Object 'byte[]' $count
  [System.Runtime.InteropServices.Marshal]::Copy($dstData.Scan0, $dstBytes, 0, $count)
  [System.Runtime.InteropServices.Marshal]::Copy($srcData.Scan0, $srcBytes, 0, $count)

  for ($index = 0; $index -lt $count; $index += 4) {
    $srcAlpha = $srcBytes[$index + 3]
    if ($srcAlpha -eq 0) { continue }
    $scale = ($srcAlpha / 255.0) * $GLOW_GAIN
    for ($channel = 0; $channel -lt 3; $channel++) {
      $sum = $dstBytes[$index + $channel] + ($srcBytes[$index + $channel] * $scale)
      if ($sum -gt 255.0) { $sum = 255.0 }
      $dstBytes[$index + $channel] = [byte][int]$sum
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($dstBytes, 0, $dstData.Scan0, $count)
  $targetBmp.UnlockBits($dstData)
  $layerBmp.UnlockBits($srcData)
}

function Ground($gfxRef, [double]$originX, [double]$originY, [double]$extent, [int]$salt) {
  $tile = 12
  for ($groundY = 0; $groundY -lt $extent; $groundY += $tile) {
    for ($groundX = 0; $groundX -lt $extent; $groundX += $tile) {
      $hashv = [Math]::Sin(((($groundX + 1) * 12.9898)) + ((($groundY + 1) * 78.233)) + (($salt * 37.719))) * 43758.5453
      $hashv = $hashv - [Math]::Floor($hashv)
      $delta = [int](($hashv - 0.5) * 42.0)
      $brush = New-Object System.Drawing.SolidBrush (RGB (122 + $delta) (106 + $delta) (84 + [int]($delta * 0.8)) 255)
      $gfxRef.FillRectangle($brush, [single]($originX + $groundX), [single]($originY + $groundY), [single]$tile, [single]$tile)
      $brush.Dispose()
    }
  }
}

# One tinted, rotated quad - the preview's stand-in for DrawQuad in
# Thing_DragonAspectOverlay. Deliberately mirrors its arithmetic: a quad of `quadCells`
# world units, centred on the position, rotated about up. Getting this wrong is how a
# preview invents a defect that is not in the art.
function DrawQuad($gfxRef, $sprite, [double]$centreX, [double]$centreY,
                  [double]$quadCells, [double]$angleDeg, [double]$opacity, $hue) {
  if ($opacity -le 0.004) { return }
  if ($null -eq $hue) { $hue = @(255, 255, 255) }
  $sizePx = $quadCells * $PX_PER_CELL
  $matrix = New-Object System.Drawing.Imaging.ColorMatrix
  $matrix.Matrix00 = [single]($hue[0] / 255.0)
  $matrix.Matrix11 = [single]($hue[1] / 255.0)
  $matrix.Matrix22 = [single]($hue[2] / 255.0)
  $matrix.Matrix33 = [single](Clamp01 $opacity)
  $attributes = New-Object System.Drawing.Imaging.ImageAttributes
  $attributes.SetColorMatrix($matrix)
  $state = $gfxRef.Save()
  $gfxRef.TranslateTransform([single]$centreX, [single]$centreY)
  $gfxRef.RotateTransform([single]$angleDeg)
  $box = New-Object System.Drawing.Rectangle ([int](-$sizePx / 2)), ([int](-$sizePx / 2)), ([int]$sizePx), ([int]$sizePx)
  $gfxRef.DrawImage($sprite, $box, 0, 0, $sprite.Width, $sprite.Height,
                    [System.Drawing.GraphicsUnit]::Pixel, $attributes)
  $gfxRef.Restore($state)
  $attributes.Dispose()
}

function DrawGhostPawn($gfxRef, $img, [double]$centreX, [double]$centreY, [double]$boxPx, [double]$opacity) {
  # vanilla's own invisibility colour, read off InvisibilityMatPool's cctor IL:
  # (0.75, 0.93, 0.98) at 50% alpha
  $matrix = New-Object System.Drawing.Imaging.ColorMatrix
  $matrix.Matrix00 = [single](191 / 255.0); $matrix.Matrix11 = [single](237 / 255.0)
  $matrix.Matrix22 = [single](250 / 255.0); $matrix.Matrix33 = [single](0.5 * $opacity)
  $attributes = New-Object System.Drawing.Imaging.ImageAttributes
  $attributes.SetColorMatrix($matrix)
  $box = New-Object System.Drawing.Rectangle ([int]($centreX - ($boxPx / 2))), ([int]($centreY - ($boxPx / 2))), ([int]$boxPx), ([int]$boxPx)
  $gfxRef.DrawImage($img, $box, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
  $attributes.Dispose()
}

# -------------------------------------------------------------------------------------
#  One frame. `spinState` is carried in from the caller and mutated, so the arcs' angles
#  are ACCUMULATED across frames rather than recomputed from t - the spin rate varies
#  over the effect's life, and recomputing would make the arcs jump when it changes.
# -------------------------------------------------------------------------------------
function RenderFrame([double]$time, $spriteMap, $spinState, [double]$radiusCells,
                     [bool]$withPawn, [int]$sizePx) {
  $frameBmp = New-Object System.Drawing.Bitmap $sizePx, $sizePx, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $frameGfx = [System.Drawing.Graphics]::FromImage($frameBmp)
  $frameGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  Ground $frameGfx 0 0 $sizePx 4

  $centre = $sizePx / 2.0
  $openFrac = OpenAt $time
  $bright = BrightAt $time
  $coreBright = CoreAt $time

  if ($withPawn -and $time -ge $ARRIVE_AT) {
    $arrival = Smooth ((($time - $ARRIVE_AT) / 0.14))
    $bodyImg = New-Object System.Drawing.Bitmap (Join-Path $BODY_DIR "Naked_Male_south.png")
    $headImg = New-Object System.Drawing.Bitmap (Join-Path $HEAD_DIR "${HEAD_KIND}_south.png")
    $pawnBox = 1.5 * $PX_PER_CELL
    DrawGhostPawn $frameGfx $bodyImg $centre $centre $pawnBox $arrival
    DrawGhostPawn $frameGfx $headImg $centre ($centre - (0.34 * $PX_PER_CELL)) $pawnBox $arrival
    $bodyImg.Dispose(); $headImg.Dispose()
  }

  # the portal itself, into its own layer so it can be ADDED rather than blended
  $glowBmp = New-Object System.Drawing.Bitmap $sizePx, $sizePx, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $glowGfx = [System.Drawing.Graphics]::FromImage($glowBmp)
  $glowGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

  # core first, so the arcs read over it
  $coreQuad = 2.0 * $radiusCells * $openFrac * 1.05
  DrawQuad $glowGfx $spriteMap["core"] $centre $centre $coreQuad 0.0 ($coreBright * 0.9) $TINT_CORE

  # the anchoring hairline
  $ringQuad = 2.0 * $radiusCells * $openFrac / $R_ARC
  DrawQuad $glowGfx $spriteMap["ring"] $centre $centre $ringQuad 0.0 ($bright * 0.55) $TINT_ARC

  for ($orbitIndex = 0; $orbitIndex -lt $ORBITS.Count; $orbitIndex++) {
    $orbit = $ORBITS[$orbitIndex]
    # invert the sprite's own baked radius to get the quad size - see $R_ARC
    $quadCells = (2.0 * $radiusCells * $orbit.Radius * $openFrac) / $R_ARC
    $arcCount = [int]$orbit.Count
    for ($arcIndex = 0; $arcIndex -lt $arcCount; $arcIndex++) {
      $angle = $spinState[$orbitIndex] + $orbit.Phase + (($arcIndex * 360.0) / $arcCount)
      DrawQuad $glowGfx $spriteMap["arc"] $centre $centre $quadCells $angle ($bright * $orbit.Alpha) $TINT_ARC
    }
  }
  $glowGfx.Dispose()
  AddLayer $frameBmp $glowBmp
  $glowBmp.Dispose()
  $frameGfx.Dispose()
  return $frameBmp
}

# =====================================================================================
#  RUN
# =====================================================================================
Write-Output "building sprites..."
$spriteMap = @{}
$spriteMap["arc"]  = BuildSprite "arc"
$spriteMap["core"] = BuildSprite "core"
$spriteMap["ring"] = BuildSprite "ring"

$lifeSeconds = $PORTAL_TICKS / 60.0
$liveFrames = [int][Math]::Round($lifeSeconds * $FPS)
$deltaTime = 1.0 / $FPS

Write-Output ("compositing " + ($liveFrames + $TAIL_FRAMES) + " frames...")
$spinState = New-Object 'double[]' $ORBITS.Count
$frameList = New-Object System.Collections.ArrayList
$keyFrames = @{}
$keyTimes = @(0.06, 0.18, 0.32, 0.46, 0.60, 0.72, 0.86, 0.97)

for ($frameIndex = 0; $frameIndex -lt $liveFrames; $frameIndex++) {
  $time = $frameIndex / [double]($liveFrames - 1)
  $rendered = RenderFrame $time $spriteMap $spinState $PORTAL_RADIUS $false $FRAME
  [void]$frameList.Add($rendered)

  # nearest live frame to each key time, kept at full quality for the still sheet
  foreach ($keyTime in $keyTimes) {
    $best = $keyFrames[$keyTime]
    if ($null -eq $best -or [Math]::Abs($time - $keyTime) -lt $best.Delta) {
      $keyFrames[$keyTime] = @{ Delta = [Math]::Abs($time - $keyTime); Time = $time }
    }
  }

  # accumulate, do not recompute - the rate changes over the effect's life
  $rate = SpinRateAt $time
  for ($orbitIndex = 0; $orbitIndex -lt $ORBITS.Count; $orbitIndex++) {
    $spinState[$orbitIndex] = $spinState[$orbitIndex] + ($ORBITS[$orbitIndex].Spin * $deltaTime * $rate)
  }
}
# a beat of bare ground before the loop repeats, so it does not cut hard back to the start
for ($tailIndex = 0; $tailIndex -lt $TAIL_FRAMES; $tailIndex++) {
  $bare = New-Object System.Drawing.Bitmap $FRAME, $FRAME, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bareGfx = [System.Drawing.Graphics]::FromImage($bare)
  Ground $bareGfx 0 0 $FRAME 4
  $bareGfx.Dispose()
  [void]$frameList.Add($bare)
}

$gifPath = Join-Path $OUT_DIR "valor_portal.gif"
$writtenCount = Write-AnimatedGif -Frames ([System.Drawing.Bitmap[]]$frameList.ToArray()) -Path $gifPath -DelayHundredths ([int](100 / $FPS))
Write-Output ("wrote " + $gifPath + " (" + $writtenCount + " frames)")

# =====================================================================================
#  THE STILL SHEET - full quality, no GIF quantisation, for judging detail.
#  GIF is 256 colours and .NET dithers to reach them, which speckles a soft glow. That
#  speckle is the FORMAT, not the effect, and the sheet says so on its own face.
# =====================================================================================
$CELL_PX = 236
$COLS = 4
$sheetW = ($COLS * $CELL_PX) + (($COLS + 1) * 18)
# tall enough for two rows of stills, their labels, and the bottom strip WITH its labels.
# The first version guessed this and cropped the whole bottom row off the sheet.
$sheetH = 62 + (2 * ($CELL_PX + 40)) + 24 + 192 + 40
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sheetGfx = [System.Drawing.Graphics]::FromImage($sheet)
$sheetGfx.Clear((RGB 28 30 28 255))
$sheetGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontBig = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontMid = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Bold)
$fontSml = New-Object System.Drawing.Font "Segoe UI", 9.5
$brushW = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brushG = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brushO = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)

$sheetGfx.DrawString("CALL OF VALOR - the PORTAL. Bright white waves circling the TARGET cell.", $fontBig, $brushW, [single]18, [single]12)
$sheetGfx.DrawString("Three counter-rotating orbits of tapering arcs + a core + an anchoring hairline. Not a wave from the caster - it spins, and it sits on the target.", $fontSml, $brushG, [single]18, [single]36)

$col = 0; $row = 0
foreach ($keyTime in $keyTimes) {
  $posX = 18 + ($col * ($CELL_PX + 18))
  $posY = 62 + ($row * ($CELL_PX + 40))
  # re-spin from zero to the key time, so each still matches the animation exactly
  $replay = New-Object 'double[]' $ORBITS.Count
  $stopAt = [int][Math]::Round($keyTime * ($liveFrames - 1))
  for ($stepIndex = 0; $stepIndex -lt $stopAt; $stepIndex++) {
    $stepTime = $stepIndex / [double]($liveFrames - 1)
    $stepRate = SpinRateAt $stepTime
    for ($orbitIndex = 0; $orbitIndex -lt $ORBITS.Count; $orbitIndex++) {
      $replay[$orbitIndex] = $replay[$orbitIndex] + ($ORBITS[$orbitIndex].Spin * $deltaTime * $stepRate)
    }
  }
  $showPawn = ($keyTime -ge $ARRIVE_AT)
  $still = RenderFrame ($stopAt / [double]($liveFrames - 1)) $spriteMap $replay $PORTAL_RADIUS $showPawn $FRAME
  $sheetGfx.DrawImage($still, (New-Object System.Drawing.Rectangle ([int]$posX), ([int]$posY), $CELL_PX, $CELL_PX))
  $still.Dispose()
  $label = "t = " + $keyTime.ToString("0.00") + "   (" + [int]($keyTime * $PORTAL_TICKS) + " ticks)"
  if ($keyTime -ge $ARRIVE_AT) { $label = $label + "  he steps through" }
  $sheetGfx.DrawString($label, $fontMid, $brushO, [single]$posX, [single]($posY + $CELL_PX + 4))
  $col++
  if ($col -ge $COLS) { $col = 0; $row++ }
}

# play distance. Their armour sheet's 96/64/48px boxes are 64/42/32 px per CELL; the
# portal frame is 3 cells, so these are the matching widths.
$zoomY = 62 + (2 * ($CELL_PX + 40)) + 24
$sheetGfx.DrawString("at play distance (the frame is 3 cells across):", $fontSml, $brushG, [single]18, [single]($zoomY - 18))
$zoomX = 18
$peakStill = RenderFrame 0.46 $spriteMap (New-Object 'double[]' $ORBITS.Count) $PORTAL_RADIUS $false $FRAME
foreach ($zoomPx in @(192, 128, 96)) {
  $sheetGfx.DrawImage($peakStill, (New-Object System.Drawing.Rectangle ([int]$zoomX), ([int]$zoomY), $zoomPx, $zoomPx))
  $sheetGfx.DrawString(("" + [int]($zoomPx / 3.0) + " px/cell"), $fontSml, $brushG, [single]$zoomX, [single]($zoomY + $zoomPx + 3))
  $zoomX += $zoomPx + 16
}
$peakStill.Dispose()

# size comparison, since the radius is the one number that changes the read most
$sheetGfx.DrawString("portal RADIUS, in cells - a pawn draws 1.5 cells wide:", $fontSml, $brushG, [single]$zoomX, [single]($zoomY - 18))
foreach ($testRadius in @(0.85, 1.10, 1.35)) {
  $sizeStill = RenderFrame 0.46 $spriteMap (New-Object 'double[]' $ORBITS.Count) $testRadius $true $FRAME
  $sheetGfx.DrawImage($sizeStill, (New-Object System.Drawing.Rectangle ([int]$zoomX), ([int]$zoomY), 148, 148))
  $sizeStill.Dispose()
  $tag = $testRadius.ToString("0.00")
  if ($testRadius -eq $PORTAL_RADIUS) { $tag = $tag + "  <- default" }
  $sheetGfx.DrawString($tag, $fontSml, $brushO, [single]$zoomX, [single]($zoomY + 151))
  $zoomX += 164
}
$sheetGfx.Dispose()

$sheetPath = Join-Path $OUT_DIR "valor_portal_frames.png"
$sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
Write-Output ("wrote " + $sheetPath)

# DOVAH_DEST writes the three sprites somewhere WITHOUT installing them into the mod - the
# same escape hatch GenerateDragonAspect.ps1 has, and added for the same reason.
#
# The user asked "your portal wave is saved too?" and the honest answer was: only half. The
# GENERATOR was committed; the sprites it produces were not in any checkpoint, so a later
# edit here could have silently changed art they had already approved with nothing to catch
# it. Every other approved piece is hash-checked against Tools/ValorApproved_2026-07-31/;
# the portal had no such cover.
#
# Deterministic output is NOT a substitute for a snapshot. It means the sprites can be
# rebuilt - it does not tell anyone the rebuild still matches what was signed off.
$SNAP_DIR = $env:DOVAH_DEST
if ($SNAP_DIR) {
  if (-not (Test-Path $SNAP_DIR)) { New-Item -ItemType Directory -Force -Path $SNAP_DIR | Out-Null }
  $spriteMap["arc"].Save((Join-Path $SNAP_DIR "ValorPortalArc.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $spriteMap["core"].Save((Join-Path $SNAP_DIR "ValorPortalCore.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $spriteMap["ring"].Save((Join-Path $SNAP_DIR "ValorPortalRing.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("wrote 3 sprites to " + $SNAP_DIR + " (NOT installed in the mod)")
}

if ($WRITE_TEXTURE) {
  if (-not (Test-Path $TEX_DIR)) { New-Item -ItemType Directory -Force -Path $TEX_DIR | Out-Null }
  $spriteMap["arc"].Save((Join-Path $TEX_DIR "ValorPortalArc.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $spriteMap["core"].Save((Join-Path $TEX_DIR "ValorPortalCore.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $spriteMap["ring"].Save((Join-Path $TEX_DIR "ValorPortalRing.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("INSTALLED 3 textures into " + $TEX_DIR)
} else {
  Write-Output "preview only - nothing written into the mod"
}

foreach ($spriteKey in @("arc", "core", "ring")) { $spriteMap[$spriteKey].Dispose() }
foreach ($frameBmp in $frameList) { $frameBmp.Dispose() }
Write-Output "DONE"
