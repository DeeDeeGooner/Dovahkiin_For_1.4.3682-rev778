# =====================================================================================
#  CALL OF VALOR - Hun Kaal Zoor. The hero's SPECTRAL greatsword.
#
#  Built from scratch 2026-07-30 after the user rejected the previous attempts with the
#  decisive note: "you still draw it in a way that behaves like a material weapon, lighting
#  effect white bands, shading, etc". That was exactly right and it is the whole brief.
#
#  ---------------------------------------------------------------------------------
#  A GHOST IS EMISSIVE, NOT LIT. THIS IS THE POINT OF THE WHOLE FILE.
#  ---------------------------------------------------------------------------------
#  Every earlier version rendered a solid object being lit from somewhere: a hard two-tone
#  bevel down the blade, a specular on the pommel, a dark keyline, shaded faces. All of that
#  says "this is a metal thing catching light", which is precisely what a spirit weapon is
#  not. Deleted, not adjusted - it was the wrong model, not the wrong numbers.
#
#  What replaces it:
#    - the body is TRANSLUCENT. You can see the ground through it. Nothing else says ghost
#      as directly, and it is the one thing none of the earlier attempts did.
#    - the EDGES glow, rather than faces being lit. A rim of near-white light round the
#      silhouette, brightest where the shape is thinnest.
#    - an outer BLOOM, several wide low-alpha passes, so it bleeds into the air around it.
#    - the engraving GLOWS instead of being a dark inlay. On the reference the knotwork is
#      cut dark into steel; on a spirit blade it is the light showing through.
#    - NO specular, NO bevel split, NO cast shading, NO dark keyline.
#
#  The dark keyline is the notable casualty. SPEC-adjacent lore in this project says a
#  keyline is not optional because a coloured shape on lit ground reads washed out - and that
#  is true FOR A SOLID OBJECT. A ghost is allowed to be soft; what it is not allowed to be is
#  illegible, so variant B keeps a faint dark separation and the sheet shows both at 48px.
#
#  ---------------------------------------------------------------------------------
#  SILHOUETTE: traced from the user's reference, a Nordic-carved greatsword
#  ---------------------------------------------------------------------------------
#  Its distinctive features, in order up the weapon:
#    squared pommel, a wrapped lower grip, a HORNED lower guard, a second wrapped section,
#    a larger HORNED upper guard, then a blade that STEPS rather than tapering smoothly -
#    a broad flared base, a step in, a long shaft, a second flare near the tip, then the
#    point. The steps and the upswept horns are what make it read as ancient rather than as
#    a generic sword; a smooth taper is what made the last attempt look like anybody's sword.
#
#  ---------------------------------------------------------------------------------
#  BEHAVIOUR - unchanged, and the reason this is its own weapon and not a tint of theirs
#  ---------------------------------------------------------------------------------
#  Our own standalone ThingDef, borrowing only DankPyon_MeleeWeapon_Greatsword's behaviour:
#    drawSize (1.25,1.25) - the Melee Animation tweak's ScaleX/ScaleY are 1.25 and MUST match
#    Mass 3, handle Poke 9 at 2s, edge Cut 31.25 at 3.15s with 0.25 AP
#    tweak: OffX 0.5461391, OffY -0.0112994611, Rotation 45.0,
#           BladeStart 0.305084735, BladeEnd 1.382298, MeleeWeaponType 6 (two-handed sword)
#    hold: no equippedAngleOffset - they use VFECore weaponDraftedDrawOffsets,
#          south/east -45, north 115, west 45. Authored angles, the right starting point.
#  WRITING IT STANDALONE DROPS CompEquippable, which lives on vanilla's BaseWeapon. Without
#  it RimWorld logs "is equipment but has no CompEquippable" and the summon fails outright -
#  that is what killed the Ancient Dragonborn's first playtest. Add it by hand.
#
#  ORIENTATION: bottom-left to top-right, tip at TOP-RIGHT, as all three of their two-handers
#  are. Tweak values live in their texture's frame, so a mirrored sprite is gripped by the blade.
#
#  PowerShell traps honoured: no single-letter variables, every numeric array element
#  parenthesised, [double] on Math::Max, no C-style casts.
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$WRITE_TEXTURE = $false

$DEST_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\Things\Item\Equipment"
$OUT_DIR = $env:DOVAH_PREVIEW
if (-not $OUT_DIR) { $OUT_DIR = $PSScriptRoot }

$SIZE   = 256
$SS     = 4
$CANVAS = $SIZE * $SS

