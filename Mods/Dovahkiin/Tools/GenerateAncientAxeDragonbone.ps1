# =====================================================================================
#  The Ancient Dragonborn's spectral weapon - DRAGONBONE BATTLEAXE shape.
#
#  Reshaped at the user's request from the halberd profile to Skyrim's Dragonbone
#  Battleaxe, which is thematically the right weapon for him anyway. Colours are
#  UNCHANGED - the user asked for shape only - so this still carries the armour's
#  ember-to-blue ramp and still reads as the same conjuration.
#
#  PREVIEW ONLY BY DEFAULT. $WRITE_TEXTURE stays $false until the shape is approved, so
#  the shipping PNG - which is committed and working - cannot be clobbered mid-iteration.
#
#  ---------------------------------------------------------------------------------
#  MEASURED OFF THE USER'S SECOND REFERENCE, which arrived already in OUR orientation
#  (head at top-right). That mattered enormously - the first reference was mirrored and
#  three features were read wrong from it. Pixel coordinates below are from that image,
#  socket at about (900, 250), ring pommel at (75, 1225), weapon length about 1277px.
#
#  1. THE HAFT BOWS TOWARD THE BLADE, NOT AWAY. At mid-height the haft sits about 68px
#     to the RIGHT of the straight chord from pommel to socket - the same side the blade
#     is on. The first attempt guessed the opposite and was wrong. $BOW is POSITIVE.
#
#  2. THE SPIKES DO NOT RUN ALONG THE HAFT. Measured, the main spike runs from the socket
#     to about (700, 20): direction (-0.657, -0.755), which decomposes as 0.994 of
#     MINUS-the-normal plus 0.124 of the haft axis - i.e. essentially PERPENDICULAR to the
#     haft, on the side OPPOSITE the blade, leaning about 8 degrees toward the head. The
#     first attempt built them as a halberd's spear point running past the head, which is
#     a different weapon entirely.
#
#  3. THERE IS A LARGE HOLE THROUGH THE BLADE, centred about (130, 140) from the socket
#     and roughly 150px across. It is one of the weapon's signature features and the
#     first attempt had nothing like it. Punched with a second sub-path and FillMode
#     Alternate, so the keyline strokes its rim for free.
#
#  Blade extents, as fractions of weapon length: outward 0.29, along +0.155 down to -0.35.
#  So it is much TALLER than wide (0.505 against 0.29) and hangs well below the socket.
#  ---------------------------------------------------------------------------------
#  TWO CONSTRAINTS FROM SPECTRAL_HALBERD_PRESET.md
#  ---------------------------------------------------------------------------------
#  1. HEAD AT TOP-RIGHT, haft to bottom-left. Every Melee Animation tweak value
#     (OffX/OffY/Rotation/BladeStart/BladeEnd) is expressed in Medieval Overhaul's frame
#     and they run that diagonal. Get it wrong and the pawn grips the weapon by the blade.
#  2. The head should stay near the halberd's position along the axis, because BladeStart
#     0.8519 / BladeEnd 1.5263 mark the cutting portion.
#
#     A REFERENCE-PROPORTIONED BLADE CANNOT FIT AT THE HALBERD'S LENGTH. Outward from the
#     head runs right-and-down; with the head at x=0.82 the most that fits before leaving
#     the frame is about 0.22, and the reference needs 0.29 of a 1.055 length = 0.31 of
#     frame. So the weapon is shortened about 16%, which needs ONE in-game check of the
#     hold - and the preset already calls that offset eyeballed and unverified.
#
#  The curved haft is the other new risk. The bend is concentrated in the UPPER half so
#  the grip stays nearly straight and the hand still sits on the haft - the same trick the
#  aura flares needed, raising the exponent so the bend lands in the last third.
#
#  PowerShell traps this is written around, all previously paid for in this project:
#    - variable names are CASE-INSENSITIVE. The old generator used $N for the canvas; this
#      one uses $CANVAS so no lowercase $n can ever collide with it.
#    - the comma operator binds TIGHTER than arithmetic, so every element of a numeric
#      array literal is parenthesised
#    - [Math]::Max(0, $double) picks the int overload and truncates - use [double]0.0
#    - a function returns EVERYTHING it emits, so no Write-Output inside a helper
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$WRITE_TEXTURE = $true         # approved 2026-07-30: the traced blade is the shipping shape
$USE_HOLE      = $false        # the traced region is solid where the hole used to be, because
                               # the user's mask kept that area - so no hole is punched now

