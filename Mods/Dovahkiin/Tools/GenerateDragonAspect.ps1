# =====================================================================================
#  Dragon Aspect overlay - SPEC.md 4.4d
#
#  The visual grows with the words known, so the player SEES their progress:
#
#    L1  (Mul)            arm armour only - spectral vambraces, nothing else
#    L2  (Mul Qah)        + torso scale plates, THREE swept fins per shoulder,
#                           and the two TES5 chains of orange circles running from the
#                           clavicles down the abdomen
#    L3  (Mul Qah Diiv)   + horned helm, + orange-and-azure aura
#
#  Output textures (256x256, transparent, in the SAME frame as a RimWorld body sprite so
#  the overlay draws at the pawn's own draw position with no offset):
#
#    DragonAspect_L1_{Male,Female,Thin,Fat,Hulk}_{south,north,east}.png   arms only
#    DragonAspect_L2_{Male,Female,Thin,Fat,Hulk}_{south,north,east}.png   full body (L3 reuses)
#    DragonAspectHelm_{south,north,east}.png   L3 only, drawn at the pawn's HEAD offset
#    DragonAspectAura.png                      L3 only, greyscale - tinted in code
#
#  West is east mirrored, which Graphic_Multi does for free.
#
#  FRAME, measured off a 256x256 reference body sprite. Only measurements were taken;
#  every pixel here is this script's own geometry and no third-party art is shipped.
#    body      x 77..178, y 88..214, centre x 127.5, widest (half-width 51) at y 120-130
#    arms      the outer ~15px band of the silhouette, y 102..196
#    chest     y 100..140 (the two pectorals)
#    abdomen   y 140..196 down the centre line
#    clavicles y ~104, at x ~108 and x ~147
#    head      60x74 within a 192x192 head frame - so ~31% x 39% of the draw quad
#
#  PowerShell traps this script is written around (all previously paid for here):
#    - variable names are CASE-INSENSITIVE: $out and $OUT are the same variable
#    - Drawing2D enums must be spelled [System.Drawing.Drawing2D.X]::Y
#    - Select-Object -First N TERMINATES an upstream pipeline; never pipe this into it
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$DEST    = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\Things\Pawn\DragonAspect"
# DOVAH_DEST redirects the 36 textures somewhere else. THIS SCRIPT OVERWRITES SIGNED-OFF ART
# BY DEFAULT - that is what $DEST above points at - so any run that is not deliberately
# re-shipping Dragon Aspect must set this. It is what makes previewing an alternative palette
# (DOVAH_PALETTE=valor, further down) a safe thing to do rather than a destructive one.
if ($env:DOVAH_DEST) { $DEST = $env:DOVAH_DEST }
$PREVIEW = "C:\Users\User\AppData\Local\Temp\claude\C--Games-Rimworld-RimWorld-RimWorldFolder-DovahkiinClaudePluged\8fd789e0-037c-4f64-847d-50fcce95451a\scratchpad"
$SIZE    = 256
$SS      = 4
$N       = $SIZE * $SS

# ---------------------------------------------------------------------------------
#  AURA PARTICLE SHAPE - SETTLED, do not change without being asked.
#
#    "moon"  half moon; both tips curl the same way, as a drawn moon glyph does
#    "ess"   the same body with one tip hooked the OTHER way  <- the user's choice
#
#  This was once called $FLARE_STYLE with values "A"/"B". It is spelled out now because
#  "version A / version B" was reused for the ARMOUR GRADIENT below, and two unrelated
#  A/B switches in one file is how the wrong one gets flipped.
# ---------------------------------------------------------------------------------
$PARTICLE_SHAPE = "ess"

# ---------------------------------------------------------------------------------
#  ARMOUR GRADIENT DIRECTION - the live A/B question.
#
#    "A"  bronze at the shoulders cooling to blue at the waist  (the confirmed build)
#    "B"  the same ramp REVERSED - blue at the shoulders warming to bronze at the waist
#
#  Affects the torso plates, the arm bands and the torso rim light. The HELM is
#  deliberately excluded: it keeps its gold crown into blue lower edge either way.
# ---------------------------------------------------------------------------------
$VERSION = "B"

# ---------------------------------------------------------------------------------
#  PLATE OPACITY - 1.55, raised from 1.0 on 2026-07-29 AT THE USER'S REQUEST.
#
#  HISTORY, because this number has been argued twice and the earlier reasoning still
#  holds - it just no longer applies on its own:
#
#  When the first playtest reported the armour as "barely visible" this was raised to 1.85
#  alone. That WAS the wrong fix. Opacity is what makes the plates translucent, and
#  cranking it by itself made the armour darker and heavier than the design that had been
#  agreed, because the plates are darker than the pale body they cover - more alpha means
#  less body showing through and a dimmer result. It was reverted, and the visibility
#  problem was solved by DEEPENING THE COLOURS instead.
#
#  The user then asked for more opacity explicitly, with "make sure it doesn't darken".
#  That is achievable, but ONLY as a pair: $PLATE_ALPHA sets how opaque, and $PLATE_LIFT
#  puts the brightness back. 1.55 with a lift of 0.32 measures as +20% to +31% opacity at
#  within 0.6% of the previous brightness, over the body silhouette, front and side, on
#  Male and Female.
#
#  SO: do not raise this one on its own. Moving it means re-sweeping $PLATE_LIFT against
#  the brightness measurement, or the old mistake comes straight back.
# ---------------------------------------------------------------------------------
$PLATE_ALPHA = 1.55

if (-not (Test-Path $DEST)) { New-Item -ItemType Directory -Path $DEST -Force | Out-Null }

# ---------------------------------------------------------------------------------
# Palette
# ---------------------------------------------------------------------------------
# Every channel is CLAMPED here rather than at the call sites. FromArgb throws on anything
# outside 0..255, and alpha is multiplied in several places downstream - by the scale jitter,
# by rim and highlight factors, by $PLATE_ALPHA - so any one of them can overshoot. Clamping
# once at the single place colours are built makes that whole class of error impossible.
function RGB($r,$g,$b,$a=255) {
  if ($r -lt 0) { $r = 0 } elseif ($r -gt 255) { $r = 255 }
  if ($g -lt 0) { $g = 0 } elseif ($g -gt 255) { $g = 255 }
  if ($b -lt 0) { $b = 0 } elseif ($b -gt 255) { $b = 255 }
  if ($a -lt 0) { $a = 0 } elseif ($a -gt 255) { $a = 255 }
  return [System.Drawing.Color]::FromArgb([int]$a,[int]$r,[int]$g,[int]$b)
}
# DEEPER than the first pass, at the user's request after seeing it in game.
#
# The armour was reading as washed-out over lit terrain. The first attempt at fixing that
# raised the plates' OPACITY, which made them darker and heavier and lost the translucent
# design that had been agreed. Saturating the palette instead makes the colour hold against
# a bright background while the alpha - and so the whole look - stays exactly as signed off.
#
# Each stop is pushed towards saturation, not towards black: red stays high while green and
# blue drop. Deepening by darkening all three channels equally would just have produced the
# muddy result the opacity change did.
$C_DEEP  = @( 88, 46, 12)    # deep bronze - a scale's shadowed body
$C_MID   = @(168,104, 28)    # burnished bronze
$C_GOLD  = @(228,152, 44)    # lit gold
$C_HOT   = @(255,206,120)    # hot edge
$C_EMBER = @(240,118, 28)    # ember amber - rim light
$C_ORANGE= @(238,104, 20)    # TES5 crest
$C_OCORE = @(255,206,150)    # crest hot centre
$C_AZURE = @( 72,152,238)    # L3 aura, second colour
$C_WHITE = @(255,255,255)    # a no-op tint, for art that carries its own colour
$C_BLEND_MID = @(252,222,198)  # the hot midtone the crescents' ember-to-azure blend passes
                               # through, instead of the grey a direct RGB lerp would give

# The cool half of the armour, deepened alongside the warm half.
#
# NOTE, and it is a real trade: this was originally Unrelenting Force's EXACT blue
# (95,165,240), chosen because Dragon Aspect's own shout ICON uses that blue at its head - the
# overlay matched its own icon rather than inventing a third blue. Deepening to (58,124,216)
# breaks that exact match. It is still recognisably the same blue, a shade down, and the
# in-game readability was judged worth it. If the icon link matters more, put these back.
$C_BLUE_LIT  = @( 58,124,216)  # was Unrelenting Force's (95,165,240)
$C_BLUE_MID  = @( 36, 80,150)
$C_BLUE_DEEP = @( 14, 32, 66)
$C_BLUE_HOT  = @(132,186,246)

# =================================================================================
# PALETTE OVERRIDE - Call of Valor's champion. OFF unless DOVAH_PALETTE says otherwise.
# =================================================================================
# Call of Valor summons a ghostly bright-white hero, and his weapon should take its colours
# from him exactly as the Ancient Dragonborn's halberd takes the Dragon Aspect ramp. Rather
# than redraw a whole pawn overlay, this reuses ALL of the geometry below - which is
# measured per body type, fits real silhouettes, and is signed off - and swaps only the
# fourteen named colours.
#
# THE DEFAULT PATH IS UNCHANGED AND MUST STAY THAT WAY. Dragon Aspect's 36 textures are
# signed off; this block does nothing at all unless the environment variable is set, and
# that was verified by generating the default set into a scratch folder and hashing it
# against the shipped files.
#
# Placed HERE deliberately: after the base stops, but BEFORE the $C_DEEP_RAW capture and the
# brightness lift further down, so the lift machinery operates on these colours the same way
# it does on the bronze ones. Putting it after the lift would bypass it silently.
#
# The roles are kept, only the hues change - a ghost lit from within rather than a burnished
# bronze plate: shadow to near-white through pale steel-blue, with the rim light cyan-white.
if ($env:DOVAH_PALETTE -eq "valor") {
  $C_DEEP  = @( 34, 58, 84)     # deep cool shadow, where bronze had its darkest body
  $C_MID   = @(120,162,200)     # pale steel-blue
  $C_GOLD  = @(206,232,252)     # the lit face
  $C_HOT   = @(255,255,255)     # hot edge - pure light
  $C_EMBER = @(214,240,255)     # rim light. A ghost's rim is cold, not amber.
  $C_ORANGE= @(168,214,250)     # crest
  $C_OCORE = @(255,255,255)     # crest hot centre
  $C_AZURE = @(120,196,255)     # aura's second colour
  $C_BLEND_MID = @(232,246,255) # the midtone the crescent blend passes through
  $C_BLUE_LIT  = @(150,196,236)
  $C_BLUE_MID  = @( 92,140,186)
  $C_BLUE_DEEP = @( 28, 52, 82)
  $C_BLUE_HOT  = @(226,246,255)
  Write-Output "PALETTE: valor - the ghostly champion, not Dragon Aspect's bronze"
}

# =================================================================================
# SHOULDER STYLE - "fins" (Dragon Aspect) or "pauldron" (Call of Valor)
# =================================================================================
# The user's brief, 2026-07-31: make the champion *different enough* from the Ancient
# Dragonborn while still looking good, one detail at a time. First detail: the three
# swept fins per shoulder become an ARMOUR PAULDRON - "from the top of his shoulders to
# over his chest", with curves.
#
# THIS IS A THIRD A/B SWITCH IN A FILE THAT ALREADY WARNS ABOUT HAVING TWO, so it is
# named for what it selects rather than "A"/"B" - $PARTICLE_SHAPE and $VERSION are the
# other two, and the whole point of naming them apart is that a session reading the
# notebook rather than the code cannot flip the wrong one.
#
# It follows the palette by default because in practice the champion always wants both,
# and one environment variable for "make the champion" is harder to get half-right than
# two. DOVAH_SHOULDER overrides it in either direction, so the combinations are still
# reachable for comparison.
#
# DRAGON ASPECT'S DEFAULT PATH IS UNCHANGED: no palette override means fins, exactly as
# the 36 signed-off textures have them. Prove it with the hash check, never by reading.
$SHOULDER_STYLE = "fins"
if ($env:DOVAH_PALETTE -eq "valor") { $SHOULDER_STYLE = "pauldron" }
if ($env:DOVAH_SHOULDER) { $SHOULDER_STYLE = $env:DOVAH_SHOULDER }
if ($SHOULDER_STYLE -ne "fins") { Write-Output ("SHOULDERS: " + $SHOULDER_STYLE) }

# ---------------------------------------------------------------------------------
# OPACITY / BRIGHTNESS KNOBS, and why there are three rather than one.
#
# "More opaque" and "darker" are the same lever unless they are deliberately separated.
# The plate gradient runs down to C_DEEP (88,46,12) and C_BLUE_DEEP (14,32,66) - the
# second is near black - so simply multiplying alpha reveals more of those stops and the
# armour reads as a dark smear rather than as more solid armour.
#
#   $PLATE_ALPHA  straight multiplier on plate alpha - how OPAQUE
#
#   TWO WAYS TO BRIGHTEN, AND THEY ARE NOT INTERCHANGEABLE. This was learned the hard way:
#   both were tuned to the same MEAN luminance and the user still saw one as darker, because
#   they put the light in completely different places.
#
#   $PLATE_GAIN   multiplies every stop. A bright stop gains as much proportionally as a
#                 dark one, so MIDTONES and HIGHLIGHTS rise together. Can clip - but only
#                 mildly at the strengths used here (at 1.12, C_GOLD's red overshoots 255
#                 by 0.4 of 255, which is nothing; it only became a real problem at the
#                 ~1.47 a fully-opaque suit would need).
#   $PLATE_LIFT   raises each stop's VALUE towards 255 by a fraction, holding the channel
#                 ratios. Preserves hue and saturation exactly and cannot clip - but the
#                 gain it applies is much larger for DARK stops than bright ones
#                 (C_BLUE_DEEP peak 66 gains ~90% at 0.32; C_GOLD peak 228 gains ~4%). So
#                 it lifts SHADOWS and barely moves midtones.
#
#   Measured over the body, at matched mean luminance: gain 1.12 gives a median of 159.9,
#   lift 0.32 gives 156.3. Same mean, 2.3% darker midtone - and the midtone is most of what
#   the eye reads. SHIPPED USES THE GAIN. Reach for the lift only when the shadow end
#   specifically needs opening up, as a fully-opaque variant would.
#   $DEEP_LIFT    pulls the two DEEP stops towards their MID neighbours - stops the
#                 shadow end going black as alpha rises. 0 = as authored, 1 = no deep
#                 stop at all.
#   $LIT_FALLOFF  how much darker a scale gets towards the waist. At 0.75 the waist sits
#                 at a quarter brightness, which is invisible under a translucent plate
#                 and very visible under an opaque one.
#
# ALL FOUR DEFAULT TO THE SIGNED-OFF LOOK. A default here is not a free parameter - it is
# art the user has already approved, and changing one as a side effect of adding a knob
# silently rewrites their decision.
#
# All four can be overridden from the environment so the A/B harness can sweep them
# without editing this file.
# ---------------------------------------------------------------------------------
# SHIPPED VALUES, chosen 2026-07-29. This is the "B - recommended" option the user picked
# from a preview, reproduced exactly.
#
# The ask was "a bit more opacity, make sure it doesn't darken". Those fight each other:
# the plates are darker than the pale body under them, so more alpha means less body
# showing and a darker composite. $PLATE_ALPHA 1.55 sets the opacity, $PLATE_GAIN 1.12 puts
# the brightness back. Measured over the body silhouette: opacity +20% to +31%, mean
# luminance within 0.6% and MEDIAN within 0.2% of the art signed off before this change.
#
# A $PLATE_LIFT 0.32 version was shipped first and the user reported it as darker. They were
# right: it matched on mean and was 2.3% down on median. Match the MEDIAN, not the mean.
$PLATE_GAIN  = 1.12
$PLATE_LIFT  = 0.00
$DEEP_LIFT   = 0.00
$LIT_FALLOFF = 0.75

# $OVERLAY_OPACITY scales the alpha of the FINISHED texture - plates, arm bands, fins,
# elbow spikes, rim light and chest crest together - so the whole overlay knocks back as
# one object. It is a separate knob from $PLATE_ALPHA on purpose:
#
#   $PLATE_ALPHA   multiplies the AUTHORED per-pixel alpha, which runs 26 at the centre
#                  line to 88 at the edges. Turning it DOWN from a saturated value does
#                  not fade the suit evenly - it brings that centre-to-edge ramp back,
#                  so the middle of the torso goes see-through while the edges stay solid.
#   $OVERLAY_OPACITY  a flat multiplier on the final alpha. THIS is "make the whole thing
#                  20% less opaque", and it leaves the fins and crest in step with the
#                  plates instead of leaving them standing at full strength.
$OVERLAY_OPACITY = 1.00

# $SPUR_SEP, 0..1: how hard the shoulder fins and elbow spikes are separated from the
# plate field behind them. See DrawSpur - it drives a dark rim, the hot edge width and a
# small brightness lift together.
#
# SHIPPED AT 0 - the fins are as originally authored. This was built and compared at 0.35,
# 0.60, 0.85 and 1.00 against the fins untouched, at the shipped opacity, and the user
# chose UNTOUCHED. Do not turn it on again without being asked.
#
# Keeping the machinery because the finding behind it stands and cost a round to reach: the
# opacity bump made the fins HARDER to read, since the plates gained ~20% opacity while the
# fins were unchanged, so fin-against-plate contrast necessarily fell. If that is ever
# revisited, 0.85 was the value that read best.
$SPUR_SEP = 0.00

