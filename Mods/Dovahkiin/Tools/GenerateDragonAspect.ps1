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
#    DragonAspect_L1_{south,north,east}.png    arms only
#    DragonAspect_L2_{south,north,east}.png    full body  (L3 reuses these)
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
$PREVIEW = "C:\Users\User\AppData\Local\Temp\claude\C--Games-Rimworld-RimWorld-RimWorldFolder-DovahkiinClaudePluged\c6ec21ec-5e0f-440d-a0de-21ea4365fe8b\scratchpad"
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
#  PLATE OPACITY - deliberately 1.0, which is the ORIGINAL signed-off art.
#
#  When the first playtest reported the armour as "barely visible" this was raised to 1.85.
#  That was the wrong fix: opacity is what makes the plates translucent, and cranking it made
#  the armour darker and heavier than the design that had been agreed. The visibility problem
#  was solved by DEEPENING THE COLOURS instead - see the palette below - which reads against
#  lit ground without touching the transparency at all.
#
#  Leave this at 1.0. If the armour needs to read harder, saturate the palette further.
# ---------------------------------------------------------------------------------
$PLATE_ALPHA = 1.0

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
# Body profile - our own parametric half-width curve, sampled from the measurements.
# It starts NARROW at the neck (y=88) and widens to the shoulders; without that the
# torso renders as a flat-topped bucket, which is how the first version failed.
# ---------------------------------------------------------------------------------
$PROFILE_SOUTH = @(
  @( 88, 15), @( 92, 27), @( 96, 37), @(102, 44), @(110, 48), @(118, 50),
  @(126, 51), @(134, 50), @(142, 48), @(150, 46), @(158, 43), @(166, 41),
  @(174, 40), @(182, 39), @(190, 38), @(198, 36), @(206, 33), @(211, 28), @(214, 21)
)
$PROFILE_NORTH = @(
  @( 88, 15), @( 92, 26), @( 96, 36), @(102, 43), @(110, 47), @(118, 49),
  @(126, 50), @(134, 49), @(142, 47), @(150, 45), @(158, 42), @(166, 40),
  @(174, 39), @(182, 38), @(190, 37), @(198, 35), @(206, 32), @(211, 27), @(214, 20)
)
$PROFILE_EAST = @(
  @( 88, 14), @( 92, 23), @( 96, 31), @(102, 37), @(110, 41), @(118, 43),
  @(126, 43), @(134, 42), @(142, 41), @(150, 38), @(158, 36), @(166, 34),
  @(174, 32), @(182, 31), @(190, 30), @(198, 28), @(206, 26), @(211, 22), @(214, 17)
)

$CX = 127.5
$Y_TOP = 88.0
$Y_BOT = 214.0
$ARM_Y_TOP = 102.0
$ARM_Y_BOT = 196.0
$ARM_W = 15.0          # arm band width inward from the silhouette edge

function HalfWidthAt($prof, [double]$y) {
  if ($y -le $prof[0][0]) { return [double]$prof[0][1] }
  $last = $prof.Count - 1
  if ($y -ge $prof[$last][0]) { return [double]$prof[$last][1] }
  for ($i = 0; $i -lt $last; $i++) {
    $y0 = [double]$prof[$i][0]; $y1 = [double]$prof[$i+1][0]
    if ($y -ge $y0 -and $y -le $y1) {
      $t = ($y - $y0) / ($y1 - $y0)
      $t = $t * $t * (3.0 - 2.0 * $t)
      return [double]$prof[$i][1] + ($prof[$i+1][1] - $prof[$i][1]) * $t
    }
  }
  return [double]$prof[$last][1]
}