# THE BLADE TRACED FROM THE USER'S PAINTED MASK. Empty = draw the original fan below, which is
# the base they painted over and which extract_blade.ps1 needs in order to measure. Set to the
# traced polygon to draw THEIR shape. Keeping the two separate is deliberate: editing the base
# in place silently changes what the extraction means.
#
# These 20 points are not designed, they are MEASURED - Douglas-Peucker at 1.8px over the
# 201-point contour of (the original blade MINUS the user's white paint), converted back
# through the blade basis the generator publishes. Alignment was solved from the ring pommel
# and the spike tip at scale 2.0093 with 0.0px error on both cross-checks.
# extract_blade.ps1 reproduces every number.
$TRACED_BLADE = @(
  @( ( 0.122), ( 0.179) ),
  @( ( 0.312), ( 0.145) ),
  @( ( 0.387), ( 0.190) ),
  @( ( 0.462), ( 0.094) ),
  @( ( 0.509), ( 0.128) ),
  @( ( 0.543), ( 0.297) ),
  @( ( 0.577), ( 0.297) ),
  @( ( 0.625), ( 0.247) ),
  @( ( 0.666), (-0.018) ),
  @( ( 0.489), (-0.233) ),
  @( ( 0.476), (-0.436) ),
  @( ( 0.231), (-0.650) ),
  @( ( 0.149), (-0.459) ),
  @( ( 0.224), (-0.413) ),
  @( ( 0.272), (-0.267) ),
  @( ( 0.258), (-0.216) ),
  @( ( 0.197), (-0.199) ),
  @( ( 0.136), (-0.295) ),
  @( ( 0.095), (-0.284) ),
  @( ( 0.115), ( 0.162) )
)

$DEST_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\Things\Item\Equipment"
$OUT_DIR = $env:DOVAH_PREVIEW
if (-not $OUT_DIR) { $OUT_DIR = $PSScriptRoot }

$SIZE   = 256
$SS     = 4                    # supersample factor
$CANVAS = $SIZE * $SS

# --- palette, lifted from the armour generator. UNCHANGED - shape-only request. ---
$C_BLUE_HOT  = @(132,186,246)
$C_BLUE_LIT  = @( 58,124,216)
$C_BLUE_MID  = @( 36, 80,150)
$C_GOLD      = @(228,152, 44)
$C_HOT       = @(255,206,120)
$C_EMBER     = @(240,118, 28)
$C_LINE      = @( 14, 18, 28)

# --- layout. Shortened from the halberd's so a reference-sized blade fits the frame. ---
$BUTT_X = 0.155; $BUTT_Y = 0.895
$HEAD_X = 0.745; $HEAD_Y = 0.235

# POSITIVE: bows toward the blade side, as measured off the reference. See note 1 above.
$BOW = 0.080

# Haft: near-parallel. The taper was what made the first halberd read as a wedge.
$HAFT_W_BUTT = 0.0150
$HAFT_W_HEAD = 0.0170
$OUTLINE     = 0.0095
$BANDS       = 44              # they follow a curve now, so more of them

# Blade, as fractions of WEAPON LENGTH, straight off the reference.
$BLADE_OUT_F   = 0.290
$BLADE_ALONG_F = 0.350

# Spikes: perpendicular-ish, opposite the blade. See note 2.
$SPIKE_LEN_F   = 0.240         # main, as a fraction of weapon length
$SPIKE_HALF    = 0.023
$SPIKE_LEAN    = 8.0           # degrees, leaning TOWARD the head
$SPIKE2_LEN_F  = 0.150         # the second, shorter one
$SPIKE2_HALF   = 0.017
$SPIKE2_LEAN   = -14.0         # splayed the other way, AWAY from the head

$RING_R      = 0.029           # ring pommel radius
$RING_W      = 0.0085          # its stroke width

function RGB($r, $g, $b, $a = 255) {
  # Clamp at the ONE place colours are constructed - alpha is multiplied downstream in
  # several independent places and any of them can push a value back over 255.
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$r))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$g))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$b))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$a))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}
function Lerp3($a, $b, [double]$t) {
  if ($t -lt 0) { $t = 0 }
  if ($t -gt 1) { $t = 1 }
  return @(
    ([int]($a[0] + ($b[0]-$a[0])*$t)),
    ([int]($a[1] + ($b[1]-$a[1])*$t)),
    ([int]($a[2] + ($b[2]-$a[2])*$t))
  )
}