if ($env:DOVAH_SPUR_SEP)        { $SPUR_SEP        = [double]$env:DOVAH_SPUR_SEP }
if ($env:DOVAH_PLATE_ALPHA)     { $PLATE_ALPHA     = [double]$env:DOVAH_PLATE_ALPHA }
if ($env:DOVAH_PLATE_GAIN)      { $PLATE_GAIN      = [double]$env:DOVAH_PLATE_GAIN }
if ($env:DOVAH_PLATE_LIFT)      { $PLATE_LIFT      = [double]$env:DOVAH_PLATE_LIFT }
if ($env:DOVAH_DEEP_LIFT)       { $DEEP_LIFT       = [double]$env:DOVAH_DEEP_LIFT }
if ($env:DOVAH_LIT_FALLOFF)     { $LIT_FALLOFF     = [double]$env:DOVAH_LIT_FALLOFF }
if ($env:DOVAH_OVERLAY_OPACITY) { $OVERLAY_OPACITY = [double]$env:DOVAH_OVERLAY_OPACITY }

# Raise a colour's VALUE towards full brightness by fraction $t, holding the channel
# ratios. Because the largest channel lands exactly on the new value, hue and saturation
# come through unchanged and NO channel can clip - which is the whole reason this is not
# a multiply. t=0 leaves the colour alone; t=1 makes its brightest channel 255.
function BrightLift($col, [double]$t) {
  if ($t -le 0.0) { return $col }
  $peak = [Math]::Max($col[0], [Math]::Max($col[1], $col[2]))
  if ($peak -le 0) { return $col }
  $peakNew = $peak + (255.0 - $peak) * $t
  $k = $peakNew / [double]$peak
  return @(
    ([int][Math]::Min(255.0, [Math]::Round($col[0] * $k))),
    ([int][Math]::Min(255.0, [Math]::Round($col[1] * $k))),
    ([int][Math]::Min(255.0, [Math]::Round($col[2] * $k)))
  )
}

# Captured BEFORE any lifting. The fin separation rim is derived from these, so it stays
# genuinely dark however far $DEEP_LIFT and $PLATE_LIFT have brightened everything else.
# Deriving it from the LIFTED deeps was the first attempt and produced a mid-tone rim that
# separated nothing - the rim has to contrast with the plates, so it cannot be brightened
# by the same knob that brightens the plates.
$C_DEEP_RAW      = $C_DEEP
$C_BLUE_DEEP_RAW = $C_BLUE_DEEP

# Applied once, here, rather than at each use site - the deep stops are read from several
# places and lifting only the obvious one is how the fins ended up gold on a blue chest.
if ($DEEP_LIFT -gt 0.0) {
  $C_DEEP = @(
    ([int]($C_DEEP[0] + ($C_MID[0] - $C_DEEP[0]) * $DEEP_LIFT)),
    ([int]($C_DEEP[1] + ($C_MID[1] - $C_DEEP[1]) * $DEEP_LIFT)),
    ([int]($C_DEEP[2] + ($C_MID[2] - $C_DEEP[2]) * $DEEP_LIFT))
  )
  $C_BLUE_DEEP = @(
    ([int]($C_BLUE_DEEP[0] + ($C_BLUE_MID[0] - $C_BLUE_DEEP[0]) * $DEEP_LIFT)),
    ([int]($C_BLUE_DEEP[1] + ($C_BLUE_MID[1] - $C_BLUE_DEEP[1]) * $DEEP_LIFT)),
    ([int]($C_BLUE_DEEP[2] + ($C_BLUE_MID[2] - $C_BLUE_DEEP[2]) * $DEEP_LIFT))
  )
}

# The lift is applied to the PLATE stops only - not to C_EMBER, C_AZURE or the crest
# colours, which belong to the aura and the crest and were tuned against their own
# backgrounds. Every stop moves together: brightening only some of them stops the scale
# reading as a lit surface and makes it read as the wrong colour instead.
$PLATE_STOPS = @("C_DEEP","C_MID","C_GOLD","C_HOT","C_BLUE_DEEP","C_BLUE_MID","C_BLUE_LIT","C_BLUE_HOT")

# GAIN first, then LIFT. Both default to a no-op, and shipping uses the gain alone.
if ($PLATE_GAIN -ne 1.0) {
  foreach ($stopName in $PLATE_STOPS) {
    $stopVal = (Get-Variable -Name $stopName -ValueOnly)
    Set-Variable -Name $stopName -Value @(
      ([int][Math]::Min(255.0, [Math]::Round($stopVal[0] * $PLATE_GAIN))),
      ([int][Math]::Min(255.0, [Math]::Round($stopVal[1] * $PLATE_GAIN))),
      ([int][Math]::Min(255.0, [Math]::Round($stopVal[2] * $PLATE_GAIN)))
    )
  }
}
if ($PLATE_LIFT -gt 0.0) {
  foreach ($stopName in $PLATE_STOPS) {
    Set-Variable -Name $stopName -Value (BrightLift (Get-Variable -Name $stopName -ValueOnly) $PLATE_LIFT)
  }
}
# How far towards blue the bottom of the armour goes. 1.0 would drop the bronze entirely.
$COOL_MAX = 0.92

function Lerp3($a, $b, [double]$t) {
  if ($t -lt 0) { $t = 0 }; if ($t -gt 1) { $t = 1 }
  return @([int]($a[0]+($b[0]-$a[0])*$t), [int]($a[1]+($b[1]-$a[1])*$t), [int]($a[2]+($b[2]-$a[2])*$t))
}

# How far towards blue the armour is at a given height. $frac is 0 at the top of the body,
# 1 at the bottom; $VERSION decides which end is the cool one. The torso plates, the arm
# bands and the torso rim all go through here so they cannot disagree - they are three
# separate pieces of code, and reversing only some of them is the obvious way to get a
# gold-rimmed blue chest. The HELM deliberately does NOT call this; it keeps its own fixed
# gold-crown-into-blue direction whichever version is selected.
function CoolAt([double]$frac) {
  $f = if ($VERSION -eq "B") { 1.0 - $frac } else { $frac }
  if ($f -lt 0.0) { $f = 0.0 }
  if ($f -gt 1.0) { $f = 1.0 }
  return $f * $f * (3.0 - 2.0 * $f) * $COOL_MAX
}

# ---------------------------------------------------------------------------------
# Body profile - MEASURED off each body type's OWN sprite.
#
# This was one hand-tuned table traced from Naked_Male_south.png and nothing else, so
# every other body type wore male-shaped plates. They are different SHAPES, not
# different sizes, and no scale factor reconciles them:
#
#   Male    shoulders 102px, waist 84, hip 88   - widest at the TOP, tapers down
#   Female  shoulders  74px, waist 60, hip 92   - hourglass, widest at the HIPS
#   Thin    shoulders  52px, waist 52, hip 52   - a straight tube
#   Fat     shoulders 138px, waist 138, hip 162 - widest low
#   Hulk    shoulders 150px, waist 120, hip 130 - and 58px taller than Male
#
# The body QUAD is 1.5 x 1.5 for EVERY adult body type (MeshPool.humanlikeBodySet, which
# is what PawnRenderer.DrawPawnBody uses). All five sprites therefore live in the same
# 256 frame, and art traced from one is authored 1:1 against it with no rescaling.
# Do NOT use the per-body-type sets in MeshPool - those are inset, for wounds and
# firefoam, and drawing armour on them puts it INSIDE the pawn.
#
# Every landmark below is a FRACTION of the measured body, and every fraction is the
# value the old hand-tuned male numbers already implied - so Male comes out as before
# and the other four now fit themselves.
# ---------------------------------------------------------------------------------
$REF_DIR    = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$BODY_TYPES = @("Male", "Female", "Thin", "Fat", "Hulk")
$ALPHA_MIN  = 40

# fractions of body HEIGHT (y measured down from the top of the silhouette)
$F_SHOULDER   = 0.2222   # was y=116 on a body spanning 88..214
$F_ARM_TOP    = 0.1111   # was y=102
$F_ARM_BOT    = 0.8571   # was y=196
$F_ELBOW_A    = 0.4206   # was y=141
$F_ELBOW_B    = 0.5317   # was y=155
$F_CREST_TOP  = 0.1587   # was y=108
$F_CREST_BOT  = 0.7937   # was y=188

# Fractions of the half-width AT THE SHOULDER LINE - deliberately NOT of the body's
# maximum half-width. Fins, arm bands and the chest crest are all upper-body features, and
# on a Fat body the maximum is the BELLY: scaling fins by it gave a pawn whose shoulder
# fins were 1.59x the male's when its shoulders were only 1.35x wider, and they read as
# wings. Male's shoulder half-width is ~50.5 against a maximum of 51, so these numbers are
# the old hardcoded ones unchanged and Male's art is untouched.
$F_ARM_W      = 0.297    # was 15.0 against a shoulder half-width of ~50.5
$F_SPUR_LEN   = 0.614    # was 31.0
$F_SPUR_THICK = 0.162    # was 8.2
$F_SHARD_TOP  = 0.416    # was 21.0
$F_SHARD_BOT  = 0.135    # was 6.8

# crest x, as a fraction of the half-width AT THAT HEIGHT
$F_CREST_X_TOP    = 0.436
$F_CREST_X_BOT    = 0.248
$F_CREST_X_TOP_E  = 0.474   # side-on, the crest sits forward on the trunk
$F_CREST_X_BOT_E  = 0.378

# ---------------------------------------------------------------------------------
# Read one body sprite's alpha silhouette. Returns per-row min/max x plus the extent.
# ---------------------------------------------------------------------------------
function MeasureSilhouette([string]$path) {
  $bmp = New-Object System.Drawing.Bitmap $path
  $bw = $bmp.Width; $bh = $bmp.Height
  $rect = New-Object System.Drawing.Rectangle 0, 0, $bw, $bh
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $stride = $data.Stride
  $buf = New-Object byte[] ($stride * $bh)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
  $bmp.UnlockBits($data)
  $bmp.Dispose()

  $rmin = New-Object int[] $bh
  $rmax = New-Object int[] $bh
  for ($yy = 0; $yy -lt $bh; $yy++) { $rmin[$yy] = -1; $rmax[$yy] = -1 }
  for ($yy = 0; $yy -lt $bh; $yy++) {
    $rowBase = $yy * $stride
    for ($xx = 0; $xx -lt $bw; $xx++) {
      if ($buf[$rowBase + $xx*4 + 3] -ge $ALPHA_MIN) {
        if ($rmin[$yy] -lt 0) { $rmin[$yy] = $xx }
        $rmax[$yy] = $xx
      }
    }
  }
  $topY = -1; $botY = -1
  for ($yy = 0; $yy -lt $bh; $yy++) { if ($rmin[$yy] -ge 0) { if ($topY -lt 0) { $topY = $yy }; $botY = $yy } }

  # centre and widest row
  $wideW = -1; $wideY = -1
  for ($yy = $topY; $yy -le $botY; $yy++) {
    if ($rmin[$yy] -lt 0) { continue }
    $ww = $rmax[$yy] - $rmin[$yy] + 1
    if ($ww -gt $wideW) { $wideW = $ww; $wideY = $yy }
  }
  $centreX = ($rmax[$wideY] + $rmin[$wideY]) / 2.0
  return @{ Min = $rmin; Max = $rmax; Top = $topY; Bot = $botY; CX = $centreX; MaxHalf = ($wideW / 2.0) }
}

# ---------------------------------------------------------------------------------
# Turn a measured silhouette into a @(y, halfLeft, halfRight) table.
#
# BOTH EDGES ARE KEPT SEPARATELY, and that is the whole point. The first version stored
# one half-width per row - max(left, right) - and mirrored it about the centre line. That
# is harmless on the front and back views, which really are near-symmetric, and badly
# wrong on the SIDE view, where the pawn faces one way. Measured on the east sprites:
#
#   Hulk   at y=230   left 69.5   right 11.5   -> 58px of armour hanging off the front
#   Thin   at y=200   left 38.5   right  0.5   -> 38px
#   Female at y=181   left 54.5   right 24.5   -> 30px
#   Male   at y=200   left 35.5   right 19.5   -> 16px
#
# The user reported it as a sagging veil in front of the abs, which is exactly what
# mirroring the larger edge produces down the lower body.
#
# Sampled every 6 rows and 3-tap smoothed: raw per-row values carry the sprite's
# antialiasing as jitter, which the plate scatter then amplifies into a ragged edge.
# ---------------------------------------------------------------------------------
function ProfileFromSilhouette($m) {
  $rmin = $m.Min; $rmax = $m.Max
  $topY = $m.Top; $botY = $m.Bot; $centreX = $m.CX

  $rawL = @{}
  $rawR = @{}
  for ($yy = $topY; $yy -le $botY; $yy++) {
    if ($rmin[$yy] -lt 0) { $rawL[$yy] = 0.0; $rawR[$yy] = 0.0; continue }
    # Clamped at zero: a row can lie entirely on one side of the centre line - the neck on
    # a side view does - and a negative half-width would fold the polygon inside out.
    $rawL[$yy] = [Math]::Max([double]0.0, [double]($centreX - $rmin[$yy]))
    $rawR[$yy] = [Math]::Max([double]0.0, [double]($rmax[$yy] - $centreX))
  }

  $stops = New-Object System.Collections.ArrayList
  $yy = $topY
  while ($yy -le $botY) {
    $accL = 0.0; $accR = 0.0; $cnt = 0
    for ($k = -1; $k -le 1; $k++) {
      $yk = $yy + $k
      if ($yk -ge $topY -and $yk -le $botY) { $accL += $rawL[$yk]; $accR += $rawR[$yk]; $cnt++ }
    }
    [void]$stops.Add(@( ([double]$yy), ([double]($accL/$cnt)), ([double]($accR/$cnt)) ))
    if ($yy -eq $botY) { break }
    $yy += 6
    if ($yy -gt $botY) { $yy = $botY }
  }
  return $stops.ToArray()
}

# ---------------------------------------------------------------------------------
# Set every geometry variable for one body type. BuildBody and its helpers read these
# from script scope, so this is called once per body type before generating.
# ---------------------------------------------------------------------------------
$GEOM = @{}
$PROFILE_CUR = $null
$CX = 127.5
$Y_TOP = 88.0
$Y_BOT = 214.0
$ARM_Y_TOP = 102.0
$ARM_Y_BOT = 196.0
$ARM_W = 15.0
$SHOULDER_Y = 116.0
$ELBOW_YS = @(141.0, 155.0)
$SPUR_LEN = 31.0
$SPUR_THICK = 8.2
$SHARD_TOP = 21.0
$SHARD_BOT = 6.8
$CREST_TOP_Y = 108.0
$CREST_BOT_Y = 188.0