function BuildTorsoPath($prof) {
  $pts = New-Object System.Collections.ArrayList
  for ($y = $Y_TOP; $y -le $Y_BOT; $y += 1.0) {
    $hw = HalfWidthAt $prof $y
    [void]$pts.Add((New-Object System.Drawing.PointF ([single](($CX+$hw)*$SS)), ([single]($y*$SS))))
  }
  for ($y = $Y_BOT; $y -ge $Y_TOP; $y -= 1.0) {
    $hw = HalfWidthAt $prof $y
    [void]$pts.Add((New-Object System.Drawing.PointF ([single](($CX-$hw)*$SS)), ([single]($y*$SS))))
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
      $hw = HalfWidthAt $prof $y
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
    $hw = (HalfWidthAt $prof $y256) * $SS
    $offset = if ($row % 2 -eq 0) { 0.0 } else { $scaleW * 0.5 }
    $lit = 1.0 - [Math]::Min(1.0, ($y256-$Y_TOP)/($Y_BOT-$Y_TOP)) * 0.75
    # The bronze/blue ramp down the body, in whichever direction $VERSION selects.
    # Smoothstepped inside CoolAt, so one end stays convincingly its own colour and the
    # change happens across the middle rather than the whole torso being a half-and-half wash.
    $cool = CoolAt (($y256 - $Y_TOP) / ($Y_BOT - $Y_TOP))
    $col = 0
    for ($x = $CX*$SS - $hw - $scaleW; $x -le $CX*$SS + $hw + $scaleW; $x += $scaleW*0.86) {
      $px = $x + $offset
      $dx = [Math]::Abs($px - $CX*$SS) / [Math]::Max(1.0, $hw)
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

  $rect = $p.GetBounds()
  if ($rect.Width -lt 1) { $rect.Width = 1 }
  if ($rect.Height -lt 1) { $rect.Height = 1 }
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (RGB $sDeep[0] $sDeep[1] $sDeep[2] $alpha), (RGB $sGold[0] $sGold[1] $sGold[2] ([int]([Math]::Min(255,$alpha*1.25)))), ([single]300.0)
  $g.FillPath($brush, $p); $brush.Dispose()

  $pen = New-Object System.Drawing.Pen (RGB $sHot[0] $sHot[1] $sHot[2] ([int]([Math]::Min(230,$alpha*1.4)))), ([single]($thick*0.16))
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
  switch ($rot) {
    "south" { $prof = $PROFILE_SOUTH }
    "north" { $prof = $PROFILE_NORTH }
    "east"  { $prof = $PROFILE_EAST  }
  }

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
    $shoulderY = 116.0
    $hwS = HalfWidthAt $prof $shoulderY
    $spurLen = 31.0 * $SS
    $spurThick = 8.2 * $SS
    # The fins sit ON the shoulders, so they take the ramp's value AT the shoulders - gold
    # in version A, blue in version B. Leaving them on a fixed gold put warm fins on a cool
    # chest the moment the ramp was reversed.
    $finCool = CoolAt (($shoulderY - $Y_TOP) / ($Y_BOT - $Y_TOP))
    if ($rot -eq "east") {
      # facing right, so BOTH fans sweep back to the left; drawing one forward crossed them
      DrawShoulderFins $g (($CX - $hwS*0.10)*$SS) (($shoulderY+8)*$SS) ($spurLen*0.70) ($spurThick*0.72) -1.0 90 $finCool
      DrawShoulderFins $g (($CX - $hwS*0.45)*$SS) ($shoulderY*$SS)     $spurLen        $spurThick       -1.0 170 $finCool
    } else {
      DrawShoulderFins $g (($CX - $hwS*0.80)*$SS) ($shoulderY*$SS) $spurLen $spurThick -1.0 170 $finCool
      DrawShoulderFins $g (($CX + $hwS*0.80)*$SS) ($shoulderY*$SS) $spurLen $spurThick  1.0 170 $finCool
    }

    # --- torso plates. SPEC 4.4d wants apparel to read underneath, so these are faint:
    #     26 at the centre line rising to 88 at the edges. The first version used 96-170
    #     and hid the pawn completely.
    $g.SetClip($torso)
    FillScales $g $prof 26.0 88.0
    $g.ResetClip()
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
    foreach ($sy in @(141.0, 155.0)) {
      $shw = HalfWidthAt $prof $sy
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
    # NOT clipped to the torso, unlike the rings it replaced: these are an EXTENSION and
    # their whole point is that the tips break past the body outline. Clipping them shaved
    # every tip flat against the silhouette and they read as a painted stripe again.
    # SIZE TAPER: the top shard is 21.0, double the 10.5 it used to be, falling to 6.8 at
    # the bottom - which is exactly what it already was. So the crest fans wide up by the
    # shoulder fins and thins to nothing by the waist, and the bottom row is untouched.
    # Colours are NOT affected: those still come from CoolAt at each shard's own height.
    if ($rot -eq "east") {
      # Side-on: one crest, sitting FORWARD on the trunk (the pawn faces right, so forward
      # is +x) and squashed, because edge-on it foreshortens along the view axis.
      DrawShardCrest $g 146.0 108.0 139.0 188.0 10 21.0 6.8 1.0 224 0.55
    } else {
      # clavicles at y~106, x~107 and x~148; converging onto the abdomen as they fall
      DrawShardCrest $g 107.0 108.0 118.0 188.0 10 21.0 6.8 -1.0 224
      DrawShardCrest $g 148.0 108.0 137.0 188.0 10 21.0 6.8  1.0 224
    }
  }

  $torso.Dispose(); $arms.Dispose(); $g.Dispose()

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
$bodies = @{}
foreach ($lvl in @(1,2)) {
  foreach ($rot in @("south","north","east")) {
    $img = BuildBody $rot $lvl
    $path = Join-Path $DEST "DragonAspect_L${lvl}_$rot.png"
    $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "wrote $path"
    $bodies["${lvl}_$rot"] = $img
  }
}
$helms = @{}
foreach ($rot in @("south","north","east")) {
  $img = BuildHelm $rot
  $path = Join-Path $DEST "DragonAspectHelm_$rot.png"
  $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output "wrote $path"
  $helms[$rot] = $img
}
$aura = BuildAuraRing
$aura.Save((Join-Path $DEST "DragonAspectAuraRing.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $DEST 'DragonAspectAuraRing.png')"
$flarePair = BuildFlarePair
$flare  = $flarePair.blend
$flareP = $flarePair.plain
$flare.Save((Join-Path $DEST "DragonAspectFlare.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $DEST 'DragonAspectFlare.png')"
$flareP.Save((Join-Path $DEST "DragonAspectFlarePlain.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "wrote $(Join-Path $DEST 'DragonAspectFlarePlain.png')"


# =================================================================================
# PREVIEW SHEET. The reference pawn is read only to build this and is never shipped.
# =================================================================================
$refDir = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$CELL = 232
$sheetW = $CELL*3 + 40*4
$sheetH = $CELL*3 + 66*3 + 96
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

function DrawPawnCell($gs, $x, $y, $rot, $img, $helm, $auraImg, $flareImg, $flarePlainImg, $refDir, $CELL) {
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
  $refPath = Join-Path $refDir "Naked_Male_$rot.png"
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

$rowLabels = @(
  @(1, "LEVEL 1 - one word (Mul): arm armour only"),
  @(2, "LEVEL 2 - two words (Mul Qah): + plates, 3 fins a side, jagged chest crest"),
  @(3, "LEVEL 3 - three words (Mul Qah Diiv): + horned helm, + orange/azure aura")
)
$r = 0
foreach ($rl in $rowLabels) {
  $lvl = $rl[0]
  $y = 62 + $r * ($CELL + 66)
  $gs.DrawString($rl[1], $fontS, $gold, [single]40, [single]($y - 26))
  $i = 0
  foreach ($rot in @("south","north","east")) {
    $x = 40 + $i * ($CELL + 40)
    $bodyKey = if ($lvl -eq 1) { "1_$rot" } else { "2_$rot" }
    $useHelm = if ($lvl -eq 3) { $helms[$rot] } else { $null }
    $useAura = if ($lvl -eq 3) { $aura } else { $null }
    $useFlare = if ($lvl -eq 3) { $flare } else { $null }
    $useFlareP = if ($lvl -eq 3) { $flareP } else { $null }
    DrawPawnCell $gs $x $y $rot $bodies[$bodyKey] $useHelm $useAura $useFlare $useFlareP $refDir $CELL
    $gs.DrawString($rot, $fontT, $grey, [single]$x, [single]($y + $CELL + 2))
    $i++
  }
  $r++
}

# colony-zoom strip
$yz = 62 + 3*($CELL+66) - 6
$gs.DrawString("colony zoom, 48px - bare, L1, L2, L3:", $fontT, $grey, [single]40, [single]($yz-20))
$zx = 40
$refPath = Join-Path $refDir "Naked_Male_south.png"
$ref = New-Object System.Drawing.Bitmap $refPath
$gs.DrawImage($ref, (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48)); $zx += 62
foreach ($spec in @(@("1_south",$null,$null), @("2_south",$null,$null), @("2_south",$helms["south"],$aura))) {
  if ($spec[2] -ne $null) {
    DrawTinted $gs $spec[2] (New-Object System.Drawing.Rectangle ($zx-7), ($yz-7), 62, 62) $C_AZURE 0.95
    DrawTinted $gs $spec[2] (New-Object System.Drawing.Rectangle ($zx+4), ($yz+4), 40, 40) $C_EMBER 0.80
    DrawFlareAt $gs $flare  ($zx+24) ($yz+24) 12  34.0  8.0  18.0 $C_WHITE 1.00 $false
    DrawFlareAt $gs $flare  ($zx+24) ($yz+24) 15 128.0 12.0 297.0 $C_WHITE 0.85 $true
    DrawFlareAt $gs $flareP ($zx+24) ($yz+24) 11 212.0  8.0 205.0 $C_EMBER 0.72 $true
  }
  $gs.DrawImage($ref, (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48))
  $gs.DrawImage($bodies[$spec[0]], (New-Object System.Drawing.Rectangle $zx, $yz, 48, 48))
  if ($spec[1] -ne $null) { $gs.DrawImage($spec[1], (New-Object System.Drawing.Rectangle ($zx+9), ($yz+3), 30, 30)) }
  $zx += 62
}
$ref.Dispose()
$gs.DrawString("Dragon Aspect - the three words, SPEC 4.4d", $font, $white, [single]40, [single]12)
$gs.Dispose()

$sheetPath = Join-Path $PREVIEW "dragon_aspect_levels.png"
$sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
foreach ($k in $bodies.Keys) { $bodies[$k].Dispose() }
foreach ($k in $helms.Keys)  { $helms[$k].Dispose() }
$aura.Dispose(); $flare.Dispose(); $flareP.Dispose()
Write-Output "wrote preview $sheetPath"
Write-Output "DONE"