# --- the curved haft centreline: a cubic Bezier, second control point pushed far more
# than the first, so the bend lands near the head and the grip stays straight ---
function BezPoint([double]$t, $b, $c1, $c2, $h) {
  $mt = 1.0 - $t
  $w0 = $mt*$mt*$mt; $w1 = 3.0*$mt*$mt*$t; $w2 = 3.0*$mt*$t*$t; $w3 = $t*$t*$t
  return @(
    (($b[0]*$w0) + ($c1[0]*$w1) + ($c2[0]*$w2) + ($h[0]*$w3)),
    (($b[1]*$w0) + ($c1[1]*$w1) + ($c2[1]*$w2) + ($h[1]*$w3))
  )
}
function BezTangent([double]$t, $b, $c1, $c2, $h) {
  $mt = 1.0 - $t
  $w0 = 3.0*$mt*$mt; $w1 = 6.0*$mt*$t; $w2 = 3.0*$t*$t
  $tx = (($c1[0]-$b[0])*$w0) + (($c2[0]-$c1[0])*$w1) + (($h[0]-$c2[0])*$w2)
  $ty = (($c1[1]-$b[1])*$w0) + (($c2[1]-$c1[1])*$w1) + (($h[1]-$c2[1])*$w2)
  $m = [Math]::Sqrt(($tx*$tx) + ($ty*$ty))
  if ($m -lt 1e-9) { return @(1.0, 0.0) }
  return @(($tx/$m), ($ty/$m))
}