# =====================================================================================
#  PALETTE - THE HERO'S OWN, not a lookalike of it.
# =====================================================================================
#  This file used to carry its own numbers: (196,232,255), (120,168,216), (168,216,255).
#  The armour's were (206,232,252), (120,162,200), (120,196,255). Near enough that nobody
#  could see the difference on the two sprites separately, and far enough that they were
#  two palettes rather than one - which drifts the moment either side is retuned, and the
#  drift only shows when the weapon is in his hand.
#
#  Now dot-sourced from the single source, and mapped BY ROLE rather than by eye. Each
#  entry is given the job it already does on the armour:
#
#      C_HOT       his hot edge, pure light    -> the weapon's luminous rim
#      C_GOLD      his lit face                -> the bright end of the blade's gradient
#      C_MID       his pale steel-blue         -> the dim end of it, at the hilt
#      C_AZURE     his aura's second colour    -> the weapon's outer bloom
#
#  Matching by role is what makes this a handoff rather than a coincidence. C_DEEP is
#  deliberately NOT used for the body: it shades plate, and at 104 alpha on a translucent
#  blade it would just make the hilt murky.
# -------------------------------------------------------------------------------------
. "$PSScriptRoot\ValorPalette.ps1"

$C_RIM    = $C_HOT             # the luminous edge
$C_BODY   = $C_GOLD            # the translucent interior, bright end - toward the tip
$C_BODY_D = $C_MID             # the translucent interior, dim end - at the hilt
$C_BLOOM  = $C_AZURE           # the halo bled into the air
$C_DARK   = $C_BLUE_DEEP       # ONLY used by variant B, as a faint separation

# --- a touch of grey, WEAPON ONLY --------------------------------------------------
# The user's last note: "add a final tiny bit of grey to its color". It belongs HERE and
# not in ValorPalette.ps1 - that file is shared, and greying it would grey his armour with
# the weapon. The palette stays canonical; the sword applies a named tweak on top of it.
# Anyone comparing the two later will find this rather than concluding they have drifted.
#
# Desaturated toward each colour's OWN luminance, not toward a fixed grey. Mixing toward
# mid-grey would darken the bright end and lighten the dim one - which is a contrast change
# wearing a saturation change's clothes. Rec.709 luma, so the perceived brightness of every
# stop survives untouched and only the colour comes out of it.
# TWO KNOBS, NOT ONE. "Grey-darker" is two requests, and this project has been caught before
# by treating saturation and value as a single lever - see the notebook on opacity and
# brightness. Kept apart, either can be retuned without disturbing the other.
#
# BOTH ZEROED 2026-07-31, AND THIS REVERSES AN EARLIER REQUEST OF THE USER'S ON PURPOSE.
# They asked for grey, then grey-darker, and then reported the weapon as STILL too ghostly
# and asked for the chestplate's colour instead. Those pull against each other, and
# measurement settled which way:
#
#     chestplate median RGB   (159, 195, 220)   a proper pale steel-blue
#     sword      median RGB   (168, 186, 202)   flatter, redder, far less blue
#
# The grey was the cause of the very thing being complained about. Desaturating a
# translucent object pushes it TOWARD the mid-tone of the ground behind it, so it loses
# separation and reads as vapour - which is exactly what "ghostly" describes. Opacity had
# already been raised twice by then and could not fix it, because opacity was not the
# problem. **Saturation is what separates a translucent object from lit ground; value and
# alpha alone cannot.**
#
# Left as named knobs at neutral rather than deleted, so the greyer look is one number away
# if it is ever wanted back.
$GREY_MIX  = 0.00   # how far toward neutral - ZERO is what fixed "too ghostly"
$VALUE_MUL = 0.90   # and then how much darker - only enough to sit on the plate's value
# With grey at zero the blade measured (182,212,236) against the plate's (159,195,220):
# the right hue at last, but about 13% brighter. 0.90 brings the value onto the plate's
# without touching saturation, which is the half that was doing the damage.

function GreyToward($colour, [double]$amount) {
  $luma = (0.2126 * $colour[0]) + (0.7152 * $colour[1]) + (0.0722 * $colour[2])
  return @(
    ([int][Math]::Round($colour[0] + (($luma - $colour[0]) * $amount))),
    ([int][Math]::Round($colour[1] + (($luma - $colour[1]) * $amount))),
    ([int][Math]::Round($colour[2] + (($luma - $colour[2]) * $amount)))
  )
}
function DarkenBy($colour, [double]$factor) {
  return @(
    ([int][Math]::Round($colour[0] * $factor)),
    ([int][Math]::Round($colour[1] * $factor)),
    ([int][Math]::Round($colour[2] * $factor))
  )
}

# THE RIM IS GREYED BUT NOT DARKENED, and that is deliberate. It is the luminous edge - the
# light the apparition gives off - and this weapon has no keyline, no bevel and no specular,
# so the rim is the ONLY thing holding its shape against lit ground. Dimming it would trade
# a colour note for legibility. The body and the bloom carry the darkening instead.
# THE INTERIOR, DARKENED FURTHER - and only the interior.
#
# Asked for as "as dark as the helmet". Measured rather than matched by eye, because the
# three pieces are further apart than they look:
#
#     helmet      (151,166,179)   darker AND flatter
#     chestplate  (159,195,220)   the blue one
#     sword       (164,192,214)   sitting on the chestplate
#
# Mean channel ratio helm/sword came to 0.874, so that is the figure.
#
# **VALUE ONLY. The helm's flatness is NOT copied.** It is less saturated than the plate
# because its dome runs down to C_DEEP at the edges - but desaturation is exactly what made
# this weapon read as vapour two rounds ago, and taking the helm's colour wholesale would
# walk straight back into it. The ask was "darker"; darker is what it gets.
#
# Applied to the BODY and the hilt's blue, NOT to the bloom or the rim. The bloom is the
# halo outside the weapon and the rim is its luminous edge - neither is interior, and
# dimming them is how a spectre stops being legible on lit ground.
$INTERIOR_MUL = 0.874