function SetBodyGeometry([string]$bodyType) {
  $meas = @{}
  foreach ($rot in @("south","north","east")) {
    $meas[$rot] = MeasureSilhouette (Join-Path $REF_DIR "Naked_${bodyType}_${rot}.png")
  }

  # SIZE quantities are taken from the FRONT view and shared by all three rotations: a
  # shoulder fin does not shrink when the pawn turns to face sideways. Only the OUTLINE,
  # the centre line and the vertical extent are per-rotation.
  $profS = ProfileFromSilhouette $meas["south"]
  $sTop  = [double]$meas["south"].Top
  $sBot  = [double]$meas["south"].Bot
  $hwShoulder = HalfWidthAt $profS ($sTop + ($sBot - $sTop) * $F_SHOULDER)

  $script:GEOM = @{}
  foreach ($rot in @("south","north","east")) {
    $m = $meas[$rot]
    # Each rotation gets its OWN centre and extent. The side sprites are neither centred
    # on 127.5 nor the same height as the front - Female east is centred on x=113 and runs
    # y 82..224 against the front's 86..224 - so sharing the front's numbers put the whole
    # side outline in the wrong place before the asymmetry was even considered.
    $top = [double]$m.Top
    $bot = [double]$m.Bot
    $bodyH = $bot - $top
    $script:GEOM[$rot] = @{
      Prof      = ProfileFromSilhouette $m
      CX        = $m.CX
      YTop      = $top
      YBot      = $bot
      ArmYTop   = $top + $bodyH * $F_ARM_TOP
      ArmYBot   = $top + $bodyH * $F_ARM_BOT
      ShoulderY = $top + $bodyH * $F_SHOULDER
      ElbowYs   = @( ($top + $bodyH * $F_ELBOW_A), ($top + $bodyH * $F_ELBOW_B) )
      CrestTopY = $top + $bodyH * $F_CREST_TOP
      CrestBotY = $top + $bodyH * $F_CREST_BOT
      ArmW      = $hwShoulder * $F_ARM_W
      SpurLen   = $hwShoulder * $F_SPUR_LEN
      SpurThick = $hwShoulder * $F_SPUR_THICK
      ShardTop  = $hwShoulder * $F_SHARD_TOP
      ShardBot  = $hwShoulder * $F_SHARD_BOT
    }
  }

  $e = $script:GEOM["east"]
  Write-Output ("  {0,-7} front y {1}..{2} cx {3}  |  east y {4}..{5} cx {6}  |  shoulder half {7}  arm band {8}  fin {9}" -f `
    $bodyType, $sTop, $sBot, [Math]::Round($meas["south"].CX,1), `
    $e.YTop, $e.YBot, [Math]::Round($e.CX,1), `
    [Math]::Round($hwShoulder,1), [Math]::Round($e.ArmW,1), [Math]::Round($e.SpurLen,1))
}

# Point the script-scope geometry variables at ONE rotation. BuildBody and every helper it
# calls read these from script scope, so this is the single place a rotation is selected.
function UseRotation([string]$rot) {
  $g = $script:GEOM[$rot]
  $script:PROFILE_CUR = $g.Prof
  $script:CX          = $g.CX
  $script:Y_TOP       = $g.YTop
  $script:Y_BOT       = $g.YBot
  $script:ARM_Y_TOP   = $g.ArmYTop
  $script:ARM_Y_BOT   = $g.ArmYBot
  $script:SHOULDER_Y  = $g.ShoulderY
  $script:ELBOW_YS    = $g.ElbowYs
  $script:CREST_TOP_Y = $g.CrestTopY
  $script:CREST_BOT_Y = $g.CrestBotY
  $script:ARM_W       = $g.ArmW
  $script:SPUR_LEN    = $g.SpurLen
  $script:SPUR_THICK  = $g.SpurThick
  $script:SHARD_TOP   = $g.ShardTop
  $script:SHARD_BOT   = $g.ShardBot
}

# Interpolate ONE column of the profile: 1 is the left half-width, 2 the right. They are
# different numbers, and on a side view they are very different - see ProfileFromSilhouette.
function ProfCol($prof, [double]$y, [int]$col) {
  if ($y -le $prof[0][0]) { return [double]$prof[0][$col] }
  $last = $prof.Count - 1
  if ($y -ge $prof[$last][0]) { return [double]$prof[$last][$col] }
  for ($i = 0; $i -lt $last; $i++) {
    $y0 = [double]$prof[$i][0]; $y1 = [double]$prof[$i+1][0]
    if ($y -ge $y0 -and $y -le $y1) {
      $t = ($y - $y0) / ($y1 - $y0)
      $t = $t * $t * (3.0 - 2.0 * $t)
      return [double]$prof[$i][$col] + ($prof[$i+1][$col] - $prof[$i][$col]) * $t
    }
  }
  return [double]$prof[$last][$col]
}

# The real silhouette edge on ONE side. $side is -1 for left, +1 for right. Anything that
# PLACES geometry against the body outline must use this, never HalfWidthAt.
function HalfSideAt($prof, [double]$y, [double]$side) {
  if ($side -lt 0) { return ProfCol $prof $y 1 }
  return ProfCol $prof $y 2
}

# A single "how wide is the body here" number, for SCALING decisions only - never for
# placing an edge. On a side view this is the mean of two very unequal halves.
function HalfWidthAt($prof, [double]$y) {
  return ((ProfCol $prof $y 1) + (ProfCol $prof $y 2)) / 2.0
}

function BuildTorsoPath($prof) {
  $pts = New-Object System.Collections.ArrayList
  # Down the RIGHT edge, then back up the LEFT edge - each taken from its own column, so
  # the outline follows an asymmetric side view instead of mirroring the wider half.
  for ($y = $Y_TOP; $y -le $Y_BOT; $y += 1.0) {
    $hr = HalfSideAt $prof $y 1.0
    [void]$pts.Add((New-Object System.Drawing.PointF ([single](($CX+$hr)*$SS)), ([single]($y*$SS))))
  }
  for ($y = $Y_BOT; $y -ge $Y_TOP; $y -= 1.0) {
    $hl = HalfSideAt $prof $y -1.0
    [void]$pts.Add((New-Object System.Drawing.PointF ([single](($CX-$hl)*$SS)), ([single]($y*$SS))))
  }
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]$pts.ToArray([System.Drawing.PointF]))
  $p.CloseFigure()
  return $p
}

# The arm bands: outer ARM_W of the silhouette, between ARM_Y_TOP and ARM_Y_BOT, with
# rounded ends so they read as sleeves rather than as cut strips.
#
# $sides is ONE side for the side-on views and both for front and back. Looking at someone
# from the side you see one arm, not two - and in the reference sprite that arm is the
# column down the REAR edge (x~88..105 with the pawn facing right), so east uses -1 and
# west inherits the mirror of it for free.
function BuildArmsPath($prof, $sides) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  foreach ($side in $sides) {
    $outer = New-Object System.Collections.ArrayList
    $inner = New-Object System.Collections.ArrayList
    for ($y = $ARM_Y_TOP; $y -le $ARM_Y_BOT; $y += 1.0) {
      $hw = HalfSideAt $prof $y $side
      # taper the band's ends so the sleeve is capped, not chopped
      $tt = [Math]::Min(($y - $ARM_Y_TOP) / 10.0, ($ARM_Y_BOT - $y) / 10.0)
      if ($tt -gt 1.0) { $tt = 1.0 }
      if ($tt -lt 0.0) { $tt = 0.0 }
      $tt = $tt * $tt * (3.0 - 2.0 * $tt)
      $wBand = $ARM_W * $tt
      [void]$outer.Add((New-Object System.Drawing.PointF ([single](($CX+$side*$hw)*$SS)), ([single]($y*$SS))))
      [void]$inner.Insert(0, (New-Object System.Drawing.PointF ([single](($CX+$side*($hw-$wBand))*$SS)), ([single]($y*$SS))))
    }
    $all = New-Object System.Collections.ArrayList
    [void]$all.AddRange($outer); [void]$all.AddRange($inner)
    $p.AddPolygon([System.Drawing.PointF[]]$all.ToArray([System.Drawing.PointF]))
    $p.CloseFigure()
  }
  return $p
}

# ---------------------------------------------------------------------------------
# One scale: rounded shield, dark body, bright lower rim (edge-lit).
# ---------------------------------------------------------------------------------
function DrawScale($g, [double]$x, [double]$y, [double]$w, [double]$h, [double]$lit, [int]$alpha, [double]$cool = 0.0) {
  if ($alpha -le 1) { return }
  $hw = $w / 2.0
  # $cool slides this scale's whole palette from bronze towards blue. Every stop moves
  # together, so a cooled plate is still a lit plate with a shadow and a rim - shifting
  # only one of them would just make the scale look wrongly coloured rather than cold.
  $cDeep = Lerp3 $C_DEEP $C_BLUE_DEEP $cool
  $cMid  = Lerp3 $C_MID  $C_BLUE_MID  $cool
  $cGold = Lerp3 $C_GOLD $C_BLUE_LIT  $cool
  $cHot  = Lerp3 $C_HOT  $C_BLUE_HOT  $cool
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddClosedCurve([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF ([single]($x-$hw)),      ([single]($y-$h*0.42))),
    (New-Object System.Drawing.PointF ([single]($x-$hw*0.86)), ([single]($y+$h*0.18))),
    (New-Object System.Drawing.PointF ([single]($x-$hw*0.44)), ([single]($y+$h*0.50))),
    (New-Object System.Drawing.PointF ([single]$x),            ([single]($y+$h*0.58))),
    (New-Object System.Drawing.PointF ([single]($x+$hw*0.44)), ([single]($y+$h*0.50))),
    (New-Object System.Drawing.PointF ([single]($x+$hw*0.86)), ([single]($y+$h*0.18))),
    (New-Object System.Drawing.PointF ([single]($x+$hw)),      ([single]($y-$h*0.42)))
  ), [single]0.45)

  $top = Lerp3 $cDeep $cMid  $lit
  $bot = Lerp3 $cMid  $cGold $lit
  $rect = New-Object System.Drawing.RectangleF ([single]($x-$hw)), ([single]($y-$h*0.5)), ([single]$w), ([single]($h*1.12))
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $top[0] $top[1] $top[2] $alpha), (RGB $bot[0] $bot[1] $bot[2] $alpha), ([single]90.0)
  $g.FillPath($brush, $p); $brush.Dispose()

  $rimA = [int]([Math]::Min(255, $alpha * 1.55))
  $rimC = Lerp3 $cGold $cHot $lit
  $pen = New-Object System.Drawing.Pen (RGB $rimC[0] $rimC[1] $rimC[2] $rimA), ([single]($h*0.13))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $arc = New-Object System.Drawing.Drawing2D.GraphicsPath
  $arc.AddCurve([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF ([single]($x-$hw*0.90)), ([single]($y+$h*0.14))),
    (New-Object System.Drawing.PointF ([single]($x-$hw*0.44)), ([single]($y+$h*0.47))),
    (New-Object System.Drawing.PointF ([single]$x),            ([single]($y+$h*0.55))),
    (New-Object System.Drawing.PointF ([single]($x+$hw*0.44)), ([single]($y+$h*0.47))),
    (New-Object System.Drawing.PointF ([single]($x+$hw*0.90)), ([single]($y+$h*0.14)))
  ), [single]0.45)
  $g.DrawPath($pen, $arc)
  $pen.Dispose(); $arc.Dispose(); $p.Dispose()
}

# Fill a clip region with jittered scale rows.
function FillScales($g, $prof, [double]$aCentre, [double]$aEdge) {
  $scaleW = 15.5 * $SS
  $scaleH = 12.5 * $SS
  $rowStep = 9.0 * $SS
  $row = 0
  for ($y = ($Y_TOP+6.0)*$SS; $y -le ($Y_BOT-2.0)*$SS; $y += $rowStep) {
    $y256 = $y / $SS
    $hwL = (HalfSideAt $prof $y256 -1.0) * $SS
    $hwR = (HalfSideAt $prof $y256  1.0) * $SS
    $offset = if ($row % 2 -eq 0) { 0.0 } else { $scaleW * 0.5 }
    $lit = 1.0 - [Math]::Min(1.0, ($y256-$Y_TOP)/($Y_BOT-$Y_TOP)) * $LIT_FALLOFF
    # The bronze/blue ramp down the body, in whichever direction $VERSION selects.
    # Smoothstepped inside CoolAt, so one end stays convincingly its own colour and the
    # change happens across the middle rather than the whole torso being a half-and-half wash.
    $cool = CoolAt (($y256 - $Y_TOP) / ($Y_BOT - $Y_TOP))
    $col = 0
    for ($x = $CX*$SS - $hwL - $scaleW; $x -le $CX*$SS + $hwR + $scaleW; $x += $scaleW*0.86) {
      $px = $x + $offset
      # The centre-to-edge alpha ramp is normalised by THIS side's own half-width, or the
      # narrow side of an asymmetric body would never reach its edge alpha.
      $hwSide = if ($px -lt $CX*$SS) { $hwL } else { $hwR }
      $dx = [Math]::Abs($px - $CX*$SS) / [Math]::Max(1.0, $hwSide)
      $alpha = [int](($aCentre + ($aEdge-$aCentre) * [Math]::Pow([Math]::Min(1.0,$dx), 1.35)) * $PLATE_ALPHA)
      if ($alpha -gt 255) { $alpha = 255 }
      # Deterministic jitter: a perfectly regular grid of identical scales reads as a
      # waffle. A few percent of variation in size, position and brightness kills that.
      $h1 = (($row*73 + $col*151) % 17)/17.0 - 0.5
      $h2 = (($row*131 + $col*37) % 13)/13.0 - 0.5
      DrawScale $g ($px + $h1*$scaleW*0.16) ($y + $h2*$scaleH*0.14) ($scaleW*(1.0+$h1*0.13)) $scaleH ($lit*(1.0+$h1*0.22)) ([int]($alpha*(1.0+$h2*0.30))) $cool
      $col++
    }
    $row++
  }
}

# ---------------------------------------------------------------------------------
# Shoulder fin. Broad root, straight leading edge, CONCAVE trailing edge.
#
# Three shapes failed before this one and every failure was the OUTLINE, not the colour:
#   curved up and inward       -> read as a cow horn (any bend in a taper says "horn")
#   straight, tapered from root-> read as a needle
#   straight and broad         -> still just a spike
# The concave trailing edge is the whole trick: it separates a folded wing from a triangle.
# ---------------------------------------------------------------------------------
# $cool slides the spur from bronze towards blue, the same way DrawScale does, so fins and
# spikes track whichever direction the armour's ramp is running. It defaults to 0 (warm) so
# the HELM's horns are unaffected - they are meant to stay gold in both versions.
function DrawSpur($g, [double]$bx, [double]$by, [double]$len, [double]$thick, [double]$dir, [int]$alpha, [double]$fanDeg = 0.0, [double]$cool = 0.0) {
  $sDeep = Lerp3 $C_DEEP $C_BLUE_DEEP $cool
  $sGold = Lerp3 $C_GOLD $C_BLUE_LIT  $cool
  $sHot  = Lerp3 $C_HOT  $C_BLUE_HOT  $cool
  # fanDeg swings the whole fin about its root, away from vertical and towards horizontal.
  # It is multiplied by $dir so that "positive fans outward" holds on both sides.
  $a = $fanDeg * [Math]::PI / 180.0 * $dir
  $ca = [Math]::Cos($a); $sa = [Math]::Sin($a)
  # EVERY element is parenthesised, and it must stay that way. In PowerShell the comma
  # operator binds TIGHTER than arithmetic, so `@( $a*$b*1.15, $c*0.55 )` is parsed as
  # `$a * $b * @(1.15, $c) * 0.55` and dies with "Object[] has no op_Multiply". The array
  # then comes back EMPTY and the only visible symptom is a drawing call complaining about
  # an invalid parameter three functions away.
  $raw = @(
    @( ($dir*$thick*1.15),  ($thick*0.55)),
    @( ($dir*$len*0.52),   (-$len*0.38)),
    @( ($dir*$len*0.95),   (-$len*1.05)),
    # this control point sits INSIDE the straight tip-to-root line - the concave scoop
    @( ($dir*$len*0.20),   (-$len*0.52)),
    @((-$dir*$thick*0.25), (-$thick*1.05))
  )
  $pts = @()
  foreach ($q in $raw) {
    $dx = $q[0]; $dy = $q[1]
    $pts += (New-Object System.Drawing.PointF ([single]($bx + $dx*$ca - $dy*$sa)), ([single]($by + $dx*$sa + $dy*$ca)))
  }
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddClosedCurve([System.Drawing.PointF[]]$pts, [single]0.18)

  # SEPARATION. A fin takes the CoolAt ramp at its own height, and so does the plate field
  # it sits on - so at the shoulders a blue fin lies on blue plates and the two merge into
  # one mass. Raising $PLATE_LIFT does not help, because it brightens both equally.
  #
  # $SPUR_SEP drives all three separation levers together, because turning one alone just
  # trades one kind of mush for another:
  #   a DARK RIM under the fill, which is what actually makes two overlapping shapes read
  #     as separate objects rather than as one silhouette
  #   a THICKER hot edge, giving the fin its own defined outline
  #   a small BRIGHTNESS lift on the fin only, so it stands off the plate field it covers
  # The rim colour is derived by darkening, not taken from the palette, so it stays dark
  # however far $PLATE_LIFT has brightened everything else.
  if ($SPUR_SEP -gt 0.0) {
    $sGold = BrightLift $sGold (0.18 * $SPUR_SEP)
    $sHot  = BrightLift $sHot  (0.18 * $SPUR_SEP)
    # From the RAW deeps, not $sDeep - see the note where $C_DEEP_RAW is captured.
    $rimBase = Lerp3 $C_DEEP_RAW $C_BLUE_DEEP_RAW $cool
    $rimCol = @(
      ([int]($rimBase[0] * 0.72)),
      ([int]($rimBase[1] * 0.72)),
      ([int]($rimBase[2] * 0.72))
    )
    $penRim = New-Object System.Drawing.Pen (RGB $rimCol[0] $rimCol[1] $rimCol[2] ([int]([Math]::Min(255, $alpha*1.45)))), ([single]($thick * 0.46 * $SPUR_SEP))
    $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($penRim, $p); $penRim.Dispose()
  }

  $rect = $p.GetBounds()
  if ($rect.Width -lt 1) { $rect.Width = 1 }
  if ($rect.Height -lt 1) { $rect.Height = 1 }
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $sDeep[0] $sDeep[1] $sDeep[2] $alpha), (RGB $sGold[0] $sGold[1] $sGold[2] ([int]([Math]::Min(255,$alpha*1.25)))), ([single]300.0)
  $g.FillPath($brush, $p); $brush.Dispose()

  $edgeW = $thick * (0.16 + 0.26 * $SPUR_SEP)
  $pen = New-Object System.Drawing.Pen (RGB $sHot[0] $sHot[1] $sHot[2] ([int]([Math]::Min(230,$alpha*1.4)))), ([single]$edgeW)
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p)
  $pen.Dispose(); $p.Dispose()
}

# Three fins per shoulder, FANNED and descending in size so all three stay visible.
#
# Fanning is the whole point. The first attempt kept all three at the same angle and just
# made them shorter, so the smaller two sat entirely inside the largest one and only one
# fin was visible. Rotating each successive fin further towards horizontal is what
# separates them - the size difference alone does nothing.
function DrawShoulderFins($g, [double]$bx, [double]$by, [double]$len, [double]$thick, [double]$dir, [int]$alpha, [double]$cool = 0.0) {
  DrawSpur $g $bx                     ($by + $len*0.10)  ($len*0.50)  ($thick*0.62) $dir ([int]($alpha*0.80)) 52.0 $cool
  DrawSpur $g ($bx - $dir*$len*0.06)  ($by + $len*0.03)  ($len*0.74)  ($thick*0.80) $dir ([int]($alpha*0.90)) 27.0 $cool
  DrawSpur $g ($bx - $dir*$len*0.10)  $by                 $len        $thick        $dir $alpha              0.0  $cool
}

# =====================================================================================
#  CALL OF VALOR'S CUIRASS - a muscled breastplate that meets the pauldrons.
# =====================================================================================
#  The brief: a chest plate running up to the shoulder pauldrons, curved, precise, and
#  "it looks like it follows the chest muscles".
#
#  WHY THIS IS DRAWABLE WHEN THE BANDED CUIRASS WAS NOT. Earlier in this project a normal
#  plate cuirass was built and rejected as dull, and the stated reason was that its
#  detail - banding, fur strands, buckles - is 2-4px on a ~102px pawn and becomes noise.
#  That reason still holds and is not being walked back. A MUSCLED plate is a different
#  proposition: a pectoral is a LARGE form, roughly 26 x 30px here, and the thing that
#  makes it read is not fine detail but the SHADING of a broad curved mass. Big forms
#  survive the downscale to 48px; hatching does not. So the pecs get domes and creases,
#  and there is deliberately no attempt at striations or rivets.
#
#  HOW A MUSCLE IS DRAWN: not with outlines. A pectoral reads as a rounded mass because it
#  is lit across the top and shadowed underneath. Each pec is therefore a closed CURVE
#  filled with a PathGradientBrush - a radial gradient with its bright centre set at the
#  upper-middle of the dome - and then the single most important stroke on the whole
#  piece: the UNDER-PEC CREASE, a dark arc with a lit lower lip. Without the crease, two
#  bright ovals on a plate read as bosses. With it, they read as muscle.
#
#  EVERY horizontal measurement is a fraction of the body's OWN half-width at that row,
#  read from the measured profile - so the plate follows each of the five silhouettes'
#  taper instead of imposing Male's, exactly as the fins and arm bands do.
# ---------------------------------------------------------------------------------
#  The cuirass outline, as (fraction down the body, plate half-width as a fraction of the
#  body's half-width at that row). Narrow at the neck, flaring across the chest, drawing
#  back in at the waist. The chest values sit around 0.72-0.76 deliberately: the arm bands
#  occupy the outer $F_ARM_W of the silhouette, so a plate wider than this would be drawn
#  over the arms rather than over the trunk.
$PLATE_PROFILE = @(
  @(0.085, 0.300),   # neckline
  @(0.127, 0.600),   # clavicle line
  @(0.180, 0.710),
  @(0.285, 0.755),   # widest, across the nipple line
  @(0.410, 0.725),   # the under-pec crease
  @(0.470, 0.665),
  @(0.520, 0.580)    # lower edge, over the upper abdomen
)
$F_PLATE_TOP = 0.085
$F_PLATE_BOT = 0.520
$F_PEC_TOP   = 0.155
$F_PEC_WIDE  = 0.285
$F_PEC_BOT   = 0.410

# Smoothstepped lookup into $PLATE_PROFILE. Same interpolation ProfCol uses on the body
# profile, so the plate's edge and the body's edge curve in the same way.
function PlateFracAt([double]$hFrac) {
  $last = $PLATE_PROFILE.Count - 1
  if ($hFrac -le $PLATE_PROFILE[0][0])     { return [double]$PLATE_PROFILE[0][1] }
  if ($hFrac -ge $PLATE_PROFILE[$last][0]) { return [double]$PLATE_PROFILE[$last][1] }
  for ($idx = 0; $idx -lt $last; $idx++) {
    $h0 = [double]$PLATE_PROFILE[$idx][0]; $h1 = [double]$PLATE_PROFILE[$idx + 1][0]
    if ($hFrac -ge $h0 -and $hFrac -le $h1) {
      $tt = ($hFrac - $h0) / ($h1 - $h0)
      $tt = $tt * $tt * (3.0 - (2.0 * $tt))
      return ([double]$PLATE_PROFILE[$idx][1] + (([double]$PLATE_PROFILE[$idx + 1][1] - [double]$PLATE_PROFILE[$idx][1]) * $tt))
    }
  }
  return [double]$PLATE_PROFILE[$last][1]
}

# absolute y for a fraction of the body's height
function PlateY([double]$hFrac) { return ($Y_TOP + (($Y_BOT - $Y_TOP) * $hFrac)) }

# the plate's own edge at a given row, on one side, in 256-frame coords
function PlateEdge($prof, [double]$yy, [double]$side) {
  $hFrac = ($yy - $Y_TOP) / ($Y_BOT - $Y_TOP)
  return ($CX + ($side * (HalfSideAt $prof $yy $side) * (PlateFracAt $hFrac)))
}

function DrawChestPlate($g, $prof, [string]$rot, [int]$alpha, [double]$cool) {
  $bodyH = $Y_BOT - $Y_TOP
  $yTop  = PlateY $F_PLATE_TOP
  $yBot  = PlateY $F_PLATE_BOT
  $pDeep = Lerp3 $C_DEEP $C_BLUE_DEEP $cool
  $pMid  = Lerp3 $C_MID  $C_BLUE_MID  $cool
  $pLit  = Lerp3 $C_GOLD $C_BLUE_LIT  $cool
  $pHot  = Lerp3 $C_HOT  $C_BLUE_HOT  $cool
  # The plate is a broad lit surface, so unlike the pauldron lames it CAN carry the deep
  # stop - that is exactly the distinction: deep stops shade wide forms and hole out
  # narrow ones.
  $pShade = Lerp3 $pDeep $pMid 0.30

  # ---- the outline: down the right edge, across the bottom, up the left, then the
  #      neckline back across the top with a dip in the middle for the throat.
  $pts = New-Object System.Collections.ArrayList
  for ($yy = $yTop; $yy -le $yBot; $yy += 1.0) {
    [void]$pts.Add((New-Object System.Drawing.PointF ([single]((PlateEdge $prof $yy 1.0) * $SS)), ([single]($yy * $SS))))
  }
  # lower edge, dipping at the centre - a cuirass points down over the belly, it is not
  # cut off square
  $bottomDip = $bodyH * 0.030
  foreach ($across in @(0.62, 0.30, 0.0, -0.30, -0.62)) {
    $edgeX = $CX + ($across * ((PlateEdge $prof $yBot 1.0) - $CX))
    $sag = $bottomDip * (1.0 - ([Math]::Abs($across) * [Math]::Abs($across)))
    [void]$pts.Add((New-Object System.Drawing.PointF ([single]($edgeX * $SS)), ([single](($yBot + $sag) * $SS))))
  }
  for ($yy = $yBot; $yy -ge $yTop; $yy -= 1.0) {
    [void]$pts.Add((New-Object System.Drawing.PointF ([single]((PlateEdge $prof $yy -1.0) * $SS)), ([single]($yy * $SS))))
  }
  # the neckline, left to right, dipping to the throat
  $neckDip = $bodyH * 0.048
  foreach ($across in @(-0.62, -0.28, 0.0, 0.28, 0.62)) {
    $edgeX = $CX + ($across * ((PlateEdge $prof $yTop 1.0) - $CX))
    $dip = $neckDip * (1.0 - ($across * $across))
    [void]$pts.Add((New-Object System.Drawing.PointF ([single]($edgeX * $SS)), ([single](($yTop + $dip) * $SS))))
  }
  $plate = New-Object System.Drawing.Drawing2D.GraphicsPath
  $plate.AddPolygon([System.Drawing.PointF[]]$pts.ToArray([System.Drawing.PointF]))
  $plate.CloseFigure()

  # ---- the plate body: lit at the chest, falling away towards the waist
  $rect = $plate.GetBounds()
  if ($rect.Width -lt 1) { $rect.Width = 1 }
  if ($rect.Height -lt 1) { $rect.Height = 1 }
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $pLit[0] $pLit[1] $pLit[2] $alpha), (RGB $pShade[0] $pShade[1] $pShade[2] ([int]($alpha * 0.92))), ([single]90.0)
  $g.FillPath($brush, $plate); $brush.Dispose()

  # ---- the muscle. South is the chest; north is the back and gets a spine instead of a
  #      sternum; east sees one pec edge-on.
  $sides = if ($rot -eq "east") { @(-1.0) } else { @(-1.0, 1.0) }
  if ($rot -ne "north") {
    foreach ($side in $sides) { DrawPectoral $g $prof $side $alpha $pDeep $pMid $pLit $pHot }
  } else {
    foreach ($side in $sides) { DrawShoulderBlade $g $prof $side $alpha $pDeep $pMid $pLit }
  }

  # ---- the centre line. Sternum on the front, spine on the back. Drawn as a GROOVE - a
  #      dark channel with a lit lip on each side - because a single dark line reads as a
  #      crack in the plate rather than as a valley between two masses.
  if ($rot -ne "east") {
    $grooveTop = PlateY ($F_PLATE_TOP + 0.045)
    $grooveBot = if ($rot -eq "north") { PlateY ($F_PLATE_BOT - 0.02) } else { PlateY ($F_PEC_BOT + 0.028) }
    $grooveW = (HalfWidthAt $prof (PlateY $F_PEC_WIDE)) * 0.052
    $groove = New-Object System.Drawing.Drawing2D.GraphicsPath
    $groovePts = @(
      (New-Object System.Drawing.PointF ([single]($CX * $SS)), ([single]($grooveTop * $SS))),
      (New-Object System.Drawing.PointF ([single](($CX + ($grooveW * 0.18)) * $SS)), ([single](($grooveTop + (($grooveBot - $grooveTop) * 0.42)) * $SS))),
      (New-Object System.Drawing.PointF ([single]($CX * $SS)), ([single]($grooveBot * $SS)))
    )
    $groove.AddCurve([System.Drawing.PointF[]]$groovePts, [single]0.4)
    $penDark = New-Object System.Drawing.Pen (RGB $pDeep[0] $pDeep[1] $pDeep[2] ([int]($alpha * 0.85))), ([single]($grooveW * $SS))
    $penDark.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penDark.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawPath($penDark, $groove); $penDark.Dispose()
    # the lit lips, one either side, offset by half the groove's width
    foreach ($lip in @(-1.0, 1.0)) {
      $lipPath = New-Object System.Drawing.Drawing2D.GraphicsPath
      $lipPts = @()
      foreach ($srcPt in $groovePts) {
        $lipPts += (New-Object System.Drawing.PointF ([single]($srcPt.X + ($lip * $grooveW * 0.62 * $SS))), ([single]$srcPt.Y))
      }
      $lipPath.AddCurve([System.Drawing.PointF[]]$lipPts, [single]0.4)
      $penLip = New-Object System.Drawing.Pen (RGB $pHot[0] $pHot[1] $pHot[2] ([int]($alpha * 0.55))), ([single]($grooveW * 0.34 * $SS))
      $g.DrawPath($penLip, $lipPath); $penLip.Dispose(); $lipPath.Dispose()
    }
    $groove.Dispose()
  }

  # ---- the plate's own rim, last so it reads as the edge of everything under it
  $penRim = New-Object System.Drawing.Pen (RGB $pHot[0] $pHot[1] $pHot[2] ([int]([Math]::Min(235, $alpha * 1.45)))), ([single]((HalfWidthAt $prof (PlateY $F_PEC_WIDE)) * 0.030 * $SS))
  $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($penRim, $plate); $penRim.Dispose()
  $plate.Dispose()
}

# One pectoral: a closed curve filled with a RADIAL gradient, then its crease.
function DrawPectoral($g, $prof, [double]$side, [int]$alpha, $pDeep, $pMid, $pLit, $pHot) {
  $yPecTop  = PlateY $F_PEC_TOP
  $yPecWide = PlateY $F_PEC_WIDE
  $yPecBot  = PlateY $F_PEC_BOT
  $pecH = $yPecBot - $yPecTop
  $hwTop  = HalfSideAt $prof $yPecTop  $side
  $hwWide = HalfSideAt $prof $yPecWide $side
  $hwBot  = HalfSideAt $prof $yPecBot  $side

  # Seven control points round one pec. Every element parenthesised - the comma operator
  # binds tighter than arithmetic in PowerShell and an unparenthesised numeric array
  # literal comes back EMPTY, with the only symptom appearing three functions away.
  $raw = @(
    @(($CX + ($side * $hwTop  * 0.10)), ($yPecTop + ($pecH * 0.06))),   # inner top, by the sternum
    @(($CX + ($side * $hwTop  * 0.40)), ($yPecTop)),                     # the crown of the pec
    @(($CX + ($side * $hwTop  * 0.64)), ($yPecTop + ($pecH * 0.14))),    # sweeping out to the shoulder
    @(($CX + ($side * $hwWide * 0.735)),($yPecWide)),                    # widest
    @(($CX + ($side * $hwBot  * 0.630)),($yPecBot - ($pecH * 0.12))),
    @(($CX + ($side * $hwBot  * 0.340)),($yPecBot)),                     # lowest, near the middle
    @(($CX + ($side * $hwBot  * 0.090)),($yPecBot - ($pecH * 0.20)))     # back up to the sternum
  )
  $pecPts = @()
  foreach ($rawPt in $raw) {
    $pecPts += (New-Object System.Drawing.PointF ([single]($rawPt[0] * $SS)), ([single]($rawPt[1] * $SS)))
  }
  $pec = New-Object System.Drawing.Drawing2D.GraphicsPath
  # AddClosedCurve here, NOT AddPolygon - the opposite of the crest shards and the pauldron
  # lames. Those wanted angular facets and cut ends; muscle is the one thing in this file
  # that genuinely must be round, and a polygon through seven points reads as a gemstone.
  $pec.AddClosedCurve([System.Drawing.PointF[]]$pecPts, [single]0.45)

  # The dome. PathGradientBrush puts the bright centre where the light lands and falls to
  # the surround colour at the boundary - which is what a rounded mass does and what no
  # linear gradient can express. The centre sits UPPER-MIDDLE of the pec, symmetric about
  # the body's centre line, because a RimWorld sprite is lit near enough head-on and two
  # pecs lit from one side would read as the pawn standing at an angle.
  $bright = New-Object System.Drawing.Drawing2D.PathGradientBrush $pec
  $bright.CenterPoint = New-Object System.Drawing.PointF ([single](($CX + ($side * $hwWide * 0.40)) * $SS)), ([single](($yPecTop + ($pecH * 0.32)) * $SS))
  $bright.CenterColor = (RGB $pHot[0] $pHot[1] $pHot[2] ([int]([Math]::Min(255, $alpha * 1.15))))
  $bright.SurroundColors = [System.Drawing.Color[]]@((RGB $pMid[0] $pMid[1] $pMid[2] ([int]($alpha * 0.68))))
  $g.FillPath($bright, $pec); $bright.Dispose()

  # THE UNDER-PEC CREASE. This is the stroke that decides whether the whole piece reads as
  # muscle or as two bosses riveted to a plate: a dark arc where the pec overhangs, with a
  # LIT LIP just below it where the light catches the abdomen plate underneath. A crease
  # with no lip is a smudge; the pairing is what makes it a fold.
  $creaseRaw = @(
    @(($CX + ($side * $hwBot * 0.660)), ($yPecBot - ($pecH * 0.16))),
    @(($CX + ($side * $hwBot * 0.470)), ($yPecBot - ($pecH * 0.01))),
    @(($CX + ($side * $hwBot * 0.245)), ($yPecBot - ($pecH * 0.04))),
    @(($CX + ($side * $hwBot * 0.095)), ($yPecBot - ($pecH * 0.19)))
  )
  $creasePts = @()
  foreach ($rawPt in $creaseRaw) {
    $creasePts += (New-Object System.Drawing.PointF ([single]($rawPt[0] * $SS)), ([single]($rawPt[1] * $SS)))
  }
  $creaseW = $pecH * 0.115
  $crease = New-Object System.Drawing.Drawing2D.GraphicsPath
  $crease.AddCurve([System.Drawing.PointF[]]$creasePts, [single]0.45)
  $penCrease = New-Object System.Drawing.Pen (RGB $pDeep[0] $pDeep[1] $pDeep[2] ([int]($alpha * 0.90))), ([single]($creaseW * $SS))
  $penCrease.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penCrease.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawPath($penCrease, $crease); $penCrease.Dispose()

  $lipPts = @()
  foreach ($srcPt in $creasePts) {
    $lipPts += (New-Object System.Drawing.PointF ([single]$srcPt.X), ([single]($srcPt.Y + ($creaseW * 0.80 * $SS))))
  }
  $lip = New-Object System.Drawing.Drawing2D.GraphicsPath
  $lip.AddCurve([System.Drawing.PointF[]]$lipPts, [single]0.45)
  $penLip = New-Object System.Drawing.Pen (RGB $pLit[0] $pLit[1] $pLit[2] ([int]($alpha * 0.62))), ([single]($creaseW * 0.42 * $SS))
  $penLip.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penLip.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawPath($penLip, $lip); $penLip.Dispose(); $lip.Dispose()
  $crease.Dispose()

  # a thin hot edge along the pec's upper curve, where a polished plate would catch light
  $topRaw = @($raw[0], $raw[1], $raw[2], $raw[3])
  $topPts = @()
  foreach ($rawPt in $topRaw) {
    $topPts += (New-Object System.Drawing.PointF ([single]($rawPt[0] * $SS)), ([single]($rawPt[1] * $SS)))
  }
  $topEdge = New-Object System.Drawing.Drawing2D.GraphicsPath
  $topEdge.AddCurve([System.Drawing.PointF[]]$topPts, [single]0.45)
  $penTop = New-Object System.Drawing.Pen (RGB $pHot[0] $pHot[1] $pHot[2] ([int]($alpha * 0.70))), ([single]($pecH * 0.055 * $SS))
  $g.DrawPath($penTop, $topEdge); $penTop.Dispose(); $topEdge.Dispose()
  $pec.Dispose()
}

# The back's equivalent: a shoulder blade. Flatter and higher than a pec, and with no
# crease under it - a back has no overhang - so it is a dome and an upper edge only.
function DrawShoulderBlade($g, $prof, [double]$side, [int]$alpha, $pDeep, $pMid, $pLit) {
  $yTop  = PlateY ($F_PEC_TOP + 0.012)
  $yBot  = PlateY ($F_PEC_BOT - 0.030)
  $bladeH = $yBot - $yTop
  $hwMid = HalfSideAt $prof (PlateY $F_PEC_WIDE) $side
  $raw = @(
    @(($CX + ($side * $hwMid * 0.155)), ($yTop + ($bladeH * 0.10))),
    @(($CX + ($side * $hwMid * 0.480)), ($yTop)),
    @(($CX + ($side * $hwMid * 0.690)), ($yTop + ($bladeH * 0.34))),
    @(($CX + ($side * $hwMid * 0.520)), ($yBot)),
    @(($CX + ($side * $hwMid * 0.185)), ($yBot - ($bladeH * 0.22)))
  )
  $bladePts = @()
  foreach ($rawPt in $raw) {
    $bladePts += (New-Object System.Drawing.PointF ([single]($rawPt[0] * $SS)), ([single]($rawPt[1] * $SS)))
  }
  $blade = New-Object System.Drawing.Drawing2D.GraphicsPath
  $blade.AddClosedCurve([System.Drawing.PointF[]]$bladePts, [single]0.45)
  $bright = New-Object System.Drawing.Drawing2D.PathGradientBrush $blade
  $bright.CenterPoint = New-Object System.Drawing.PointF ([single](($CX + ($side * $hwMid * 0.42)) * $SS)), ([single](($yTop + ($bladeH * 0.38)) * $SS))
  $bright.CenterColor = (RGB $pLit[0] $pLit[1] $pLit[2] ([int]($alpha * 0.95)))
  $bright.SurroundColors = [System.Drawing.Color[]]@((RGB $pMid[0] $pMid[1] $pMid[2] ([int]($alpha * 0.45))))
  $g.FillPath($bright, $blade); $bright.Dispose()
  $penEdge = New-Object System.Drawing.Pen (RGB $pDeep[0] $pDeep[1] $pDeep[2] ([int]($alpha * 0.55))), ([single]($bladeH * 0.055 * $SS))
  $g.DrawPath($penEdge, $blade); $penEdge.Dispose(); $blade.Dispose()
}

# Which shoulder piece this build wears. A single dispatch rather than an `if` repeated
# at all four call sites: the two styles then take exactly the same arguments in exactly
# the same order, and no rotation can end up half-converted.
function DrawShoulderPiece($g, [double]$bx, [double]$by, [double]$len, [double]$thick,
                           [double]$dir, [int]$alpha, [double]$cool = 0.0) {
  if ($SHOULDER_STYLE -eq "pauldron") {
    DrawShoulderPauldron $g $bx $by $len $thick $dir $alpha $cool
  } else {
    DrawShoulderFins $g $bx $by $len $thick $dir $alpha $cool
  }
}

# =====================================================================================
#  CALL OF VALOR'S PAULDRON - a curved shoulder cap, in overlapping lames.
# =====================================================================================
#  The brief: "as if the pawn had armor pauldron, from the top of his shoulders to over
#  his chest, meaning you gotta use a bit of curves."
#
#  So this is NOT the fin with different numbers. A fin is a straight tapering blade
#  swung about its root; a pauldron is a band of PLATE that follows the shoulder's own
#  curve. It is built as concentric arc bands about a pivot sitting just below and
#  inboard of the shoulder point, each band swept from outboard-low, up over the top of
#  the shoulder, and down INBOARD across the upper chest.
#
#  Three things make it read as armour rather than as a hoop, and all three are curves:
#
#   1. THE SWEEP IS ASYMMETRIC about the top of the shoulder. It reaches further inboard
#      (over the chest) than outboard, which is what the brief actually asks for. A sweep
#      centred on the shoulder reads as a shoulder pad, not a pauldron.
#   2. THE BAND TAPERS TO A POINT at the chest end and stays full width outboard. A
#      constant-width arc is a croissant. The taper is what gives it a direction.
#   3. SUCCESSIVE LAMES ARE LONGER AS WELL AS LARGER. Real pauldron lames fan: each one
#      below the last reaches a little further round at both ends. Growing the radius
#      alone stacks concentric rings, which reads as a target, not as plate. This is the
#      same lesson the fins already carry - repeated shapes must be fanned, not just
#      resized - restated because the axis of the fan is different here.
#
#  It BREAKS THE SILHOUETTE by design: at the widest lame it projects ~16px past the
#  body edge on a male south sprite. Anything drawn inside the outline at this size is a
#  ten-pixel stripe and will not read - which is exactly why the procedural normal-plate
#  armour was rejected as dull.
#
#  Geometry is expressed entirely in multiples of $len, which the caller derives from the
#  body's own half-width AT THE SHOULDER LINE - so it fits all five silhouettes without a
#  second set of numbers, and a Fat pawn's belly cannot inflate it.
# ---------------------------------------------------------------------------------
function SmoothStep([double]$t) {
  if ($t -le 0.0) { return 0.0 }
  if ($t -ge 1.0) { return 1.0 }
  return ($t * $t * (3.0 - 2.0 * $t))
}

# One lame: a curved band of plate. $phiLo is the inboard end (over the chest), $phiHi
# the outboard end. Angles are measured from the INBOARD horizontal, rising through 90
# at the top of the shoulder - so the same numbers serve both shoulders and only the
# sign of the x offset changes.
function DrawLame($g, [double]$px, [double]$py, [double]$rMid, [double]$band,
                  [double]$phiLo, [double]$phiHi, [double]$dir, [int]$alpha, [double]$cool) {
  $sDeep = Lerp3 $C_DEEP $C_BLUE_DEEP $cool
  $sGold = Lerp3 $C_GOLD $C_BLUE_LIT  $cool
  $sHot  = Lerp3 $C_HOT  $C_BLUE_HOT  $cool
  $inb = -$dir          # inboard is +x for the left shoulder, -x for the right

  $steps = 34
  $outer = @()
  $inner = @()
  for ($stepIdx = 0; $stepIdx -le $steps; $stepIdx++) {
    $along = $stepIdx / [double]$steps                    # 0 at the chest end, 1 outboard
    $phi = ($phiLo + (($phiHi - $phiLo) * $along)) * [Math]::PI / 180.0
    # TAPER AT BOTH ENDS, fattest over the shoulder itself. The first version tapered only
    # the chest end and left the outboard end at full width, which made the lame read as a
    # hoop with a cut edge rather than as a plate that wraps and stops.
    $width = $band *
             (0.18 + (0.82 * (SmoothStep ($along / 0.38)))) *
             (0.45 + (0.55 * (SmoothStep ((1.0 - $along) / 0.22))))
    $rOut = $rMid + ($width * 0.5)
    $rIn  = $rMid - ($width * 0.5)
    $cosP = [Math]::Cos($phi); $sinP = [Math]::Sin($phi)
    # EVERY element parenthesised - the comma operator binds tighter than arithmetic here
    $outer += (New-Object System.Drawing.PointF ([single]($px + ($inb * $rOut * $cosP))), ([single]($py - ($rOut * $sinP))))
    $inner += (New-Object System.Drawing.PointF ([single]($px + ($inb * $rIn  * $cosP))), ([single]($py - ($rIn  * $sinP))))
  }
  # walk out along the outer edge, back along the inner one
  $pts = @()
  $pts += $outer
  for ($backIdx = $inner.Count - 1; $backIdx -ge 0; $backIdx--) { $pts += $inner[$backIdx] }

  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  # AddPolygon, not AddClosedCurve. The arc is already sampled at 34 steps so it is
  # smooth on its own, and a curve tension would round off the lame's cut ends - which
  # are the edges that make it read as a plate with a boundary rather than as a smear.
  $path.AddPolygon([System.Drawing.PointF[]]$pts)

  if ($SPUR_SEP -gt 0.0) {
    $sGold = BrightLift $sGold (0.18 * $SPUR_SEP)
    $sHot  = BrightLift $sHot  (0.18 * $SPUR_SEP)
    $rimBase = Lerp3 $C_DEEP_RAW $C_BLUE_DEEP_RAW $cool
    $rimCol = @(([int]($rimBase[0] * 0.72)), ([int]($rimBase[1] * 0.72)), ([int]($rimBase[2] * 0.72)))
    $penRim = New-Object System.Drawing.Pen (RGB $rimCol[0] $rimCol[1] $rimCol[2] ([int]([Math]::Min(255, $alpha * 1.45)))), ([single]($band * 0.30 * $SPUR_SEP))
    $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($penRim, $path); $penRim.Dispose()
  }

  $rect = $path.GetBounds()
  if ($rect.Width -lt 1) { $rect.Width = 1 }
  if ($rect.Height -lt 1) { $rect.Height = 1 }
  # 300 degrees, the same lighting direction the fins and scales use - a pauldron lit from
  # a different angle than the plate under it reads as a sticker.
  #
  # The dark end of the gradient is lifted 42% towards the lit tone. A fin is a compact
  # blob and can carry the full deep-to-lit range; a LAME IS A THIN BAND, and running it
  # from (34,58,84) - near black - to lit across ten pixels reads as a shadow with a bright
  # rim, not as a plate. The whole piece then looked like wire hoops laid over the pawn.
  # Same trap as the crest shards, which were filled down to C_BLUE_DEEP and came out with
  # a dark hole punched through each one: a "deep" palette stop shades a broad lit surface,
  # it does not fill a narrow one.
  $sFill = Lerp3 $sDeep $sGold 0.42
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $sFill[0] $sFill[1] $sFill[2] $alpha), (RGB $sGold[0] $sGold[1] $sGold[2] ([int]([Math]::Min(255, $alpha * 1.25)))), ([single]300.0)
  $g.FillPath($brush, $path); $brush.Dispose()

  # 0.09, not 0.20. The edge has to OUTLINE the band, not BE it - at 0.20 the stroke was
  # most of a tapered lame's width and the fill never showed.
  $pen = New-Object System.Drawing.Pen (RGB $sHot[0] $sHot[1] $sHot[2] ([int]([Math]::Min(230, $alpha * 1.4)))), ([single]($band * 0.09))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $path)
  $pen.Dispose(); $path.Dispose()
}

# Three lames per shoulder. Drawn OUTERMOST FIRST so the top lame overlaps the ones below
# it, which is the way real plate is riveted and the way it has to stack to read.
function DrawShoulderPauldron($g, [double]$bx, [double]$by, [double]$len, [double]$thick,
                              [double]$dir, [int]$alpha, [double]$cool = 0.0) {
  # Pivot: essentially ON the shoulder joint. The first version put it a long way below
  # and used radii up to 0.80 of $len, which threw the top of every arc 16px ABOVE the
  # shoulder line - up beside the neck - so it read as a raised collar rather than as
  # something worn on the shoulder. Small radii about a pivot at the joint keep the mass
  # where the shoulder actually is.
  # The pivot is pulled INBOARD, which is what buys the reach across the chest. Note the
  # trade being made: moving it inboard also pulls the outboard end back inside the body
  # outline, and an overlay that does not break the silhouette does not read at all. 0.18
  # with a slightly larger base radius keeps ~9px of overhang past the body edge while
  # bringing the inboard tip to within ~15px of the centre line - i.e. genuinely over the
  # pectoral rather than stopping at the collarbone.
  $px = $bx - ($dir * $len * 0.18)
  $py = $by + ($len * 0.16)
  $rBase = $len * 0.38
  $band  = $len * 0.42          # a LAME IS PLATE. Thin bands read as wire, however lit.
  $lames = 3
  for ($lameIdx = $lames - 1; $lameIdx -ge 0; $lameIdx--) {
    # 0.55 of the band, so consecutive lames overlap by nearly half and the three read as
    # one wrapped mass with seams. At 0.78 they were separated into concentric rings,
    # which reads as a target rather than as riveted plate.
    $rMid = $rBase + ($lameIdx * $band * 0.55)
    # BOTH ends sit BELOW the horizontal: the inboard end reaches down over the chest, the
    # outboard end hangs down the outside of the arm. Running the sweep entirely above the
    # pivot - which the first version did - is what made it a collar.
    $phiLo = -28.0  - ($lameIdx * 6.0)
    $phiHi = 186.0 + ($lameIdx * 5.0)
    # the top lame reads brightest; the ones under it fall back a little
    $lameAlpha = [int]($alpha * (1.0 - (0.10 * $lameIdx)))
    DrawLame $g $px $py $rMid $band $phiLo $phiHi $dir $lameAlpha $cool
  }
}

# ---------------------------------------------------------------------------------
# The TES5 crest: two rows of jagged crystal SHARDS running from the clavicles down the
# abdomen, each one growing outward from the centre line.
#
# This replaced a chain of rings. The rings came from a misremembering - the user checked
# against a screenshot and the real effect is a jagged, faceted extension. It also takes
# the armour's own bronze/blue ramp, so it turns with $VERSION like everything else.
# ---------------------------------------------------------------------------------

# ONE shard. Straight segments via AddPolygon, NEVER AddClosedCurve: the whole read here is
# "angular", and even a tension of 0.1 rounds the facets into a petal. The kinks partway
# along each edge are what separate a faceted shard from a plain triangle.
# $squash flattens it for the side-on view, where the crest is seen nearly edge-on.
# HOLLOW - stroke only, exactly like the rings this replaced. Nothing is painted inside; the
# armour underneath shows straight through. Three passes: bloom, wall, hot edge.
function DrawShard($g, [double]$x, [double]$y, [double]$len, [double]$halfH, [double]$angDeg, [double]$dir, [int]$alpha, [double]$cool, [double]$squash = 1.0) {
  $kMid  = Lerp3 $C_MID  $C_BLUE_MID  $cool
  $kLit  = Lerp3 $C_GOLD $C_BLUE_LIT  $cool
  $kHot  = Lerp3 $C_HOT  $C_BLUE_HOT  $cool

  $raw = @(
    @( (0.0),        (-$halfH) ),
    @( ($len*0.45),  (-$halfH*0.52) ),
    @( ($len),       (0.0) ),
    @( ($len*0.40),  ($halfH*0.78) ),
    @( (0.0),        ($halfH) )
  )
  $a = $angDeg * [Math]::PI / 180.0 * $dir
  $ca = [Math]::Cos($a); $sa = [Math]::Sin($a)
  $pts = @()
  foreach ($q in $raw) {
    $lx = $q[0] * $dir; $ly = $q[1]
    $pts += (New-Object System.Drawing.PointF ([single]($x + ($lx*$ca - $ly*$sa)*$squash)), ([single]($y + $lx*$sa + $ly*$ca)))
  }
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]$pts)

  # faint bloom first, so the shard sits IN the armour rather than on top of it
  $pen = New-Object System.Drawing.Pen (RGB $kMid[0] $kMid[1] $kMid[2] ([int]($alpha*0.20))), ([single]($halfH*1.3))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($pen, $p); $pen.Dispose()

  # the facet WALL, in the lit tone. Using $kDeep here is what made these read as holes:
  # $C_BLUE_DEEP is (20,44,84), near black, so every shard had a dark centre.
  $pen = New-Object System.Drawing.Pen (RGB $kLit[0] $kLit[1] $kLit[2] $alpha), ([single]($halfH*0.44))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $g.DrawPath($pen, $p); $pen.Dispose()

  # hot edge with MITRED joins - a rounded join blunts the facet corners, which is most
  # of what makes it read as crystal
  $pen = New-Object System.Drawing.Pen (RGB $kHot[0] $kHot[1] $kHot[2] ([int]([Math]::Min(255,$alpha*0.90)))), ([single]($halfH*0.19))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $g.DrawPath($pen, $p)
  $pen.Dispose(); $p.Dispose()
}

# A row of them down one side of the chest. The shards fan as they descend: angled up-and-out
# at the clavicle, swinging to down-and-out at the abdomen, which is what makes it read as a
# crest growing off the body rather than a column of identical spikes.
function DrawShardCrest($g, [double]$x0, [double]$y0, [double]$x1, [double]$y1, [int]$count, [double]$len0, [double]$len1, [double]$dir, [int]$alpha, [double]$squash = 1.0) {
  for ($i = 0; $i -lt $count; $i++) {
    $t = if ($count -le 1) { 0.0 } else { $i / [double]($count-1) }
    $x = $x0 + ($x1-$x0) * $t
    $y = $y0 + ($y1-$y0) * $t
    $len = $len0 + ($len1-$len0) * $t
    $ang = -46.0 + 86.0 * $t                     # up-and-out, sweeping to down-and-out
    # alternate the size slightly so the row is jagged rather than a neat comb
    $jit = if ($i % 2 -eq 0) { 1.0 } else { 0.78 }
    $cool = CoolAt (($y - $Y_TOP) / ($Y_BOT - $Y_TOP))
    DrawShard $g ($x*$SS) ($y*$SS) ($len*$jit*$SS) ($len*0.30*$SS) $ang $dir $alpha $cool $squash
  }
}

# ---------------------------------------------------------------------------------
# Build one body rotation at a given level. Level 1 = arms only, 2 = everything.
# ---------------------------------------------------------------------------------
function BuildBody([string]$rot, [int]$level) {
  UseRotation $rot
  $prof = $PROFILE_CUR

  $bmp = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear((RGB 0 0 0 0))
  $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

  $torso = BuildTorsoPath $prof
  $armSides = if ($rot -eq "east") { @(-1.0) } else { @(-1.0, 1.0) }
  $arms  = BuildArmsPath $prof $armSides

  if ($level -ge 2) {
    # --- fins first, so plates overlap their roots and they look grown-on ---
    $shoulderY = $SHOULDER_Y
    # Each shoulder sits on ITS OWN edge. Using one mirrored half-width hung the far fin
    # off the front of a side view, where the two edges differ by up to 58px.
    $hwSL = HalfSideAt $prof $shoulderY -1.0
    $hwSR = HalfSideAt $prof $shoulderY  1.0
    $spurLen = $SPUR_LEN * $SS
    $spurThick = $SPUR_THICK * $SS
    # The fins sit ON the shoulders, so they take the ramp's value AT the shoulders - gold
    # in version A, blue in version B. Leaving them on a fixed gold put warm fins on a cool
    # chest the moment the ramp was reversed.
    $finCool = CoolAt (($shoulderY - $Y_TOP) / ($Y_BOT - $Y_TOP))
    # One dispatch, so the four call sites below stay identical between the two styles and
    # cannot drift apart. $SHOULDER_STYLE is "fins" unless the champion is being built.
    #
    # DRAW ORDER DIFFERS BETWEEN THE TWO STYLES, AND IT HAS TO.
    #   fins:     fins first, then the scale field over their roots, so the fins look
    #             GROWN OUT of the body rather than stuck on it.
    #   pauldron: scales, then the cuirass, then the pauldrons LAST - because plate is
    #             worn, not grown, and a pauldron laps OVER the cuirass it is strapped to.
    #             Drawing them in the fins' order put the breastplate on top of the
    #             shoulder piece, which reads as a bib.
    $shoulderCalls = {
      if ($rot -eq "east") {
        # facing right, so BOTH sweep back to the left; drawing one forward crossed them
        DrawShoulderPiece $g (($CX - $hwSL*0.10)*$SS) (($shoulderY+8)*$SS) ($spurLen*0.70) ($spurThick*0.72) -1.0 90 $finCool
        DrawShoulderPiece $g (($CX - $hwSL*0.45)*$SS) ($shoulderY*$SS)     $spurLen        $spurThick       -1.0 170 $finCool
      } else {
        DrawShoulderPiece $g (($CX - $hwSL*0.80)*$SS) ($shoulderY*$SS) $spurLen $spurThick -1.0 170 $finCool
        DrawShoulderPiece $g (($CX + $hwSR*0.80)*$SS) ($shoulderY*$SS) $spurLen $spurThick  1.0 170 $finCool
      }
    }

    if ($SHOULDER_STYLE -ne "pauldron") { & $shoulderCalls }

    # --- torso plates. SPEC 4.4d wants apparel to read underneath, so these are faint:
    #     26 at the centre line rising to 88 at the edges. The first version used 96-170
    #     and hid the pawn completely.
    $g.SetClip($torso)
    FillScales $g $prof 26.0 88.0
    $g.ResetClip()

    if ($SHOULDER_STYLE -eq "pauldron") {
      # The cuirass takes the ramp at the CHEST, not at the shoulder line - it is a chest
      # piece, and reading the ramp where the fins read it would tint it for a height it
      # does not occupy.
      $plateCool = CoolAt (($F_PLATE_TOP + $F_PLATE_BOT) * 0.5)
      # 152, not the 132 this started at. The scale field is drawn UNDER the cuirass and
      # its regular pattern shows straight through a thin plate, which muddies exactly the
      # broad smooth shading the pectorals depend on to read as rounded. Still well short
      # of opaque - SPEC 4.4d wants the pawn's own apparel visible underneath.
      DrawChestPlate $g $prof $rot 152 $plateCool
      & $shoulderCalls
    }
  }

  # --- arm bands: present at EVERY level, and the only thing present at level 1.
  #     Denser than the torso, because at level 1 they carry the whole effect alone.
  $g.SetClip($arms)
  if ($level -ge 2) { FillScales $g $prof 70.0 118.0 } else { FillScales $g $prof 95.0 150.0 }
  $g.ResetClip()

  # --- two spikes on each arm, at the elbow ---
  # Same fin shape as the shoulders, small, and swung almost horizontal (fan ~72) so they
  # jut sideways off the arm rather than standing up like little shoulder fins. A run of
  # four down the whole sleeve read as a saw blade; a pair at the elbow reads as armour.
  # The arm spans y 102..196, so the elbow sits around y 148.
  $spikeAlpha = if ($level -ge 2) { 155 } else { 185 }
  foreach ($side in $armSides) {
    $k = 0
    foreach ($sy in $ELBOW_YS) {
      $shw = HalfSideAt $prof $sy $side
      # each spike takes the ramp at its OWN height, so it matches the sleeve it grows from
      $spCool = CoolAt (($sy - $Y_TOP) / ($Y_BOT - $Y_TOP))
      DrawSpur $g (($CX + $side*($shw-2.0))*$SS) ($sy*$SS) ((9.0 - $k*1.4)*$SS) (2.7*$SS) $side $spikeAlpha (70.0 + $k*6.0) $spCool
      $k++
    }
  }

  # --- arm band edging: a hot line down each sleeve so the vambrace has a shape ---
  $armPen = New-Object System.Drawing.Pen (RGB $C_GOLD[0] $C_GOLD[1] $C_GOLD[2] $(if ($level -ge 2) { 150 } else { 195 })), ([single](1.25*$SS))
  $armPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $g.DrawPath($armPen, $arms)
  $armPen.Dispose()

  if ($level -ge 2) {
    # --- rim light: TIGHT edge plus one whisper of bloom. Five fat strokes turned the
    #     first version into an orange sticker outline.
    #
    #     Drawn in horizontal BANDS so the rim follows the same bronze-to-blue ramp as the
    #     plates. A single warm rim over a cooled lower body looked like an outline that had
    #     been forgotten about. Bands overlap by a pixel; without that, hairline gaps show
    #     between them where the clip rectangles meet.
    $RIM_BANDS = 12
    for ($b = 0; $b -lt $RIM_BANDS; $b++) {
      $by0 = $Y_TOP + ($Y_BOT - $Y_TOP) * $b / $RIM_BANDS
      $by1 = $Y_TOP + ($Y_BOT - $Y_TOP) * ($b + 1) / $RIM_BANDS
      $bc = CoolAt (($b + 0.5) / $RIM_BANDS)
      $rimGlow = Lerp3 $C_EMBER $C_BLUE_LIT $bc
      $rimEdge = Lerp3 $C_GOLD  $C_BLUE_HOT $bc
      $clip = New-Object System.Drawing.RectangleF ([single]0), ([single](($by0-0.5)*$SS)), ([single]$N), ([single](($by1-$by0+1.0)*$SS))
      $g.SetClip($clip)
      for ($k = 4; $k -ge 1; $k--) {
        $pen = New-Object System.Drawing.Pen (RGB $rimGlow[0] $rimGlow[1] $rimGlow[2] ([int](7 + (4-$k)*5))), ([single](($k*1.7)*$SS))
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $torso); $pen.Dispose()
      }
      $pen = New-Object System.Drawing.Pen (RGB $rimEdge[0] $rimEdge[1] $rimEdge[2] 150), ([single](1.1*$SS))
      $g.DrawPath($pen, $torso); $pen.Dispose()
      $g.ResetClip()
    }

    # --- the jagged crest, growing out of each side of the chest ---
    #
    # SUPPRESSED FOR THE PAULDRON BUILD, and this is a real decision rather than a tidy-up.
    # The crest is two rows of bright crystal shards running down the middle of the chest -
    # exactly the surface the muscled cuirass occupies. Drawn together, the shards win: they
    # are the brightest thing on the sprite, and the pectorals underneath simply stop
    # existing. There is no alpha at which both read, because they are not layered, they are
    # competing for the same forms. So the champion gets the plate and loses the crest.
    #
    # It is also the right call thematically - the crest is a dragon's spine breaking out
    # through the skin, which is the Dovahkiin's signature, and this hero is a man in armour.
    # Reversible in one line if the crest is wanted back.
    if ($SHOULDER_STYLE -eq "pauldron") { $drawCrest = $false } else { $drawCrest = $true }
    if ($drawCrest) {
    # NOT clipped to the torso, unlike the rings it replaced: these are an EXTENSION and
    # their whole point is that the tips break past the body outline. Clipping them shaved
    # every tip flat against the silhouette and they read as a painted stripe again.
    # SIZE TAPER: the top shard is 21.0, double the 10.5 it used to be, falling to 6.8 at
    # the bottom - which is exactly what it already was. So the crest fans wide up by the
    # shoulder fins and thins to nothing by the waist, and the bottom row is untouched.
    # Colours are NOT affected: those still come from CoolAt at each shard's own height.
    # The crest starts out by the clavicles and converges onto the abdomen as it falls.
    # Both ends are taken as a fraction of the half-width AT THAT HEIGHT, so on an
    # hourglass body the crest follows the waist in rather than running straight down.
    $cTopY = $CREST_TOP_Y
    $cBotY = $CREST_BOT_Y
    if ($rot -eq "east") {
      # Side-on: one crest, sitting FORWARD on the trunk (the pawn faces right, so forward
      # is +x) and squashed, because edge-on it foreshortens along the view axis. Taken off
      # the RIGHT edge specifically - the front of the body - not a mirrored average.
      $xTopE = $CX + (HalfSideAt $prof $cTopY 1.0) * $F_CREST_X_TOP_E
      $xBotE = $CX + (HalfSideAt $prof $cBotY 1.0) * $F_CREST_X_BOT_E
      DrawShardCrest $g $xTopE $cTopY $xBotE $cBotY 10 $SHARD_TOP $SHARD_BOT 1.0 224 0.55
    } else {
      $dxTopL = (HalfSideAt $prof $cTopY -1.0) * $F_CREST_X_TOP
      $dxBotL = (HalfSideAt $prof $cBotY -1.0) * $F_CREST_X_BOT
      $dxTopR = (HalfSideAt $prof $cTopY  1.0) * $F_CREST_X_TOP
      $dxBotR = (HalfSideAt $prof $cBotY  1.0) * $F_CREST_X_BOT
      DrawShardCrest $g ($CX - $dxTopL) $cTopY ($CX - $dxBotL) $cBotY 10 $SHARD_TOP $SHARD_BOT -1.0 224
      DrawShardCrest $g ($CX + $dxTopR) $cTopY ($CX + $dxBotR) $cBotY 10 $SHARD_TOP $SHARD_BOT  1.0 224
    }
    }   # end if ($drawCrest)
  }

  $torso.Dispose(); $arms.Dispose(); $g.Dispose()

  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gf = [System.Drawing.Graphics]::FromImage($final)
  $gf.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gf.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gf.Clear((RGB 0 0 0 0))
  $gf.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0,0,$SIZE,$SIZE))
  $gf.Dispose(); $bmp.Dispose()

  # Knock the WHOLE overlay back as one object. Done here, on the finished texture, so it
  # catches every layer - plates, arm bands, fins, elbow spikes, rim light, crest - rather
  # than only the ones routed through FillScales. The helm is a separate texture and is
  # deliberately NOT touched here; decide it explicitly when a value is chosen.
  if ($OVERLAY_OPACITY -lt 1.0) {
    $fRect = New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE
    $fBits = $final.LockBits($fRect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $fBuf = New-Object 'byte[]' ($fBits.Stride * $SIZE)
    [System.Runtime.InteropServices.Marshal]::Copy([System.IntPtr]$fBits.Scan0, $fBuf, [int]0, [int]$fBuf.Length)
    for ($fi = 3; $fi -lt $fBuf.Length; $fi += 4) {
      $fBuf[$fi] = [byte][int][Math]::Round($fBuf[$fi] * $OVERLAY_OPACITY)
    }
    [System.Runtime.InteropServices.Marshal]::Copy($fBuf, 0, [System.IntPtr]$fBits.Scan0, $fBuf.Length)
    $final.UnlockBits($fBits)
  }
  return $final
}

# ---------------------------------------------------------------------------------
# The L3 helm. Drawn in the SAME 256 frame, centred, sized to a head that occupies
# ~31% x 39% of the draw quad (measured: 60x74 inside a 192 head frame). In game it is
# drawn at the pawn's head offset, which PawnRenderer.BaseHeadOffsetAt gives us.
# ---------------------------------------------------------------------------------
# The helm's skull cap: an ellipse at the front, serrated across the BACK.
#
# "Back" is whichever side is opposite the face, which in a top-down view means: for a
# south-facing pawn the face points down the screen, so the rear is the TOP of the sprite;
# for north it is the bottom; for east, facing right, it is the left.
#
# The teeth are a triangle wave on the radius, faded in by how closely each point faces the
# rear - so the serration grows out of the smooth front instead of starting at a seam. The
# polygon is walked at 1.5-degree steps: coarser than that and the teeth get rounded off by
# their own sampling, which defeats the point.
function BuildCapPath([double]$hcx, [double]$hcy, [double]$hw, [double]$hh, [string]$rot) {
  switch ($rot) {
    "south" { $bx = 0.0; $by = -1.0 }
    "north" { $bx = 0.0; $by =  1.0 }
    default { $bx = -1.0; $by = 0.0 }
  }
  # Fewer teeth, deeper, and sharpened. Eleven shallow bumps read as a wobbly edge rather
  # than a jagged one - the eye needs each tooth to be big enough to see as a tooth.
  $TEETH = 8.0
  $AMP   = 0.30
  $SHARP = 1.45      # >1 narrows each peak into a spike; 1.0 is a plain triangle
  $pts = New-Object System.Collections.ArrayList
  for ($deg = 0.0; $deg -lt 360.0; $deg += 1.5) {
    $th = $deg * [Math]::PI / 180.0
    $cx0 = [Math]::Cos($th); $sy0 = [Math]::Sin($th)
    # how much this point faces the rear, 0 at the sides, 1 dead astern
    $al = $cx0*$bx + $sy0*$by
    $w = ($al - 0.05) / 0.95
    if ($w -lt 0.0) { $w = 0.0 }
    if ($w -gt 1.0) { $w = 1.0 }
    $w = $w * $w * (3.0 - 2.0 * $w)
    # triangle wave, 0..1..0 per tooth - straight flanks, which is what reads as jagged
    $u = ($deg / 360.0) * $TEETH
    $fr = $u - [Math]::Floor($u)
    $tri = 1.0 - [Math]::Abs(2.0*$fr - 1.0)
    $tri = [Math]::Pow($tri, $SHARP)
    $k = 1.0 + $AMP * $w * $tri
    [void]$pts.Add((New-Object System.Drawing.PointF ([single](($hcx + $cx0*$hw*$k)*$SS)), ([single](($hcy + $sy0*$hh*$k)*$SS))))
  }
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddPolygon([System.Drawing.PointF[]]$pts.ToArray([System.Drawing.PointF]))
  $p.CloseFigure()
  return $p
}

function BuildHelm([string]$rot) {
  $bmp = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear((RGB 0 0 0 0))
  $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

  # The helm is drawn on the pawn's BODY mesh (see Thing_DragonAspectOverlay), so it has to be
  # sized inside this 256 frame the way a head occupies a body-sized quad - not the way a head
  # fills its own head texture.
  #
  # A head is about 60x74 px of a 192 head frame, and head and body quads are both 1.5 wide
  # (MeshPool.HumanlikeHeadAverageWidth = HumanlikeBodyWidth = 1.5). So a head covers 0.31 x
  # 0.39 of a quad, which in this 256 frame is about 80 x 100 px - half-width 40, half-height
  # 50. A helm sits slightly proud of the skull, hence 44 and 54.
  #
  # The first version used 31 and 38, which made the helm barely two thirds of a head. On top
  # of a fixed 0.93 draw size that came out at less than half, and the user reported the helm
  # as literally smaller than the pawn.
  $hcx = 128.0
  $hcy = 128.0
  $hw  = if ($rot -eq "east") { 39.0 } else { 44.0 }   # half-width of the skull cap
  $hh  = 54.0                                          # half-height

  # skull cap - round at the front, serrated across the back
  $cap = BuildCapPath $hcx $hcy $hw $hh $rot

  # The helm takes the same bronze-to-blue ramp as the body, but only to HELM_COOL_MAX
  # rather than the body's 0.92 - it sits at the top of the pawn and stays the warm focal
  # piece, with the blue creeping in around its lower edge.
  $HELM_COOL_MAX = 0.78
  $capBot = Lerp3 $C_DEEP $C_BLUE_DEEP $HELM_COOL_MAX
  $rect = New-Object System.Drawing.RectangleF ([single](($hcx-$hw)*$SS)), ([single](($hcy-$hh)*$SS)), ([single](($hw*2)*$SS)), ([single](($hh*2)*$SS))
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $C_MID[0] $C_MID[1] $C_MID[2] 120), (RGB $capBot[0] $capBot[1] $capBot[2] 150), ([single]90.0)
  $g.FillPath($brush, $cap); $brush.Dispose()

  # scales across the cap, cooling down the helm exactly as the body's do
  $g.SetClip($cap)
  $row = 0
  for ($y = ($hcy-$hh+3)*$SS; $y -le ($hcy+$hh)*$SS; $y += 7.0*$SS) {
    $hcf = (($y/$SS) - ($hcy-$hh)) / (2.0*$hh)
    if ($hcf -lt 0.0) { $hcf = 0.0 }
    if ($hcf -gt 1.0) { $hcf = 1.0 }
    $hcool = $hcf * $hcf * (3.0 - 2.0 * $hcf) * $HELM_COOL_MAX
    $col = 0
    for ($x = ($hcx-$hw-8)*$SS; $x -le ($hcx+$hw+8)*$SS; $x += 10.0*$SS) {
      $off = if ($row % 2 -eq 0) { 0.0 } else { 5.0*$SS }
      $h1 = (($row*73 + $col*151) % 17)/17.0 - 0.5
      DrawScale $g ($x+$off) $y (11.0*$SS) (9.0*$SS) 0.75 ([int](70*(1.0+$h1*0.3))) $hcool
      $col++
    }
    $row++
  }
  $g.ResetClip()

  # brow ridge and a central crest running front to back
  $pen = New-Object System.Drawing.Pen (RGB $C_GOLD[0] $C_GOLD[1] $C_GOLD[2] 170), ([single](2.2*$SS))
  $crest = New-Object System.Drawing.Drawing2D.GraphicsPath
  $crest.AddCurve([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF ([single]($hcx*$SS)), ([single](($hcy-$hh+4)*$SS))),
    (New-Object System.Drawing.PointF ([single]($hcx*$SS)), ([single]($hcy*$SS))),
    (New-Object System.Drawing.PointF ([single]($hcx*$SS)), ([single](($hcy+$hh-5)*$SS)))
  ), [single]0.3)
  $g.DrawPath($pen, $crest); $pen.Dispose(); $crest.Dispose()

  # rim, gold at the crown into blue at the lower edge. A Pen built from a
  # LinearGradientBrush does this in one stroke - no need for the banded-clip trick the
  # torso rim uses, because a closed ellipse takes a gradient brush cleanly.
  $rimTop = $C_GOLD
  $rimBot = Lerp3 $C_GOLD $C_BLUE_HOT $HELM_COOL_MAX
  $rimRect = New-Object System.Drawing.RectangleF ([single](($hcx-$hw)*$SS)), ([single](($hcy-$hh)*$SS)), ([single](($hw*2)*$SS)), ([single](($hh*2)*$SS))
  $rimBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rimRect, (RGB $rimTop[0] $rimTop[1] $rimTop[2] 185), (RGB $rimBot[0] $rimBot[1] $rimBot[2] 185), ([single]90.0)
  $pen = New-Object System.Drawing.Pen $rimBrush, ([single](1.3*$SS))
  $g.DrawPath($pen, $cap); $pen.Dispose(); $rimBrush.Dispose()

  # two horns sweeping back from the temples - same fin shape as the shoulders, so the
  # helm reads as part of the same armour rather than a separate hat
  # Two horns a side now, FANNED apart - the same lesson as the shoulders, where a second
  # horn at the same angle simply vanished inside the first. The extra one is shorter and
  # swung further towards horizontal.
  $hornLen = 26.0*$SS; $hornThick = 6.0*$SS
  # The second horn is LONGER AND THINNER than a scaled-down copy would be, and fanned only
  # ~28 degrees. A short fat spur at a 40-degree fan is nearly all root, so it rendered as a
  # blunt wedge stuck to the side of the helm rather than as a horn.
  if ($rot -eq "east") {
    DrawSpur $g (($hcx-$hw*0.35)*$SS) (($hcy-$hh*0.15)*$SS) $hornLen $hornThick -1.0 190  0.0
    DrawSpur $g (($hcx-$hw*0.16)*$SS) (($hcy+$hh*0.16)*$SS) ($hornLen*0.82) ($hornThick*0.52) -1.0 150 30.0
  } else {
    DrawSpur $g (($hcx-$hw*0.80)*$SS) (($hcy-$hh*0.10)*$SS) $hornLen $hornThick -1.0 190  0.0
    DrawSpur $g (($hcx+$hw*0.80)*$SS) (($hcy-$hh*0.10)*$SS) $hornLen $hornThick  1.0 190  0.0
    DrawSpur $g (($hcx-$hw*0.70)*$SS) (($hcy+$hh*0.24)*$SS) ($hornLen*0.80) ($hornThick*0.52) -1.0 165 28.0
    DrawSpur $g (($hcx+$hw*0.70)*$SS) (($hcy+$hh*0.24)*$SS) ($hornLen*0.80) ($hornThick*0.52)  1.0 165 28.0
  }
  $cap.Dispose(); $g.Dispose()

  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gf = [System.Drawing.Graphics]::FromImage($final)
  $gf.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gf.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gf.Clear((RGB 0 0 0 0))
  $gf.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0,0,$SIZE,$SIZE))
  $gf.Dispose(); $bmp.Dispose()
  return $final
}

# ---------------------------------------------------------------------------------
# The L3 aura, in TWO pieces.
#
# It was one texture with every flare baked into it, which cannot do what is wanted: if the
# flares live in one image they can only fade together. Individual particles winking in and
# out needs each flare to be its OWN draw, so the aura is now:
#
#   DragonAspectAuraRing.png   a faint smooth ring - the constant underglow, so the effect
#                              never goes fully empty between particles
#   DragonAspectFlare.png      ONE curved tongue, pointing up from the frame centre. The
#                              game draws it many times at different rotations, each with
#                              its own life cycle and its own tint, so 2-3 are alight at any
#                              moment and they swirl.
#
# Both are authored WHITE and tinted at draw time, which keeps the orange and the azure
# tunable without regenerating any art.
# ---------------------------------------------------------------------------------
function BuildAuraRing() {
  # Written PER PIXEL, not as stacked FillEllipse calls.
  #
  # The first version drew 220 concentric FILLED discs of low alpha. Every one of them
  # also covers the centre, so alpha accumulated there and the "ring" came out as a solid
  # disc that swallowed the pawn. A radial falloff has to be evaluated per pixel from the
  # radius - there is no way to stack filled shapes into one.
  #
  # No supersampling: a smooth radial gradient has no edges to alias, so 256 is exact.
  $bmp = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $rect = New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $buf = New-Object byte[] ($data.Stride * $SIZE)

  $c = ($SIZE - 1) / 2.0
  $norm = $SIZE / 2.0
  $rPeak = 0.70       # where the band of light sits, as a fraction of the half-frame
  $rWide = 0.30       # half-width of the band
  $maxA  = 44.0       # faint: the flares carry the effect, this only stops it going empty

  for ($y = 0; $y -lt $SIZE; $y++) {
    $dy = ($y - $c) / $norm
    $row = $y * $data.Stride
    for ($x = 0; $x -lt $SIZE; $x++) {
      $dx = ($x - $c) / $norm
      $r = [Math]::Sqrt($dx*$dx + $dy*$dy)
      # early-out first: this is what keeps a per-pixel pass in PowerShell to seconds
      if ($r -gt 1.0 -or $r -lt 0.30) { continue }
      $d = ($r - $rPeak) / $rWide
      $v = 1.0 - $d*$d
      if ($v -le 0.0) { continue }
      $v = $v * $v
      $a = [int]($maxA * $v)
      if ($a -lt 1) { continue }
      $i = $row + $x*4
      $buf[$i]   = 255                 # B - authored white so the game can tint it
      $buf[$i+1] = 255                 # G
      $buf[$i+2] = 255                 # R
      $buf[$i+3] = [byte]$a
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $buf.Length)
  $bmp.UnlockBits($data)
  return $bmp
}

# ---------------------------------------------------------------------------------
# ONE small CRESCENT, CENTRED in its own frame.
#
# It used to sit offset above the frame centre, so that rotating the quad swung it around
# the pawn. That baked the orbit into the texture and welded the two together: one rotation
# moved the moon AND turned it, so every moon always faced the same way relative to the
# pawn and none of them could point elsewhere.
#
# Centred instead, the draw does the placing in two independent steps -
#   translate to the pawn, rotate by the POSITION angle, translate out by the orbit,
#   then rotate by the SPIN angle
# - so where a moon sits and which way its horns point are now separate numbers.
#
# A crescent moon is built as a DISC WITH A DISC BITTEN OUT of it - an outer circle minus a
# slightly smaller circle pushed off to one side. It is not an arc with a thickness profile.
#
# That distinction is the whole thing. Sweeping a tapered band along an arc can only ever
# make a banana: to get horns that curl right round towards each other you have to take the
# difference of two circles, and the wrap angle then falls out of the radii and the offset
# rather than being dialled in. These numbers give about 250 degrees of wrap.
#
# The inner circle's radius is modulated slightly with angle so one horn runs longer than
# the other. Without that the crescent is symmetric about the line through both centres,
# which means mirroring it is the same as rotating it - and the handedness variety in the
# aura would quietly stop existing.
# ---------------------------------------------------------------------------------
# Returns BOTH crescents from one mask: the ember-to-azure blend, and a plain WHITE copy of
# the identical shape. The white one exists so some moons can be a single flat colour -
# tinting the blended sprite cannot do that, because a tint multiplies, so tinting the blend
# ember would leave its azure end a dark muddy brown rather than turning it orange.
# The blur is the expensive part and is shared, so the second texture is nearly free.
function BuildFlarePair() {
  $bmp = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $rect = New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $buf = New-Object byte[] ($data.Stride * $SIZE)

  $bmpP = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $dataP = $bmpP.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bufP = New-Object byte[] ($dataP.Stride * $SIZE)

  $c = ($SIZE - 1) / 2.0
  $norm = $SIZE / 2.0
  # The tongue spans r 0.52..0.99, a reach of under 2x rather than the 3.3x it had at 0.30.
  # That ratio is fixed in the texture, so it is what decides how LONG a flare looks at any
  # drawn size - a 3.3x span could not be made short by shrinking it, only by moving it in
  # towards the pawn as well. Keeping the span short is what lets two rows of these sit at
  # different radii without the inner row reaching straight through the outer one.
  # ORBIT is separate from the crescent's own size ON PURPOSE: shrinking the drawn quad
  # would pull the particles inward as well as making them smaller, so size lives here and
  # the draw size is left to choose the orbit.
  #
  # R_IN and OFFSET are held in PROPORTION to R_OUT. The wrap angle depends only on their
  # ratios, so scaling all three together shrinks the moon without reopening it into a
  # banana - which is what happened when only R_OUT was reduced.
  # The moon now fills its own frame, so the drawn quad size IS the moon's size and the
  # orbit is applied by the caller.
  $R_OUT   = 0.500     # outer circle
  $R_IN    = $R_OUT * 0.856    # circle bitten out of it
  $OFFSET  = $R_OUT * 0.356    # how far that bite is pushed aside - this sets the wrap
  $SKEW    = 0.075     # angular modulation of R_IN, so one horn outruns the other
  # Radius of the box blur applied to the finished mask. Three box passes approximate a
  # gaussian, and this is scaled with R_OUT so the halo stays the same fraction of the moon
  # as before: the shape ends up mostly halo, which is the ghostly look wanted.
  # Check it still fits: 0.500 + 3*14/128 = 0.83 of the half-frame, inside the edge.
  $BLUR_R  = 14
  $BLUR_N  = 3
  $maxA    = 178.0     # peak after the blur is renormalised
  # The ess shape's spine half-length. Declared out here because the colour pass needs it too,
  # to run the blend ALONG the S rather than around it.
  $B_HALF_LEN = 0.455

  # crescent centre = frame centre
  $ox = 0.0
  $oy = 0.0
  # centre of the bite, pushed towards +x
  $ix = $ox + $OFFSET
  $iy = $oy
  $reach = $R_OUT

  # ---- 1. the hard mask ----
  # A distance-to-nearest-edge ramp cannot produce a ghostly look on a shape this thin: the
  # ramp has only a few pixels to work in, so it reads as a slightly soft edge on a solid
  # moon. A real BLUR is what gives a halo, because it spreads light OUTSIDE the shape as
  # well as softening the inside. So the mask is built hard here and blurred afterwards.
  $mask = New-Object double[] ($SIZE * $SIZE)

  if ($PARTICLE_SHAPE -eq "moon") {
    # --- A: two circles subtracted. Both tips curl the same way. ---
    for ($y = 0; $y -lt $SIZE; $y++) {
      $dy = ($y - $c) / $norm
      if ([Math]::Abs($dy - $oy) -gt $reach) { continue }     # whole rows skipped: fast
      $mrow = $y * $SIZE
      for ($x = 0; $x -lt $SIZE; $x++) {
        $dx = ($x - $c) / $norm
        $pox = $dx - $ox; $poy = $dy - $oy
        $dOut = [Math]::Sqrt($pox*$pox + $poy*$poy)
        if ($dOut -gt $reach) { continue }

        $pix = $dx - $ix; $piy = $dy - $iy
        $dIn = [Math]::Sqrt($pix*$pix + $piy*$piy)

        # one horn longer than the other: swell the bite slightly on one side
        $rIn = $R_IN * (1.0 + $SKEW * ($piy / [Math]::Max([double]0.0001, [double]$dIn)))

        if ($dOut -ge $R_OUT) { continue }     # outside the outer circle
        if ($dIn -le $rIn)    { continue }     # inside the bite
        $mask[$mrow + $x] = 1.0
      }
    }
  }
  else {
    # --- B: one tip hooked the other way. ---
    #
    # Subtracting circles CANNOT produce this: the two horns of that construction always
    # curl the same way, because they are both ends of one arc bounded by one bite. An S
    # needs the curvature to change SIGN halfway along, so B is built from a spine instead
    # - a full sine wave, which is exactly one hook each way - with the thickness tapering
    # to a point at both ends so the body still reads as a moon rather than a ribbon.
    #
    # Rasterised through a GraphicsPath rather than per pixel, because distance-to-a-curve
    # per pixel is far more work than letting GDI+ fill the outline. Everything after this
    # point is shared with A, so the blur and the colour blend do not care which ran.
    $L  = $B_HALF_LEN
    # The S has to swing WIDER than the blur or it does not survive it. At an amplitude of
    # 0.19 against a blur sigma of ~0.11 of the half-frame, the two hooks smeared into one
    # blob and the shape read as a bean. The swing is now nearly three times the blur, and
    # the ribbon is thinner so the two lobes stay distinct rather than merging.
    $AM = 0.315      # how far the S swings off the axis
    $T0 = 0.103      # max half-thickness, at the middle
    $steps = 150
    $upper = New-Object System.Collections.ArrayList
    $lower = New-Object System.Collections.ArrayList
    for ($i = 0; $i -le $steps; $i++) {
      $s = -1.0 + 2.0 * $i / $steps
      $px = $L * $s
      $py = $AM * [Math]::Sin([Math]::PI * $s)
      # tangent, then its normal, so the thickness is laid across the spine
      $tx = $L
      $ty = $AM * [Math]::PI * [Math]::Cos([Math]::PI * $s)
      $tl = [Math]::Sqrt($tx*$tx + $ty*$ty)
      $nx = -$ty / $tl; $ny = $tx / $tl
      $th = $T0 * [Math]::Pow([Math]::Max([double]0.0, [double](1.0 - $s*$s)), 0.62)
      [void]$upper.Add((New-Object System.Drawing.PointF ([single](($c + ($px + $nx*$th)*$norm))), ([single](($c + ($py + $ny*$th)*$norm)))))
      [void]$lower.Insert(0, (New-Object System.Drawing.PointF ([single](($c + ($px - $nx*$th)*$norm))), ([single](($c + ($py - $ny*$th)*$norm)))))
    }
    $all = New-Object System.Collections.ArrayList
    [void]$all.AddRange($upper); [void]$all.AddRange($lower)

    $mb = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mg = [System.Drawing.Graphics]::FromImage($mb)
    $mg.Clear((RGB 0 0 0 0))
    $mg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $mp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $mp.AddPolygon([System.Drawing.PointF[]]$all.ToArray([System.Drawing.PointF]))
    $mp.CloseFigure()
    $mbr = New-Object System.Drawing.SolidBrush (RGB 255 255 255 255)
    $mg.FillPath($mbr, $mp)
    $mbr.Dispose(); $mp.Dispose(); $mg.Dispose()

    $mrect = New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE
    $mdat = $mb.LockBits($mrect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mbuf = New-Object byte[] ($mdat.Stride * $SIZE)
    [System.Runtime.InteropServices.Marshal]::Copy($mdat.Scan0, $mbuf, 0, $mbuf.Length)
    $mb.UnlockBits($mdat); $mb.Dispose()
    for ($y = 0; $y -lt $SIZE; $y++) {
      $srow = $y * $mdat.Stride; $mrow = $y * $SIZE
      for ($x = 0; $x -lt $SIZE; $x++) { $mask[$mrow + $x] = $mbuf[$srow + $x*4 + 3] / 255.0 }
    }
  }

  # ---- 2. blur it: BLUR_N box passes, which approximate a gaussian ----
  $tmp = New-Object double[] ($SIZE * $SIZE)
  $win = 2*$BLUR_R + 1
  $last = $SIZE - 1
  for ($pass = 0; $pass -lt $BLUR_N; $pass++) {
    for ($y = 0; $y -lt $SIZE; $y++) {          # horizontal, mask -> tmp
      $base = $y * $SIZE
      $sum = 0.0
      for ($k = -$BLUR_R; $k -le $BLUR_R; $k++) {
        $xx = if ($k -lt 0) { 0 } elseif ($k -gt $last) { $last } else { $k }
        $sum += $mask[$base + $xx]
      }
      for ($x = 0; $x -lt $SIZE; $x++) {
        $tmp[$base + $x] = $sum / $win
        $xo = $x - $BLUR_R;     if ($xo -lt 0) { $xo = 0 }
        $xi = $x + $BLUR_R + 1; if ($xi -gt $last) { $xi = $last }
        $sum += $mask[$base + $xi] - $mask[$base + $xo]
      }
    }
    for ($x = 0; $x -lt $SIZE; $x++) {          # vertical, tmp -> mask
      $sum = 0.0
      for ($k = -$BLUR_R; $k -le $BLUR_R; $k++) {
        $yy = if ($k -lt 0) { 0 } elseif ($k -gt $last) { $last } else { $k }
        $sum += $tmp[$yy * $SIZE + $x]
      }
      for ($y = 0; $y -lt $SIZE; $y++) {
        $mask[$y * $SIZE + $x] = $sum / $win
        $yo = $y - $BLUR_R;     if ($yo -lt 0) { $yo = 0 }
        $yi = $y + $BLUR_R + 1; if ($yi -gt $last) { $yi = $last }
        $sum += $tmp[$yi * $SIZE + $x] - $tmp[$yo * $SIZE + $x]
      }
    }
  }

  # ---- 3. renormalise and write out, with the colour blend baked in ----
  # Blurring a thin shape costs a lot of peak amplitude, so rescale to the intended maximum
  # rather than guessing a compensating constant.
  $peak = 0.0
  for ($i = 0; $i -lt $mask.Length; $i++) { if ($mask[$i] -gt $peak) { $peak = $mask[$i] } }
  if ($peak -le 0.0) { $peak = 1.0 }
  $scale = $maxA / $peak

  # Each moon runs ember at one horn into azure at the other, rather than each moon being
  # wholly one colour. ALPHA comes from the blurred mask but COLOUR is recomputed from the
  # geometry, so the blend stays clean instead of smearing with the blur - and it still
  # covers the halo, where the mask has spread beyond the original shape.
  #
  # Note the trade: the two colours now live in this texture instead of in the draw-time
  # tint. Changing them means regenerating rather than editing a number in the caller.
  $xh = ($OFFSET*$OFFSET + $R_OUT*$R_OUT - $R_IN*$R_IN) / (2.0*$OFFSET)
  $yh = [Math]::Sqrt([Math]::Max([double]0.0, [double]($R_OUT*$R_OUT - $xh*$xh)))
  $hornAng = [Math]::Atan2($yh, $xh)          # where the horns sit, measured from +x
  $span = 2.0*[Math]::PI - 2.0*$hornAng       # angle the crescent body covers
  $TWO_PI = 2.0 * [Math]::PI

  for ($y = 0; $y -lt $SIZE; $y++) {
    $row = $y * $data.Stride
    $mrow = $y * $SIZE
    $poy = ($y - $c) / $norm - $oy
    for ($x = 0; $x -lt $SIZE; $x++) {
      $a = [int]($mask[$mrow + $x] * $scale)
      if ($a -lt 1) { continue }
      if ($a -gt 255) { $a = 255 }

      $pox = ($x - $c) / $norm - $ox
      if ($PARTICLE_SHAPE -eq "moon") {
        # A curls round, so the blend runs round it - measured as angle from one horn
        $ang = [Math]::Atan2($poy, $pox)
        if ($ang -lt 0.0) { $ang += $TWO_PI }
        $t = ($ang - $hornAng) / $span        # 0 at one horn, 1 at the other
      } else {
        # B runs along its spine, so the blend runs along x. Using the angle here would
        # sweep the gradient round a shape that does not go round, and the two colours
        # would land across the S rather than along it.
        $t = ($pox + $B_HALF_LEN) / (2.0 * $B_HALF_LEN)
      }
      if ($t -lt 0.0) { $t = 0.0 }
      if ($t -gt 1.0) { $t = 1.0 }
      # Smoothstepped TWICE: each colour then holds most of its own horn and the change
      # happens quickly across the middle, instead of the whole moon being in transition.
      $t = $t * $t * (3.0 - 2.0 * $t)
      $t = $t * $t * (3.0 - 2.0 * $t)
      # Through a hot midtone, NOT straight from one to the other. Lerping ember to azure
      # in RGB passes through (188,170,155) - a dead grey-tan - so every moon came out
      # washed and muddy through its middle. Routing via a warm near-white keeps both ends
      # saturated and reads as the two colours meeting at something hot.
      $col = if ($t -lt 0.5) {
        Lerp3 $C_EMBER $C_BLEND_MID ($t * 2.0)
      } else {
        Lerp3 $C_BLEND_MID $C_AZURE (($t - 0.5) * 2.0)
      }

      $i = $row + $x*4
      $buf[$i]   = [byte]$col[2]              # B
      $buf[$i+1] = [byte]$col[1]              # G
      $buf[$i+2] = [byte]$col[0]              # R
      $buf[$i+3] = [byte]$a
      # same shape, no colour - this one gets tinted at draw time
      $bufP[$i]   = 255
      $bufP[$i+1] = 255
      $bufP[$i+2] = 255
      $bufP[$i+3] = [byte]$a
    }
  }
  [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $buf.Length)
  $bmp.UnlockBits($data)
  [System.Runtime.InteropServices.Marshal]::Copy($bufP, 0, $dataP.Scan0, $bufP.Length)
  $bmpP.UnlockBits($dataP)
  return @{ blend = $bmp; plain = $bmpP }
}

# =================================================================================
# Generate everything
# =================================================================================
# A/B harness support: sweep the opacity knobs without paying for a full 30-texture run
# plus the aura, the flares and the preview sheet. Absent = a normal full run.
$FAST_MODE  = ($env:DOVAH_FAST -eq "1")
$emitTypes  = $BODY_TYPES
$emitDest   = $DEST
$emitSuffix = ""
if ($env:DOVAH_ONLY)    { $emitTypes  = $env:DOVAH_ONLY -split "," }
if ($env:DOVAH_DESTDIR) { $emitDest   = $env:DOVAH_DESTDIR }
if ($env:DOVAH_SUFFIX)  { $emitSuffix = $env:DOVAH_SUFFIX }

$bodies = @{}
Write-Output ("plate alpha {0}, deep lift {1}, lit falloff {2}" -f $PLATE_ALPHA, $DEEP_LIFT, $LIT_FALLOFF)
Write-Output "measuring body silhouettes off Beautiful Bodies:"
foreach ($bt in $emitTypes) {
  SetBodyGeometry $bt
  $emitLvls = if ($FAST_MODE) { @(2) } else { @(1,2) }
  $emitRots = if ($FAST_MODE) { @("south","east") } else { @("south","north","east") }
  foreach ($lvl in $emitLvls) {
    foreach ($rot in $emitRots) {
      $img = BuildBody $rot $lvl
      $path = Join-Path $emitDest ("DragonAspect_L${lvl}_${bt}_${rot}${emitSuffix}.png")
      $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
      $bodies["${bt}_${lvl}_$rot"] = $img
    }
  }
  Write-Output ("    wrote textures for " + $bt)
}

if ($FAST_MODE) {
  foreach ($k in $bodies.Keys) { $bodies[$k].Dispose() }
  Write-Output "FAST MODE - skipped helm, aura, flares and the preview sheet"
  Write-Output "DONE"
  return
}
$helms = @{}
foreach ($rot in @("south","north","east")) {
  $img = BuildHelm $rot
  $path = Join-Path $emitDest "DragonAspectHelm_$rot.png"
  $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "wrote $path"
  $helms[$rot] = $img
}
$aura = BuildAuraRing
$aura.Save((Join-Path $emitDest "DragonAspectAuraRing.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $emitDest 'DragonAspectAuraRing.png')"
$flarePair = BuildFlarePair
$flare  = $flarePair.blend
$flareP = $flarePair.plain
$flare.Save((Join-Path $emitDest "DragonAspectFlare.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $emitDest 'DragonAspectFlare.png')"
$flareP.Save((Join-Path $emitDest "DragonAspectFlarePlain.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $emitDest 'DragonAspectFlarePlain.png')"


# =================================================================================
# PREVIEW SHEET. The reference pawn is read only to build this and is never shipped.
# =================================================================================
$refDir = $REF_DIR
$CELL = 232
$sheetW = $CELL*4 + 40*5
$sheetH = $CELL*5 + 66*5 + 110
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gs = [System.Drawing.Graphics]::FromImage($sheet)
$gs.Clear((RGB 38 40 36 255))
$gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gs.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$font  = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontS = New-Object System.Drawing.Font "Segoe UI", 11, ([System.Drawing.FontStyle]::Bold)
$fontT = New-Object System.Drawing.Font "Segoe UI", 10
$white = New-Object System.Drawing.SolidBrush (RGB 235 235 230 255)
$grey  = New-Object System.Drawing.SolidBrush (RGB 165 168 160 255)
$gold  = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)

# Draw the white aura texture TINTED, exactly as the game will: two passes at different
# scales and colours. Drawing it raw is what made the preview a white blob - the texture is
# authored greyscale on purpose so the two colours stay tunable without regenerating art.
function DrawTinted($gs, $img, $rect, $col, [double]$strength) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single]($col[0]/255.0); $cm.Matrix11 = [single]($col[1]/255.0); $cm.Matrix22 = [single]($col[2]/255.0)
  $cm.Matrix33 = [single]$strength; $cm.Matrix44 = [single]1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $gs.DrawImage($img, $rect, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}

# One flare, tinted and rotated about the frame centre - the same call the game makes per
# particle. Used by the still sheet to show a representative moment of the animation.
# Place one crescent. $posDeg is WHERE it sits around the pawn, $spinDeg is which way its
# horns point - two independent numbers, which is the whole reason the sprite is centred in
# its own frame rather than offset within it. $mirror flips the horn asymmetry.
function DrawFlareAt($gs, $img, [double]$cx, [double]$cy, [double]$size, [double]$posDeg, [double]$orbit, [double]$spinDeg, $col, [double]$strength, [bool]$mirror = $false) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single]($col[0]/255.0); $cm.Matrix11 = [single]($col[1]/255.0); $cm.Matrix22 = [single]($col[2]/255.0)
  $cm.Matrix33 = [single]$strength; $cm.Matrix44 = [single]1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $st = $gs.Save()
  $gs.TranslateTransform([single]$cx, [single]$cy)
  $gs.RotateTransform([single]$posDeg)          # where around the pawn
  $gs.TranslateTransform([single]0.0, [single](-$orbit))
  $gs.RotateTransform([single]$spinDeg)         # which way the horns point
  if ($mirror) { $gs.ScaleTransform([single](-1.0), [single]1.0) }
  $r = New-Object System.Drawing.Rectangle ([int](-$size/2)), ([int](-$size/2)), ([int]$size), ([int]$size)
  $gs.DrawImage($img, $r, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $gs.Restore($st)
  $ia.Dispose()
}

# Rough LIT GROUND under the pawn.
#
# The notebook has carried this as an unactioned lesson for a round: the plates were
# authored and signed off against a flat dark backdrop, and over real terrain in game the
# user reported them as barely visible - they needed 1.85x. A dark background flatters low
# alpha enormously. Judging a translucent overlay on one is not a preview, it is a
# different question. Deterministic, so the sheet reproduces between runs.
function DrawGround($gs, [int]$x, [int]$y, [int]$size, [int]$salt) {
  $base = RGB 122 106 84 255
  $br = New-Object System.Drawing.SolidBrush $base
  $gs.FillRectangle($br, $x, $y, $size, $size)
  $br.Dispose()
  $cellPx = 11
  for ($gy = 0; $gy -lt $size; $gy += $cellPx) {
    for ($gx = 0; $gx -lt $size; $gx += $cellPx) {
      $hsh = [Math]::Sin(($gx + 1) * 12.9898 + ($gy + 1) * 78.233 + $salt * 37.719) * 43758.5453
      $hsh = $hsh - [Math]::Floor($hsh)
      $d = [int](($hsh - 0.5) * 42.0)
      $c = RGB (122 + $d) (106 + $d) (84 + [int]($d * 0.8)) 255
      $b2 = New-Object System.Drawing.SolidBrush $c
      $gs.FillRectangle($b2, ($x + $gx), ($y + $gy), $cellPx, $cellPx)
      $b2.Dispose()
    }
  }
}

function DrawPawnCell($gs, $x, $y, $rot, $img, $helm, $auraImg, $flareImg, $flarePlainImg, $refDir, $CELL, $bodyType, $salt) {
  DrawGround $gs $x $y $CELL $salt
  if ($auraImg -ne $null) {
    $cx = $x + $CELL/2.0; $cy = $y + $CELL/2.0
    # TWO bands of underglow - azure wide, ember tight. This is the layering from the
    # earlier version; collapsing it to a single ring flattened the whole effect.
    DrawTinted $gs $auraImg (New-Object System.Drawing.Rectangle ($x-26), ($y-26), ($CELL+52), ($CELL+52)) $C_AZURE 0.95
    DrawTinted $gs $auraImg (New-Object System.Drawing.Rectangle ($x+16), ($y+16), ($CELL-32), ($CELL-32)) $C_EMBER 0.80
    # A representative still. Two orbits, mixed colours, mixed handedness, and - note the
    # spin column - horns pointing in unrelated directions, which is only possible because
    # position and spin are separate arguments now.
    # Five alight, which is the new average. The last two use spin -90: horns turned away
    # from the pawn, the mode the seven extra slots use.
    #
    # $C_WHITE, not a colour: the moons carry their own ember-to-azure blend now, and the
    # tint MULTIPLIES, so passing ember here would filter the azure end out of every one.
    # Half blended (white tint on the blended sprite), a quarter flat ember and a quarter
    # flat azure (an ember or azure tint on the PLAIN sprite).
    #                                     size          pos   orbit  spin
    DrawFlareAt $gs $flareImg      $cx $cy ($CELL*0.23)  47.0 ($CELL*0.160)  18.0 $C_WHITE 1.00 $false
    DrawFlareAt $gs $flareImg      $cx $cy ($CELL*0.25) 233.0 ($CELL*0.160) 205.0 $C_WHITE 0.62 $true
    DrawFlareAt $gs $flareImg      $cx $cy ($CELL*0.31) 141.0 ($CELL*0.242) 297.0 $C_WHITE 0.88 $true
    DrawFlareAt $gs $flarePlainImg $cx $cy ($CELL*0.29) 311.0 ($CELL*0.242) 112.0 $C_EMBER 0.72 $false
    DrawFlareAt $gs $flarePlainImg $cx $cy ($CELL*0.27)  95.0 ($CELL*0.242) (-90.0+22.0) $C_AZURE 0.85 $false
    DrawFlareAt $gs $flareImg      $cx $cy ($CELL*0.24) 274.0 ($CELL*0.242) (-90.0-17.0) $C_WHITE 0.70 $true
  }
  $refPath = Join-Path $refDir "Naked_${bodyType}_$rot.png"
  if (Test-Path $refPath) {
    $ref = New-Object System.Drawing.Bitmap $refPath
    $gs.DrawImage($ref, (New-Object System.Drawing.Rectangle $x, $y, $CELL, $CELL))
    $ref.Dispose()
  }
  $gs.DrawImage($img, (New-Object System.Drawing.Rectangle $x, $y, $CELL, $CELL))
  if ($helm -ne $null) {
    # head sits above the body; this preview offset is approximate and gets one tuning
    # pass in game against PawnRenderer.BaseHeadOffsetAt
    $hs = [int]($CELL*0.62)
    $hx = $x + [int](($CELL-$hs)/2)
    $hy = $y + [int]($CELL*0.055)
    $gs.DrawImage($helm, (New-Object System.Drawing.Rectangle $hx, $hy, $hs, $hs))
  }
}

# One ROW PER BODY TYPE. The question this sheet has to answer is no longer "do the three
# levels look right" - that was signed off - but "does each body type's armour fit THAT
# body". So every cell paints the real body sprite for its own type, over lit ground.
$COLS = @(
  @("south", 1, $false, "south, L1 (arms only)"),
  @("south", 2, $true,  "south, L3 (full)"),
  @("north", 2, $true,  "north, L3"),
  @("east",  2, $true,  "east, L3")
)
$r = 0
foreach ($bt in $BODY_TYPES) {
  $y = 62 + $r * ($CELL + 66)
  $gs.DrawString(("BODY TYPE: " + $bt.ToUpper()), $fontS, $gold, [single]40, [single]($y - 26))
  $i = 0
  foreach ($cspec in $COLS) {
    $rot = $cspec[0]
    $lvl = $cspec[1]
    $full = $cspec[2]
    $x = 40 + $i * ($CELL + 40)
    $useHelm   = if ($full) { $helms[$rot] } else { $null }
    $useAura   = if ($full) { $aura }       else { $null }
    $useFlare  = if ($full) { $flare }      else { $null }
    $useFlareP = if ($full) { $flareP }     else { $null }
    DrawPawnCell $gs $x $y $rot $bodies["${bt}_${lvl}_$rot"] $useHelm $useAura $useFlare $useFlareP $refDir $CELL $bt ($r*7 + $i)
    $gs.DrawString($cspec[3], $fontT, $grey, [single]$x, [single]($y + $CELL + 2))
    $i++
  }
  $r++
}

# colony-zoom strip: every body type at play distance, bare then L3
$yz = 62 + 5*($CELL+66) - 6
$gs.DrawString("colony zoom, 48px - each body type BARE then with L3, over lit ground:", $fontT, $grey, [single]40, [single]($yz-20))
$zx = 40
$zi = 0
foreach ($bt in $BODY_TYPES) {
  $refPath = Join-Path $refDir "Naked_${bt}_south.png"
  $ref = New-Object System.Drawing.Bitmap $refPath
  # bare
  DrawGround $gs $zx $yz 48 (100 + $zi)
  $gs.DrawImage($ref, (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48))
  $gs.DrawString($bt, $fontT, $grey, [single]$zx, [single]($yz + 50))
  $zx += 56
  # with L3
  DrawGround $gs $zx $yz 48 (200 + $zi)
  DrawTinted $gs $aura (New-Object System.Drawing.Rectangle ($zx-7), ($yz-7), 62, 62) $C_AZURE 0.95
  DrawTinted $gs $aura (New-Object System.Drawing.Rectangle ($zx+4), ($yz+4), 40, 40) $C_EMBER 0.80
  DrawFlareAt $gs $flare  ($zx+24) ($yz+24) 12  34.0  8.0  18.0 $C_WHITE 1.00 $false
  DrawFlareAt $gs $flare  ($zx+24) ($yz+24) 15 128.0 12.0 297.0 $C_WHITE 0.85 $true
  DrawFlareAt $gs $flareP ($zx+24) ($yz+24) 11 212.0  8.0 205.0 $C_EMBER 0.72 $true
  $gs.DrawImage($ref, (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48))
  $gs.DrawImage($bodies["${bt}_2_south"], (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48))
  $gs.DrawImage($helms["south"], (New-Object System.Drawing.Rectangle ($zx+9), ($yz+3), 30, 30))
  $ref.Dispose()
  $zx += 76
  $zi++
}
$gs.DrawString("Dragon Aspect - fitted per body type, SPEC 4.4d", $font, $white, [single]40, [single]12)
$gs.Dispose()

if ($env:DOVAH_PREVIEW) { $PREVIEW = $env:DOVAH_PREVIEW }
$sheetPath = Join-Path $PREVIEW "dragon_aspect_levels.png"
$sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
foreach ($k in $bodies.Keys) { $bodies[$k].Dispose() }
foreach ($k in $helms.Keys)  { $helms[$k].Dispose() }
$aura.Dispose(); $flare.Dispose(); $flareP.Dispose()
Write-Output "wrote preview $sheetPath"
Write-Output "DONE"