function BuildAxe([double]$bow) {

  $bmp = New-Object System.Drawing.Bitmap $CANVAS, $CANVAS, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gg = [System.Drawing.Graphics]::FromImage($bmp)
  $gg.Clear((RGB 0 0 0 0))
  $gg.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $gg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

  function LPT([double]$fx, [double]$fy) {
    return (New-Object System.Drawing.PointF ([single]($fx*$CANVAS)), ([single]($fy*$CANVAS)))
  }

  $chordX = $HEAD_X - $BUTT_X
  $chordY = $HEAD_Y - $BUTT_Y
  $chordLen = [Math]::Sqrt(($chordX*$chordX) + ($chordY*$chordY))
  $cux = $chordX/$chordLen; $cuy = $chordY/$chordLen
  $cnx = -$cuy;             $cny = $cux

  $BLADE_OUT   = $BLADE_OUT_F   * $chordLen
  $BLADE_ALONG = $BLADE_ALONG_F * $chordLen
  $SPIKE_LEN   = $SPIKE_LEN_F   * $chordLen
  $SPIKE2_LEN  = $SPIKE2_LEN_F  * $chordLen

  $butt = @( ($BUTT_X), ($BUTT_Y) )
  $head = @( ($HEAD_X), ($HEAD_Y) )
  $ctl1 = @( (($BUTT_X + $chordX*0.333) + ($cnx*$bow*0.15)), (($BUTT_Y + $chordY*0.333) + ($cny*$bow*0.15)) )
  $ctl2 = @( (($BUTT_X + $chordX*0.667) + ($cnx*$bow*1.00)), (($BUTT_Y + $chordY*0.667) + ($cny*$bow*1.00)) )

  # --- haft: dark keyline pass, then the gradient pass, both following the curve ---
  foreach ($pass in @(0, 1)) {
    for ($i = 0; $i -lt $BANDS; $i++) {
      $t0 = $i / [double]$BANDS
      $t1 = ($i + 1) / [double]$BANDS
      $p0 = BezPoint $t0 $butt $ctl1 $ctl2 $head
      $p1 = BezPoint $t1 $butt $ctl1 $ctl2 $head
      $g0 = BezTangent $t0 $butt $ctl1 $ctl2 $head
      $g1 = BezTangent $t1 $butt $ctl1 $ctl2 $head
      $n0x = -$g0[1]; $n0y = $g0[0]
      $n1x = -$g1[1]; $n1y = $g1[0]
      $w0 = $HAFT_W_BUTT + (($HAFT_W_HEAD - $HAFT_W_BUTT) * $t0)
      $w1 = $HAFT_W_BUTT + (($HAFT_W_HEAD - $HAFT_W_BUTT) * $t1)
      if ($pass -eq 0) { $w0 += $OUTLINE; $w1 += $OUTLINE }
      $quad = @(
        (LPT ($p0[0] + $n0x*$w0) ($p0[1] + $n0y*$w0)),
        (LPT ($p1[0] + $n1x*$w1) ($p1[1] + $n1y*$w1)),
        (LPT ($p1[0] - $n1x*$w1) ($p1[1] - $n1y*$w1)),
        (LPT ($p0[0] - $n0x*$w0) ($p0[1] - $n0y*$w0))
      )
      if ($pass -eq 0) {
        $br = New-Object System.Drawing.SolidBrush (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 240)
      } else {
        $col = Lerp3 $C_EMBER $C_BLUE_LIT (($t0 + $t1) * 0.5)
        $br = New-Object System.Drawing.SolidBrush (RGB $col[0] $col[1] $col[2] 232)
      }
      $gg.FillPolygon($br, [System.Drawing.PointF[]]$quad)
      $br.Dispose()
    }
  }

  # --- RING POMMEL, stroke-only. A filled disc reads as a blob on the end of a stick. ---
  $bt = BezTangent 0.0 $butt $ctl1 $ctl2 $head
  $ringCx = $BUTT_X - ($bt[0] * $RING_R * 0.72)
  $ringCy = $BUTT_Y - ($bt[1] * $RING_R * 0.72)
  $ringRect = New-Object System.Drawing.RectangleF `
    ([single](($ringCx - $RING_R) * $CANVAS)), ([single](($ringCy - $RING_R) * $CANVAS)), `
    ([single]($RING_R * 2.0 * $CANVAS)), ([single]($RING_R * 2.0 * $CANVAS))
  $penRingLine = New-Object System.Drawing.Pen (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 240), ([single](($RING_W + 0.0055) * $CANVAS))
  $gg.DrawEllipse($penRingLine, $ringRect); $penRingLine.Dispose()
  $penRing = New-Object System.Drawing.Pen (RGB $C_EMBER[0] $C_EMBER[1] $C_EMBER[2] 238), ([single]($RING_W * $CANVAS))
  $gg.DrawEllipse($penRing, $ringRect); $penRing.Dispose()

  # --- two GRIP BANDS on the lower haft, as the reference has ---
  foreach ($bandT in @(0.19, 0.34)) {
    $bp = BezPoint $bandT $butt $ctl1 $ctl2 $head
    $bg = BezTangent $bandT $butt $ctl1 $ctl2 $head
    $bnx = -$bg[1]; $bny = $bg[0]
    $bw = ($HAFT_W_BUTT + (($HAFT_W_HEAD - $HAFT_W_BUTT) * $bandT)) * 1.66
    $penBandLine = New-Object System.Drawing.Pen (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 240), ([single](0.0165 * $CANVAS))
    $gg.DrawLine($penBandLine, (LPT ($bp[0] + $bnx*$bw) ($bp[1] + $bny*$bw)), `
                               (LPT ($bp[0] - $bnx*$bw) ($bp[1] - $bny*$bw)))
    $penBandLine.Dispose()
    $penBand = New-Object System.Drawing.Pen (RGB $C_GOLD[0] $C_GOLD[1] $C_GOLD[2] 235), ([single](0.0092 * $CANVAS))
    $gg.DrawLine($penBand, (LPT ($bp[0] + $bnx*$bw) ($bp[1] + $bny*$bw)), `
                           (LPT ($bp[0] - $bnx*$bw) ($bp[1] - $bny*$bw)))
    $penBand.Dispose()
  }

  # --- the head, on the haft's tangent AT the head so it rotates with the curve ---
  $ht = BezTangent 1.0 $butt $ctl1 $ctl2 $head
  $hux = $ht[0]; $huy = $ht[1]
  $hnx = -$huy;  $hny = $hux

  $anchorX = $HEAD_X + ($hux * 0.020)
  $anchorY = $HEAD_Y + ($huy * 0.020)

  # Publish the blade's coordinate frame. This is what lets a mask painted over a RENDER be
  # converted back into (outward, along) numbers that can be pasted straight into $BLADE -
  # without a second copy of this arithmetic living in the extraction script and drifting.
  $script:BLADE_BASIS = @{
    AnchorX = $anchorX; AnchorY = $anchorY
    Hux = $hux; Huy = $huy; Hnx = $hnx; Hny = $hny
    Out = $BLADE_OUT; Along = $BLADE_ALONG
  }

  # THE DRAGONBONE FAN BLADE, with its HOLE.
  #
  # Points are (outward, along), outward in units of $BLADE_OUT away from the haft on the
  # blade side, along in units of $BLADE_ALONG positive toward the head. Traced off the
  # second reference: an upper-right horn, a stepped and notched outer edge, a lower-right
  # point, then a long BEARD sweeping down and back toward the haft, and a concave scoop.
  #
  # The blade is TALLER than wide - 0.505 of weapon length along against 0.29 outward -
  # and hangs well below the socket. Getting that backwards is what made the first attempt
  # read as a flag.
  # THE BASE THE USER PAINTED OVER. Restored deliberately and left here: the extraction
  # script rasterises THIS polygon and subtracts their white mask, so if it is edited the
  # measurement silently changes meaning. It did exactly that once - a replacement blade was
  # dropped in first, the extractor dot-sourced the generator, and it measured the new shape
  # instead of the one that was painted (2262px of blade instead of 4659). The traced result
  # goes in BELOW, not here.
  $BLADE = @(
    @( (0.10),  (0.40) ),
    @( (0.70),  (0.46) ),   # upper-right horn
    @( (0.80),  (0.26) ),   # step in
    @( (1.00),  (0.10) ),   # widest point
    @( (0.94), (-0.16) ),   # step
    @( (0.78), (-0.26) ),   # notch in
    @( (0.88), (-0.50) ),   # lower-right point
    @( (0.62), (-0.66) ),
    @( (0.30), (-1.06) ),   # long beard tip
    @( (0.20), (-0.62) ),   # concave scoop back
    @( (0.08), (-0.30) )
  )
  if ($TRACED_BLADE -and $TRACED_BLADE.Count -ge 3) { $BLADE = $TRACED_BLADE }
  # The HOLE. Centred about (0.51, -0.04) in the same units - measured at (130, 140) from
  # the socket, about 150px across on a 1277px weapon. Punched as a second sub-path with
  # FillMode Alternate, which also gets its dark rim stroked for free.
  # SMALLER than the first pass and pushed up toward the socket. At 0.36 across and centred
  # it left only a thin C of blade around it and the whole head read as a hook rather than a
  # fan. Re-measured off the reference: the hole spans about 110px on a 1277px weapon, i.e.
  # 0.086 of length = 0.30 in outward units, and it sits in the UPPER part of the blade with
  # the blade's mass below and outboard of it.
  $BLADE_HOLE = @(
    @( (0.32),  (0.18) ),
    @( (0.46),  (0.22) ),
    @( (0.56),  (0.11) ),
    @( (0.52), (-0.02) ),
    @( (0.38), (-0.04) ),
    @( (0.28),  (0.06) )
  )

  $bladePath = New-Object System.Drawing.Drawing2D.GraphicsPath
  $bladePath.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
  $outerPts = @()
  # Record every vertex in FRACTIONAL frame coords as we go, so the preview sheet can label
  # them. Recomputing the transform outside this function would be a second copy of the same
  # arithmetic and would silently drift the moment one of them was edited.
  $script:BLADE_VERTS = @()
  $script:HOLE_VERTS = @()
  foreach ($q in $BLADE) {
    $vx = $anchorX + ($hnx*$BLADE_OUT*$q[0]) + ($hux*$BLADE_ALONG*$q[1])
    $vy = $anchorY + ($hny*$BLADE_OUT*$q[0]) + ($huy*$BLADE_ALONG*$q[1])
    $script:BLADE_VERTS += ,@(($vx), ($vy))
    $outerPts += (LPT $vx $vy)
  }
  $bladePath.AddPolygon([System.Drawing.PointF[]]$outerPts)   # AddPolygon, never
                                                              # AddClosedCurve - any curve
                                                              # tension rounds the facets
  # THE HOLE IS OFF. It is in the reference, but on a blade this size it was one more
  # competing feature inside 40 pixels and the user's mask painted straight over it. Kept
  # behind a switch rather than deleted, so it does not have to be re-derived if wanted.
  if ($USE_HOLE) {
    $holePts = @()
    foreach ($q in $BLADE_HOLE) {
      $vx = $anchorX + ($hnx*$BLADE_OUT*$q[0]) + ($hux*$BLADE_ALONG*$q[1])
      $vy = $anchorY + ($hny*$BLADE_OUT*$q[0]) + ($huy*$BLADE_ALONG*$q[1])
      $script:HOLE_VERTS += ,@(($vx), ($vy))
      $holePts += (LPT $vx $vy)
    }
    $bladePath.AddPolygon([System.Drawing.PointF[]]$holePts)
  }

  # keyline UNDER the fill, width proportional to the shape - a fixed width stops being an
  # outline and becomes the shape once the piece shrinks
  $keyW = [Math]::Max([double](2.0*$SS), [double]($BLADE_OUT * $CANVAS * 0.030))
  $penKey = New-Object System.Drawing.Pen (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 245), ([single]$keyW)
  $penKey.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $penKey.MiterLimit = [single]6.0
  $gg.DrawPath($penKey, $bladePath); $penKey.Dispose()

  $bRect = $bladePath.GetBounds()
  if ($bRect.Width -lt 1) { $bRect.Width = 1 }
  if ($bRect.Height -lt 1) { $bRect.Height = 1 }
  $brBlade = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bRect, `
    (RGB $C_BLUE_MID[0] $C_BLUE_MID[1] $C_BLUE_MID[2] 228), `
    (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] 243), ([single]45.0)
  $gg.FillPath($brBlade, $bladePath); $brBlade.Dispose()
  $penEdge = New-Object System.Drawing.Pen (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] 250), ([single](1.8*$SS))
  $penEdge.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $gg.DrawPath($penEdge, $bladePath); $penEdge.Dispose()
  $bladePath.Dispose()

  # THE TWO SPIKES, perpendicular-ish to the haft on the side OPPOSITE the blade. See
  # note 2 at the top: measured direction decomposes as 0.994 of minus-the-normal plus
  # 0.124 of the haft axis. They are splayed by ANGLE, not distinguished by size alone -
  # the shoulder fins taught that three shapes at one angle render as one shape.
  function SpikePoly([double]$leanDeg, [double]$slen, [double]$shalf) {
    $rad = $leanDeg * [Math]::PI / 180.0
    # -normal rotated toward (+) or away from (-) the head
    $dx = (-$hnx * [Math]::Cos($rad)) + ($hux * [Math]::Sin($rad))
    $dy = (-$hny * [Math]::Cos($rad)) + ($huy * [Math]::Sin($rad))
    # side vector, perpendicular to the spike itself
    $sx = -$dy; $sy = $dx
    $bx = $anchorX - ($dx * $slen * 0.10)
    $by = $anchorY - ($dy * $slen * 0.10)
    $mx = $bx + ($dx * $slen * 0.44)
    $my = $by + ($dy * $slen * 0.44)
    # five points: fat root, a waist, then a long fine point. A plain triangle tapers dead
    # straight and reads as a wedge.
    return @(
      (LPT ($bx + $sx*$shalf)        ($by + $sy*$shalf)),
      (LPT ($mx + $sx*$shalf*0.50)   ($my + $sy*$shalf*0.50)),
      (LPT ($bx + $dx*$slen)         ($by + $dy*$slen)),
      (LPT ($mx - $sx*$shalf*0.46)   ($my - $sy*$shalf*0.46)),
      (LPT ($bx - $sx*$shalf)        ($by - $sy*$shalf))
    )
  }
  # shorter one first, so the long one overlaps it rather than the other way round
  foreach ($sp in @((SpikePoly $SPIKE2_LEAN $SPIKE2_LEN $SPIKE2_HALF),
                    (SpikePoly $SPIKE_LEAN  $SPIKE_LEN  $SPIKE_HALF))) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]$sp)
    $penL = New-Object System.Drawing.Pen (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 245), ([single](4.4*$SS))
    $penL.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $penL.MiterLimit = [single]6.0
    $gg.DrawPath($penL, $path); $penL.Dispose()
    $rc = $path.GetBounds()
    if ($rc.Width -lt 1) { $rc.Width = 1 }
    if ($rc.Height -lt 1) { $rc.Height = 1 }
    $brS = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rc, `
      (RGB $C_BLUE_MID[0] $C_BLUE_MID[1] $C_BLUE_MID[2] 230), `
      (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] 244), ([single]45.0)
    $gg.FillPath($brS, $path); $brS.Dispose()
    $penE = New-Object System.Drawing.Pen (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] 250), ([single](1.6*$SS))
    $penE.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
    $gg.DrawPath($penE, $path); $penE.Dispose()
    $path.Dispose()
  }

  # THE RIVETED SOCKET, drawn LAST so it covers the joins between haft, blade and spikes.
  # In the reference this is a prominent dark plate assembly and it is what makes the head
  # read as MOUNTED rather than fused to the pole.
  # MUCH smaller than the first pass, which at 0.105 x 0.130 was a dark hexagon that
  # swallowed the blade root, both spike bases and the beard. It is a COLLAR, not a plate:
  # its job is to hide the three joins, and anything beyond that is a black blob at the
  # centre of the weapon.
  $SOCK_OUT = 0.055
  $SOCK_ALONG = 0.072
  $SOCKET = @(
    @( (0.60),  (0.40) ),
    @( (0.82), (-0.20) ),
    @( (0.30), (-0.74) ),
    @( (-0.40),(-0.58) ),
    @( (-0.60), (0.26) ),
    @( (0.00),  (0.66) )
  )
  $sockPts = @()
  foreach ($q in $SOCKET) {
    $sockPts += (LPT ($anchorX + ($hnx*$SOCK_OUT*$q[0]) + ($hux*$SOCK_ALONG*$q[1])) `
                     ($anchorY + ($hny*$SOCK_OUT*$q[0]) + ($huy*$SOCK_ALONG*$q[1])))
  }
  $sockPath = New-Object System.Drawing.Drawing2D.GraphicsPath
  $sockPath.AddPolygon([System.Drawing.PointF[]]$sockPts)
  $brSock = New-Object System.Drawing.SolidBrush (RGB 32 42 60 246)
  $gg.FillPath($brSock, $sockPath); $brSock.Dispose()
  $penSock = New-Object System.Drawing.Pen (RGB $C_HOT[0] $C_HOT[1] $C_HOT[2] 214), ([single](1.7*$SS))
  $penSock.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $gg.DrawPath($penSock, $sockPath); $penSock.Dispose()
  $sockPath.Dispose()

  # rivets. Gone by 48px, but most of what says "riveted collar" at the sizes it is looked at.
  foreach ($rv in @(@((0.34),(0.24)), @((-0.28),(0.18)), @((0.40),(-0.22)), @((-0.16),(-0.36)))) {
    $rx = $anchorX + ($hnx*$SOCK_OUT*$rv[0]) + ($hux*$SOCK_ALONG*$rv[1])
    $ry = $anchorY + ($hny*$SOCK_OUT*$rv[0]) + ($huy*$SOCK_ALONG*$rv[1])
    $rr = 0.0058
    $brRv = New-Object System.Drawing.SolidBrush (RGB $C_HOT[0] $C_HOT[1] $C_HOT[2] 228)
    $gg.FillEllipse($brRv, [single](($rx-$rr)*$CANVAS), [single](($ry-$rr)*$CANVAS), `
                           [single]($rr*2.0*$CANVAS), [single]($rr*2.0*$CANVAS))
    $brRv.Dispose()
  }

  $gg.Dispose()

  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gf = [System.Drawing.Graphics]::FromImage($final)
  $gf.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gf.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gf.Clear((RGB 0 0 0 0))
  $gf.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE))
  $gf.Dispose(); $bmp.Dispose()
  return $final
}