$C_RIM    = GreyToward $C_RIM    $GREY_MIX   # already neutral, so this is a no-op on it
$C_BODY   = DarkenBy (DarkenBy (GreyToward $C_BODY   $GREY_MIX) $VALUE_MUL) $INTERIOR_MUL
$C_BODY_D = DarkenBy (DarkenBy (GreyToward $C_BODY_D $GREY_MIX) $VALUE_MUL) $INTERIOR_MUL
$C_BLOOM  = DarkenBy (GreyToward $C_BLOOM  $GREY_MIX) $VALUE_MUL

# --- HIS BLUE, through the hilt only ------------------------------------------------
# A gradient of the hero's blue from the pommel up to the second crossguard, fading out
# there. The blade above it keeps the grey steel.
#
# THE BLUE IS DARKENED BUT NOT GREYED, and that is the point of adding it. $GREY_MIX is
# what pulled the weapon toward steel; putting the blue through it as well would cancel
# the request before it drew a pixel. It takes $VALUE_MUL only, so it sits in the same
# value range as everything around it and reads as more COLOUR rather than as a brighter
# patch.
#
# C_AZURE is the aura's own colour on the armour - the most saturated blue he carries -
# so the hilt is quoting a part of him rather than a blue chosen to look nice.
# Takes $INTERIOR_MUL as well - it is mixed INTO the body, so leaving it out would light
# the hilt brighter than the blade it belongs to.
$C_HILT_BLUE   = DarkenBy (DarkenBy $C_AZURE $VALUE_MUL) $INTERIOR_MUL
$HILT_BLUE_END = 0.352   # the second crossguard's upper edge, where it reaches zero
$HILT_BLUE_MIX = 0.55    # strength at the pommel

# How solid the interior is. Lower = ghostlier.
#
# RAISED FROM 104. The user reported the weapon still reading as more ghostly than the hero,
# and it was - it had been set at 104 back when the armour was a faint scale field, and the
# armour has since been rebuilt around solid plate. The weapon's number never followed.
#
# THE FIRST CORRECTION WAS 152, TO MATCH THE CUIRASS'S OWN CONSTANT, AND THAT WAS WRONG -
# measurement caught it. Median interior alpha came back **sword 173, cuirass 215**, still
# 42 points apart after supposedly matching. The reason: the cuirass is not one fill. It is
# a plate body, then pectoral domes, then creases and lit lips, then a rim - four or five
# translucent layers accumulating over each other - while the blade is essentially one.
# **The shared constant is not the shared appearance.** Two pieces match when their
# COMPOSITED result matches, and that has to be measured on the finished textures.
#
# 196 brought the blade's measured interior level with his plate - 206 against 215. The user
# then asked for a bit more still, so parity was the waypoint rather than the destination:
# 220 puts the weapon slightly ABOVE his armour, which is defensible on its own terms since
# a blade is a forged object and the plate is a translucent overlay on a body.
#
# Still translucent, and that is the floor this must not cross - "you can see the ground
# through it" is the one thing none of the pre-2026-07-30 attempts did and the whole reason
# this file was rebuilt. Measured after every change, not assumed.
$BODY_ALPHA  = 220
$RIM_ALPHA   = 236
$GLYPH_ALPHA = 150

# --- geometry: the full diagonal ---------------------------------------------------
$BUTT_X = 0.060; $BUTT_Y = 0.952
$TIP_X  = 0.952; $TIP_Y  = 0.060

# THE PROFILE, traced from the reference. (along, half-width), both fractions of length.
# A STEPPED blade, not a smooth taper - repeated points at the same 'along' are the steps.
$PROFILE = @(
  @( (0.000), (0.0230) ),   # pommel cap
  @( (0.035), (0.0270) ),
  @( (0.045), (0.0175) ),   # step in to the lower grip
  @( (0.150), (0.0175) ),
  @( (0.160), (0.0620) ),   # LOWER GUARD, horns spread
  @( (0.196), (0.0620) ),
  @( (0.206), (0.0180) ),   # step back in to the middle grip
  @( (0.300), (0.0180) ),
  @( (0.312), (0.0790) ),   # UPPER GUARD, wider
  @( (0.352), (0.0790) ),
  @( (0.364), (0.0300) ),   # blade root
  @( (0.396), (0.0560) ),   # flare out - the broad base
  @( (0.470), (0.0520) ),
  @( (0.492), (0.0380) ),   # step in
  @( (0.760), (0.0350) ),   # the long shaft, barely tapering
  @( (0.792), (0.0470) ),   # second flare, near the tip
  @( (0.842), (0.0420) ),
  @( (0.868), (0.0330) )    # THE YOKOTE - where the symmetric blade ends and the tip begins
)