# The quadrant check SPECTRAL_HALBERD_PRESET.md says silently invalidates the whole Melee
# Animation tweak file if the art is redrawn. Threshold 8, split at 128 - recorded so it
# reproduces, which is the thing the old table was missing.
function QuadOf($bmp) {
  $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object 'byte[]' ($data.Stride * $bmp.Height)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $bmp.UnlockBits($data)
  $tl=0; $tr=0; $bl=0; $br=0
  $half = [int]($bmp.Width / 2)
  for ($y = 0; $y -lt $bmp.Height; $y++) {
    $row = $y * $data.Stride
    for ($x = 0; $x -lt $bmp.Width; $x++) {
      if ($bytes[$row + $x*4 + 3] -gt 8) {
        if ($y -lt $half) { if ($x -lt $half) { $tl++ } else { $tr++ } }
        else { if ($x -lt $half) { $bl++ } else { $br++ } }
      }
    }
  }
  return @($tl, $tr, $bl, $br)
}

$built = @()
$built += ,@((BuildAxe $BOW),          "as measured (bow toward blade)", "matches the reference")
$built += ,@((BuildAxe (-1.0*$BOW)),   "bow the other way",             "for comparison")
$built += ,@((BuildAxe 0.0),           "no bow, straight haft",         "for comparison")