# ------------------------------------------------------------------------------------
# THE TIP IS ASYMMETRIC - a katana's kissaki, or a glaive's head.
#
# $PROFILE cannot express this: it stores ONE half-width per station and mirrors it, which
# can only ever produce a symmetric point. So the blade is symmetric up to the yokote and
# the tip is built explicitly, one side at a time.
#
# Which side is which: the sprite runs bottom-left to top-right, so -perp is the upper-left
# face. On a blade pointing up-right that upper-left line is the SPINE, and +perp is the
# EDGE. The point therefore sits offset toward -perp, with the edge sweeping up to meet the
# spine - which is what makes it read as a single-edged blade rather than a chisel.
#
# Two shapes offered because they are genuinely different weapons:
#   KATANA - a short kissaki, a restrained slant, the point close to the spine line
#   GLAIVE - a longer, broader forward sweep with more belly to the edge
# ------------------------------------------------------------------------------------
$TIP_KATANA = @{
  Edge  = @( @((0.900), ( 0.0268)), @((0.949), ( 0.0170)), @((0.985), ( 0.0062)) )
  Point =    @((1.000), (-0.0070))
  Spine = @( @((0.962), (-0.0224)), @((0.906), (-0.0300)) )
}
$TIP_GLAIVE = @{
  Edge  = @( @((0.898), ( 0.0400)), @((0.938), ( 0.0396)), @((0.975), ( 0.0250)) )
  Point =    @((1.000), (-0.0090))
  Spine = @( @((0.958), (-0.0230)), @((0.902), (-0.0306)) )
}

# The upswept HORNS on both guards - drawn separately, because they sweep back toward the
# blade and a symmetric profile cannot express that.
$HORNS = @(
  @( (0.160), (0.0620), (0.055), (1.9) ),   # along, from, length, sweep
  @( (0.312), (0.0790), (0.068), (2.1) )
)

function RGB($red, $green, $blue, $alpha = 255) {
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$red))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$green))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$blue))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$alpha))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}
function Lerp3($from, $to, [double]$amount) {
  if ($amount -lt 0) { $amount = 0 }
  if ($amount -gt 1) { $amount = 1 }
  return @(
    ([int]($from[0] + ($to[0]-$from[0])*$amount)),
    ([int]($from[1] + ($to[1]-$from[1])*$amount)),
    ([int]($from[2] + ($to[2]-$from[2])*$amount))
  )
}

# $colder: swap the palette toward a saturated spirit-blue instead of near-neutral white.
# $bodyAlpha: how solid the interior is. Lower is ghostlier.
#
# A DARK SEPARATION WAS TRIED HERE AND REMOVED. The idea was a faint dark stroke outside the
# rim to hold the shape against lit ground, the way a keyline does for a solid weapon.
# Measured, it did nothing: drawn after the bloom and under the rim, only about a pixel of it
# survived, and the interior and just-outside alphas came back 170/140 against 170/143 for the
# version without it. Two ways to read that - it needed to be far heavier, or a ghost should
# not have a dark outline at all. The second is right, and the cool bloom already separates
# the weapon from warm ground by HUE, which is the spectral way to stay legible.
function BuildSword([bool]$colder, [int]$bodyAlpha, $tipSpec) {
  $bmp = New-Object System.Drawing.Bitmap $CANVAS, $CANVAS, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $gfx.Clear((RGB 0 0 0 0))
  $gfx.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

  $spanX = $TIP_X - $BUTT_X
  $spanY = $TIP_Y - $BUTT_Y
  $spanLen = [Math]::Sqrt(($spanX*$spanX) + ($spanY*$spanY))
  $uxx = $spanX/$spanLen; $uyy = $spanY/$spanLen
  $nxx = -$uyy;           $nyy = $uxx

  # the colder palette: a saturated spirit-blue rather than near-neutral white
  $rimCol   = if ($colder) { @(226, 246, 255) } else { $C_RIM }
  $bodyHi   = if ($colder) { @(140, 210, 255) } else { $C_BODY }
  $bodyLo   = if ($colder) { @( 64, 132, 206) } else { $C_BODY_D }
  $bloomCol = if ($colder) { @( 96, 178, 255) } else { $C_BLOOM }

  function WPT([double]$along, [double]$perp) {
    $fx = $BUTT_X + ($uxx * $along * $spanLen) + ($nxx * $perp * $spanLen)
    $fy = $BUTT_Y + ($uyy * $along * $spanLen) + ($nyy * $perp * $spanLen)
    return (New-Object System.Drawing.PointF ([single]($fx*$CANVAS)), ([single]($fy*$CANVAS)))
  }

  # One closed outline: up the EDGE side, round the asymmetric tip, back down the SPINE side.
  # The tip is inserted between the two passes rather than mirrored, which is the whole
  # reason it can be a kissaki instead of a symmetric point.
  $outline = @()
  foreach ($node in $PROFILE) { $outline += (WPT $node[0] $node[1]) }
  foreach ($node in $tipSpec.Edge)  { $outline += (WPT $node[0] $node[1]) }
  $outline += (WPT $tipSpec.Point[0] $tipSpec.Point[1])
  foreach ($node in $tipSpec.Spine) { $outline += (WPT $node[0] $node[1]) }
  for ($idx = $PROFILE.Count - 1; $idx -ge 0; $idx--) {
    $outline += (WPT $PROFILE[$idx][0] (-1.0 * $PROFILE[$idx][1]))
  }
  $body = New-Object System.Drawing.Drawing2D.GraphicsPath
  $body.AddPolygon([System.Drawing.PointF[]]$outline)

  # the horns, as separate paths so their sweep can be expressed
  $hornPaths = @()
  foreach ($spec in $HORNS) {
    foreach ($side in @((1.0), (-1.0))) {
      $atAlong = $spec[0]; $fromPerp = $spec[1]; $len = $spec[2]; $sweep = $spec[3]
      $hp = New-Object System.Drawing.Drawing2D.GraphicsPath
      $hp.AddPolygon([System.Drawing.PointF[]]@(
        (WPT ($atAlong - 0.012) ($side * $fromPerp * 0.80)),
        (WPT ($atAlong + ($len * 0.30)) ($side * ($fromPerp + ($len * 0.55)))),
        (WPT ($atAlong + ($len * $sweep * 0.42)) ($side * ($fromPerp + ($len * 0.30)))),
        (WPT ($atAlong + 0.014) ($side * $fromPerp * 0.72))
      ))
      $hornPaths += $hp
    }
  }

  $allPaths = @($body) + $hornPaths

  # ---- 1. OUTER BLOOM. Widest and faintest first. This is the weapon bleeding into the
  # air, and it is most of what sells "not solid". ----
  foreach ($spec in @(@((30.0), (16)), @((20.0), (24)), @((11.0), (34)))) {
    foreach ($pathRef in $allPaths) {
      $penGlow = New-Object System.Drawing.Pen (RGB $bloomCol[0] $bloomCol[1] $bloomCol[2] $spec[1]), ([single]($spec[0]*$SS))
      $penGlow.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
      $penGlow.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
      $penGlow.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
      $gfx.DrawPath($penGlow, $pathRef); $penGlow.Dispose()
    }
  }

  # ---- 2. optional faint DARK separation, variant B only. Sits outside the rim, so it
  # separates the weapon from lit ground without implying a lit surface. ----

  # ---- 3. TRANSLUCENT BODY, banded along the length so it carries the gradient. Drawn as
  # clipped bands over the whole silhouette: the ground shows through. ----
  $oldClip = $gfx.Clip
  $region = New-Object System.Drawing.Region $body
  foreach ($hp in $hornPaths) { $region.Union($hp) }
  $gfx.Clip = $region
  $BANDS = 64
  for ($band = 0; $band -lt $BANDS; $band++) {
    $tA = $band / [double]$BANDS
    $tB = ($band + 1) / [double]$BANDS
    $col = Lerp3 $bodyLo $bodyHi (($tA + $tB) * 0.5)
    # his blue through the hilt, strongest at the pommel and gone by the second crossguard.
    # SMOOTHSTEPPED, so it does not stop dead at the guard - a linear ramp to zero leaves a
    # visible band edge exactly where two pieces of furniture already meet, and reads as a
    # drawing seam rather than as colour running out.
    $midAlong = ($tA + $tB) * 0.5
    if ($midAlong -lt $HILT_BLUE_END) {
      $fall = 1.0 - ($midAlong / $HILT_BLUE_END)
      $fall = $fall * $fall * (3.0 - (2.0 * $fall))
      $col = Lerp3 $col $C_HILT_BLUE ($HILT_BLUE_MIX * $fall)
    }
    $quad = @(
      (WPT $tA ( 0.30)), (WPT $tB ( 0.30)), (WPT $tB (-0.30)), (WPT $tA (-0.30))
    )
    $brush = New-Object System.Drawing.SolidBrush (RGB $col[0] $col[1] $col[2] $bodyAlpha)
    $gfx.FillPolygon($brush, [System.Drawing.PointF[]]$quad)
    $brush.Dispose()
  }

  # ---- 4. THE GLOWING ENGRAVING, inside the clip so it cannot escape the blade. On the
  # reference this knotwork is cut DARK into steel; on a spirit blade it is the light
  # showing through, so it is drawn bright. Sparse on purpose - it is gone by 48px and
  # dense detail there reads as noise. ----
  $penGlyph = New-Object System.Drawing.Pen (RGB 255 255 255 $GLYPH_ALPHA), ([single](2.2*$SS))
  $penGlyph.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  foreach ($at in @(0.430, 0.545, 0.660, 0.812)) {
    # a small angular meander: out, along, back in - the Nordic step motif
    $gfx.DrawLines($penGlyph, [System.Drawing.PointF[]]@(
      (WPT ($at - 0.026) ( 0.0110)),
      (WPT ($at - 0.026) ( 0.0235)),
      (WPT ($at + 0.020) ( 0.0235)),
      (WPT ($at + 0.020) (-0.0235)),
      (WPT ($at - 0.026) (-0.0235)),
      (WPT ($at - 0.026) (-0.0110))
    ))
  }
  # a centreline of light running the blade's length
  $penSpine = New-Object System.Drawing.Pen (RGB 255 255 255 96), ([single](2.6*$SS))
  $penSpine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penSpine.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $gfx.DrawLine($penSpine, (WPT 0.380 0.0), (WPT 0.862 0.0))
  $penSpine.Dispose()
  $penGlyph.Dispose()

  # =====================================================================================
  #  THE FURNITURE - pommel, grips and both guards. Added 2026-07-31.
  # =====================================================================================
  #  Everything above the blade root used to be bare: the body gradient and the rim, and
  #  nothing else. The blade had its meander and its centreline while the whole handle
  #  carried no information at all.
  #
  #  ALL OF IT IS INTERIOR. Every stroke below is inside the clip region, so the outline
  #  cannot move - the silhouette is signed off and this is detail drawn on it, not a
  #  reshaping of it. That distinction is the one the arms got wrong.
  #
  #  CURVES, NOT STRAIGHT LINES, and for a reason rather than for its own sake: every one
  #  of these features wraps a ROUND object. Cord spiralling a grip, a collar round a
  #  tang, a langet clasping a blade - seen flat, each is an arc, and drawn as a straight
  #  line each reads as a sticker laid on top. The bow is small (0.002-0.004 of length,
  #  about a pixel and a half) but it is the difference between "wrapped" and "striped".
  # -------------------------------------------------------------------------------------
  # One arc crossing the weapon: $skew leans it along the axis, $bow bellies it out.
  function WARC([double]$atAlong, [double]$halfPerp, [double]$skew, [double]$bow) {
    return @(
      (WPT ($atAlong - $skew) (-$halfPerp)),
      (WPT ($atAlong + $bow)  ( 0.0)),
      (WPT ($atAlong + $skew) ( $halfPerp))
    )
  }
  # A closed rounded shape in weapon space - collars, bosses, the pommel cap.
  function WBOSS([double]$atAlong, [double]$halfAlong, [double]$halfPerp) {
    return @(
      (WPT ($atAlong - $halfAlong) ( 0.0)),
      (WPT  $atAlong               (-$halfPerp)),
      (WPT ($atAlong + $halfAlong) ( 0.0)),
      (WPT  $atAlong               ( $halfPerp))
    )
  }

  $penFine = New-Object System.Drawing.Pen (RGB 255 255 255 128), ([single](1.5*$SS))
  $penFine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penFine.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $penPart = New-Object System.Drawing.Pen (RGB 255 255 255 172), ([single](2.1*$SS))
  $penPart.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $penPart.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penPart.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

  # ---- POMMEL: a cap arc across the butt, a raised boss, and two nicks --------------
  $capPath = New-Object System.Drawing.Drawing2D.GraphicsPath
  $capPath.AddCurve([System.Drawing.PointF[]](WARC 0.013 0.0205 0.0 -0.005), [single]0.4)
  $gfx.DrawPath($penPart, $capPath); $capPath.Dispose()
  $bossPath = New-Object System.Drawing.Drawing2D.GraphicsPath
  $bossPath.AddClosedCurve([System.Drawing.PointF[]](WBOSS 0.027 0.0125 0.0140), [single]0.45)
  $gfx.DrawPath($penPart, $bossPath); $bossPath.Dispose()
  foreach ($nickSide in @((1.0), (-1.0))) {
    $nick = New-Object System.Drawing.Drawing2D.GraphicsPath
    $nick.AddCurve([System.Drawing.PointF[]]@(
      (WPT 0.0405 ($nickSide * 0.0200)), (WPT 0.0435 ($nickSide * 0.0125)), (WPT 0.0405 ($nickSide * 0.0055))
    ), [single]0.4)
    $gfx.DrawPath($penFine, $nick); $nick.Dispose()
  }

  # ---- GRIP WRAP: cord spiralling a round grip, so every band is an arc and they all
  #      lean the SAME way. Alternating the lean would read as a lattice, which is a
  #      different binding and not this one. --------------------------------------------
  foreach ($gripRun in @( @((0.056), (0.144), (9)), @((0.216), (0.294), (8)) )) {
    $runFrom = [double]$gripRun[0]; $runTo = [double]$gripRun[1]; $bandCount = [int]$gripRun[2]
    for ($bandIdx = 0; $bandIdx -lt $bandCount; $bandIdx++) {
      $atBand = $runFrom + (($runTo - $runFrom) * (($bandIdx + 0.5) / $bandCount))
      $wrap = New-Object System.Drawing.Drawing2D.GraphicsPath
      $wrap.AddCurve([System.Drawing.PointF[]](WARC $atBand 0.0165 0.0042 0.0022), [single]0.4)
      $gfx.DrawPath($penFine, $wrap); $wrap.Dispose()
    }
  }

  # ---- THE TWO GUARDS: a collar round the tang, a moulded seam down each arm, and a
  #      boss near each arm's end. The collar is what makes a guard read as a separate
  #      forged piece rather than as a wide spot in the outline. ------------------------
  foreach ($guardSpec in @( @((0.178), (0.0620), (0.0165), (0.0215)), @((0.332), (0.0790), (0.0195), (0.0270)) )) {
    $guardAt = [double]$guardSpec[0]; $armEnd = [double]$guardSpec[1]
    $collarA = [double]$guardSpec[2]; $collarP = [double]$guardSpec[3]
    $collar = New-Object System.Drawing.Drawing2D.GraphicsPath
    $collar.AddClosedCurve([System.Drawing.PointF[]](WBOSS $guardAt $collarA $collarP), [single]0.45)
    $gfx.DrawPath($penPart, $collar); $collar.Dispose()
    foreach ($armSide in @((1.0), (-1.0))) {
      # the seam runs OUT along the arm and bellies toward the blade - a moulded ridge
      $seam = New-Object System.Drawing.Drawing2D.GraphicsPath
      $seam.AddCurve([System.Drawing.PointF[]]@(
        (WPT ($guardAt + 0.0010) ($armSide * ($collarP + 0.0045))),
        (WPT ($guardAt + 0.0075) ($armSide * (($collarP + $armEnd) * 0.52))),
        (WPT ($guardAt + 0.0035) ($armSide * ($armEnd - 0.0070)))
      ), [single]0.4)
      $gfx.DrawPath($penFine, $seam); $seam.Dispose()
      $armBoss = New-Object System.Drawing.Drawing2D.GraphicsPath
      $armBoss.AddClosedCurve([System.Drawing.PointF[]]@(
        (WPT ($guardAt - 0.0080) ($armSide * ($armEnd - 0.0175))),
        (WPT  $guardAt           ($armSide * ($armEnd - 0.0290))),
        (WPT ($guardAt + 0.0080) ($armSide * ($armEnd - 0.0175))),
        (WPT  $guardAt           ($armSide * ($armEnd - 0.0060)))
      ), [single]0.45)
      $gfx.DrawPath($penFine, $armBoss); $armBoss.Dispose()
    }
  }

  # ---- LANGETS: two tongues reaching up from the upper guard onto the blade root and
  #      clasping it. They curve inward because they wrap the blade's faces. -----------
  foreach ($langetSide in @((1.0), (-1.0))) {
    $langet = New-Object System.Drawing.Drawing2D.GraphicsPath
    $langet.AddCurve([System.Drawing.PointF[]]@(
      (WPT 0.3540 ($langetSide * 0.0215)),
      (WPT 0.3760 ($langetSide * 0.0185)),
      (WPT 0.3980 ($langetSide * 0.0105)),
      (WPT 0.4080 ($langetSide * 0.0035))
    ), [single]0.42)
    $gfx.DrawPath($penPart, $langet); $langet.Dispose()
  }

  $penFine.Dispose()
  $penPart.Dispose()
  $gfx.Clip = $oldClip
  $region.Dispose()

  # ---- 5. THE LUMINOUS RIM, last so nothing covers it. This is the edge of the apparition,
  # and it replaces both the keyline and the specular. ----
  foreach ($pathRef in $allPaths) {
    $penRim = New-Object System.Drawing.Pen (RGB $rimCol[0] $rimCol[1] $rimCol[2] $RIM_ALPHA), ([single](2.6*$SS))
    $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $gfx.DrawPath($penRim, $pathRef); $penRim.Dispose()
  }

  foreach ($hp in $hornPaths) { $hp.Dispose() }
  $body.Dispose()
  $gfx.Dispose()

  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfin = [System.Drawing.Graphics]::FromImage($final)
  $gfin.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gfin.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gfin.Clear((RGB 0 0 0 0))
  $gfin.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE))
  $gfin.Dispose(); $bmp.Dispose()
  return $final
}