$CW = 300
$PADX = 24
$sheetW = ($PADX * 4) + ($CW * 3)
$sheetH = 500
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gs = [System.Drawing.Graphics]::FromImage($sheet)
$gs.Clear((RGB 28 30 28 255))
$gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontB = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontS = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Bold)
$fontT = New-Object System.Drawing.Font "Segoe UI", 9.5
$brW = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brG = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brO = New-Object System.Drawing.SolidBrush (RGB 236 156 96 255)
$gs.DrawString("DRAGONBONE BATTLEAXE - measured off your second reference", $fontB, $brW, [single]$PADX, [single]12)
$gs.DrawString("spikes now perpendicular on the far side from the blade, hole punched through it, haft bowing toward the blade", $fontT, $brG, [single]$PADX, [single]36)

function Ground($gfx, [double]$x, [double]$y, [double]$size, [int]$salt) {
  $tile = 12
  for ($gy = 0; $gy -lt $size; $gy += $tile) {
    for ($gx = 0; $gx -lt $size; $gx += $tile) {
      $hv = [Math]::Sin((($gx+1)*12.9898) + (($gy+1)*78.233) + ($salt*37.719)) * 43758.5453
      $hv = $hv - [Math]::Floor($hv)
      $dv = [int](($hv - 0.5) * 42.0)
      $bru = New-Object System.Drawing.SolidBrush (RGB (122+$dv) (106+$dv) (84+[int]($dv*0.8)) 255)
      $gfx.FillRectangle($bru, [single]($x+$gx), [single]($y+$gy), [single]$tile, [single]$tile)
      $bru.Dispose()
    }
  }
}