function QuadOf($bitmap) {
  $data = $bitmap.LockBits((New-Object System.Drawing.Rectangle 0,0,$bitmap.Width,$bitmap.Height),
          [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object 'byte[]' ($data.Stride * $bitmap.Height)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $stride = $data.Stride
  $bitmap.UnlockBits($data)
  $topLeft=0;$topRight=0;$botLeft=0;$botRight=0
  $half = [int]($bitmap.Width/2)
  for ($yy=0; $yy -lt $bitmap.Height; $yy++) {
    for ($xx=0; $xx -lt $bitmap.Width; $xx++) {
      if ($bytes[$yy*$stride + $xx*4 + 3] -gt 8) {
        if ($yy -lt $half) { if ($xx -lt $half) {$topLeft++} else {$topRight++} }
        else { if ($xx -lt $half) {$botLeft++} else {$botRight++} }
      }
    }
  }
  return @($topLeft,$topRight,$botLeft,$botRight)
}

$built = @()
$built += ,@((BuildSword $false $BODY_ALPHA $TIP_KATANA), "A. katana tip - short kissaki")
$built += ,@((BuildSword $false $BODY_ALPHA $TIP_GLAIVE), "B. glaive tip - broader forward sweep")
$built += ,@((BuildSword $true  $BODY_ALPHA $TIP_KATANA), "C. katana tip, spirit-blue")

$CELLW = 300
$PADX = 24
$sheetW = ($PADX * 4) + ($CELLW * 3)
$sheetH = 500
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gsheet = [System.Drawing.Graphics]::FromImage($sheet)
$gsheet.Clear((RGB 28 30 28 255))
$gsheet.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontBig = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontMid = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Bold)
$fontSml = New-Object System.Drawing.Font "Segoe UI", 9.5
$brWhite = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brGrey  = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brGold  = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)
$gsheet.DrawString("CALL OF VALOR - built from scratch as a SPECTRE, not a lit object", $fontBig, $brWhite, [single]$PADX, [single]12)
$gsheet.DrawString("translucent body you can see the ground through, glowing edges and engraving, outer bloom. No bevel, no specular, no keyline.", $fontSml, $brGrey, [single]$PADX, [single]36)

function Ground($gfxRef, [double]$px, [double]$py, [double]$size, [int]$salt) {
  $tile = 12
  for ($gy = 0; $gy -lt $size; $gy += $tile) {
    for ($gx = 0; $gx -lt $size; $gx += $tile) {
      $hashv = [Math]::Sin((($gx+1)*12.9898) + (($gy+1)*78.233) + ($salt*37.719)) * 43758.5453
      $hashv = $hashv - [Math]::Floor($hashv)
      $delta = [int](($hashv - 0.5) * 42.0)
      $brush = New-Object System.Drawing.SolidBrush (RGB (122+$delta) (106+$delta) (84+[int]($delta*0.8)) 255)
      $gfxRef.FillRectangle($brush, [single]($px+$gx), [single]($py+$gy), [single]$tile, [single]$tile)
      $brush.Dispose()
    }
  }
}

$column = 0
$report = @()
foreach ($entry in $built) {
  $img = $entry[0]; $label = $entry[1]
  $px = $PADX + ($column * ($CELLW + $PADX))
  $py = 58
  Ground $gsheet $px $py $CELLW (11 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$px), ([int]$py), $CELLW, $CELLW))
  $brLabel = if ($column -eq 0) { $brGold } else { $brGrey }
  $gsheet.DrawString($label, $fontMid, $brLabel, [single]$px, [single]($py + $CELLW + 6))
  Ground $gsheet $px ($py + $CELLW + 30) 96 (40 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$px), ([int]($py + $CELLW + 30)), 96, 96))
  Ground $gsheet ($px + 104) ($py + $CELLW + 30) 48 (50 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]($px + 104)), ([int]($py + $CELLW + 30)), 48, 48))
  $gsheet.DrawString("96px (in hand)      48px", $fontSml, $brGrey, [single]$px, [single]($py + $CELLW + 130))
  $quad = QuadOf $img
  $report += ("{0,-42} TL {1,5} TR {2,5} BL {3,5} BR {4,5}   tip-top-right: {5}" -f `
    $label, $quad[0], $quad[1], $quad[2], $quad[3],
    $(if ($quad[1] -gt $quad[0] -and $quad[1] -gt $quad[3]) { "YES" } else { "*** NO ***" }))
  $column++
}
$gsheet.Dispose()

$previewPath = Join-Path $OUT_DIR "valor_greatsword.png"
$sheet.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

for ($idx = 0; $idx -lt $built.Count; $idx++) {
  $cand = Join-Path $OUT_DIR ("valor_gs_" + ([char](65+$idx)) + ".png")
  $built[$idx][0].Save($cand, [System.Drawing.Imaging.ImageFormat]::Png)
}
if ($WRITE_TEXTURE) {
  $dest = Join-Path $DEST_DIR "DovahkiinValorGreatsword.png"
  $built[0][0].Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("WROTE SHIPPING TEXTURE " + $dest)
} else {
  Write-Output "preview only - nothing written into the mod (`$WRITE_TEXTURE is false)"
}
foreach ($entry in $built) { $entry[0].Dispose() }
foreach ($line in $report) { Write-Output $line }
Write-Output ("wrote " + $previewPath)
Write-Output "DONE"