$col = 0
$report = @()
$letters = @("A", "B", "C")
foreach ($b in $built) {
  $img = $b[0]; $label = $b[1]; $note = $b[2]
  $x = $PADX + ($col * ($CW + $PADX))
  $y = 58
  Ground $gs $x $y $CW (7 + $col)
  $gs.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$x), ([int]$y), $CW, $CW))
  $brLbl = if ($col -eq 0) { $brO } else { $brG }
  $gs.DrawString(($letters[$col] + ". " + $label), $fontS, $brLbl, [single]$x, [single]($y + $CW + 6))
  $gs.DrawString($note, $fontT, $brG, [single]$x, [single]($y + $CW + 23))
  Ground $gs $x ($y + $CW + 42) 96 (40 + $col)
  $gs.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$x), ([int]($y + $CW + 42)), 96, 96))
  Ground $gs ($x + 104) ($y + $CW + 42) 48 (50 + $col)
  $gs.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]($x + 104)), ([int]($y + $CW + 42)), 48, 48))
  $gs.DrawString("96px (in hand)      48px", $fontT, $brG, [single]$x, [single]($y + $CW + 142))
  $q = QuadOf $img
  $report += ("{0} {1,-32} TL {2,5} TR {3,5} BL {4,5} BR {5,5}  head-top-right: {6}" -f `
    $letters[$col], $label, $q[0], $q[1], $q[2], $q[3], `
    $(if ($q[1] -gt $q[0] -and $q[1] -gt $q[3]) { "YES" } else { "*** NO ***" }))
  $col++
}
$gs.Dispose()

$prev = Join-Path $OUT_DIR "ancient_axe_dragonbone.png"
$sheet.Save($prev, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

# candidates written to the PREVIEW folder so PreviewAncientDragonborn.ps1 can be pointed at
# one with DOVAH_AXE_OVERRIDE and show it held, without writing into the mod
for ($k = 0; $k -lt $built.Count; $k++) {
  $cand = Join-Path $OUT_DIR ("axe_candidate_" + $letters[$k] + ".png")
  $built[$k][0].Save($cand, [System.Drawing.Imaging.ImageFormat]::Png)
}

if ($WRITE_TEXTURE) {
  $outPath = Join-Path $DEST_DIR "DovahkiinAncientAxe.png"
  $built[0][0].Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("WROTE SHIPPING TEXTURE " + $outPath)
} else {
  Write-Output "preview only - shipping texture NOT touched (`$WRITE_TEXTURE is false)"
}
foreach ($b in $built) { $b[0].Dispose() }
foreach ($line in $report) { Write-Output $line }
Write-Output ("wrote " + $prev)
Write-Output "DONE"
