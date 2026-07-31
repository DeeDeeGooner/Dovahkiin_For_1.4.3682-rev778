# =====================================================================================
#  CALL OF VALOR - the hero of Sovngarde's SPECTRAL ARMOUR.
#
#  Spec from the user 2026-07-31, and the governing line is: HE IS NOT A SECOND ANCIENT
#  DRAGONBORN. A first pass reused Dragon Aspect's geometry with a spectral palette and was
#  rejected on exactly that ground - he looked like kin to the Dovahkiin.
#
#  So this is its own geometry:
#    - NORMAL ARMOUR in shape. The reference is Skyrim's Nord warrior kit: horned helm,
#      fur-trimmed pauldrons, a banded plate cuirass, a belted fur-and-leather skirt,
#      bracers, greaves. Plates and straps.
#    - NO spurs, NO fins, NO chest crest. Those are the Dovahkiin's.
#    - NO AURA. No ring, no crescents. Also his.
#    - the PALETTE stays spectral - he is still a ghost, just not that ghost.
#
#  PREVIEW ONLY. Nothing is written into the mod until the shape is approved.
#
#  ---------------------------------------------------------------------------------
#  WHY THIS MEASURES THE BODY SPRITE INSTEAD OF USING FIXED NUMBERS
#  ---------------------------------------------------------------------------------
#  Hard-won here and worth restating: a pawn overlay traced from ONE body type fits nobody
#  else, because the five silhouettes are different SHAPES rather than different sizes -
#  Male is widest at the shoulders and tapers, Female pinches to a 60px waist and is widest
#  at the hips, Thin is a straight 52px tube. So every landmark below is a fraction of the
#  body's OWN measured outline.
#
#  Two traps that cost a playtest round each, both honoured here:
#    - store (y, left, right) and never mirror one half-width. Front views are near
#      symmetric so mirroring looks fine there and is WRONG on every side view: Hulk's east
#      sprite is 69.5px left of centre and 11.5px right, so mirroring hangs 58px of armour
#      off the front of the body.
#    - centre and vertical extent are PER ROTATION. Side sprites are not centred on 127.5
#      (Female east sits on x=113) and are not the same height as the front.
#
#  PowerShell traps honoured: no single-letter variables, numeric array elements
#  parenthesised, [double] on Math::Max, no C-style casts.
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$WRITE_TEXTURE = $false

$BODY_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\B.B\Textures\Things\Pawn\Humanlike\Bodies"
$HEAD_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Gloomy Face Mod\Textures\Things\Pawn\Humanlike\Heads\Male"
$HEAD_KIND = "Male_Average_Pointy"
$OUT_DIR = $env:DOVAH_PREVIEW
if (-not $OUT_DIR) { $OUT_DIR = $PSScriptRoot }

$SIZE = 256
$SS   = 3
$CANVAS = $SIZE * $SS

# --- spectral palette, shared with the greatsword so weapon and wearer read as one -------
$C_RIM   = @(255, 255, 255)
$C_LIT   = @(230, 244, 255)
$C_MID   = @(176, 210, 238)
$C_DEEP  = @( 34,  58,  84)
$C_BLOOM = @(168, 216, 255)
$C_FUR   = @(150, 190, 224)   # the fur trim reads as a softer, dimmer band

$PLATE_ALPHA = 128            # translucent: his own body still shows through
$RIM_ALPHA   = 236
$FUR_ALPHA   = 108

function RGB($red, $green, $blue, $alpha = 255) {
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$red))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$green))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$blue))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$alpha))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}

# ---------------------------------------------------------------------------------
# Measure one body sprite: per row, the leftmost and rightmost opaque pixel. Kept as two
# separate edges - see the header for why mirroring is wrong.
# ---------------------------------------------------------------------------------
function MeasureBody([string]$path) {
  $bmp = New-Object System.Drawing.Bitmap $path
  $wide = $bmp.Width; $high = $bmp.Height
  $data = $bmp.LockBits((New-Object System.Drawing.Rectangle 0,0,$wide,$high),
          [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object 'byte[]' ($data.Stride * $high)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $stride = $data.Stride
  $bmp.UnlockBits($data); $bmp.Dispose()

  $rows = @{}
  $topY = -1; $botY = -1
  for ($yy = 0; $yy -lt $high; $yy++) {
    $left = -1; $right = -1
    for ($xx = 0; $xx -lt $wide; $xx++) {
      if ($bytes[$yy*$stride + $xx*4 + 3] -gt 8) {
        if ($left -lt 0) { $left = $xx }
        $right = $xx
      }
    }
    if ($left -ge 0) {
      $rows[$yy] = @($left, $right)
      if ($topY -lt 0) { $topY = $yy }
      $botY = $yy
    }
  }
  return @{ Rows = $rows; Top = $topY; Bottom = $botY }
}

# left/right edge at a fraction of the body's own vertical extent
function EdgeAt($prof, [double]$frac) {
  $yy = [int][Math]::Round($prof.Top + (($prof.Bottom - $prof.Top) * $frac))
  if ($yy -lt $prof.Top) { $yy = $prof.Top }
  if ($yy -gt $prof.Bottom) { $yy = $prof.Bottom }
  # walk outward for the nearest row that has ink, so a gap cannot return nothing
  for ($step = 0; $step -lt 24; $step++) {
    foreach ($probe in @(($yy + $step), ($yy - $step))) {
      if ($prof.Rows.ContainsKey($probe)) {
        return @(($prof.Rows[$probe][0]), ($prof.Rows[$probe][1]), $yy)
      }
    }
  }
  return @(100, 156, $yy)
}

# =====================================================================================
#  THE PLATE PROFILE - measured off Medieval Overhaul's worn full-plate cuirass.
# =====================================================================================
#  Their PNG is not copied and is not shipped. These RATIOS are, which is the same
#  standard the halberd and greatsword were held to: measure their proportions, draw our
#  own art to them.
#
#  Each entry is (fraction down the body's own height, plate half-width AS A MULTIPLE of
#  the body's half-width at that height). Expressing it as a multiple rather than in pixels
#  is what makes it fit all five body silhouettes - the armour follows each body's own
#  taper instead of imposing a male one.
#
#  THE THING THREE FAILED PASSES GOT WRONG: real worn armour is WIDER THAN THE BODY almost
#  everywhere - theirs projects 6 to 29px per side and peaks at 1.57x the body's half-width
#  at the shoulders. Mine were inset INSIDE the silhouette, which is why they could only
#  ever read as stripes painted on a torso rather than as plate worn over one.
#
#  Read off their south sprite (body y 88..214, so height 126):
#     y  90 -> 0.016 down, ratio 2.10     the gorget, where the body is only a neck
#     y 105 -> 0.135 down, ratio 1.39
#     y 129 -> 0.325 down, ratio 1.57     PAULDRONS, the widest point
#     y 150 -> 0.492 down, ratio 1.38
#     y 156 -> 0.540 down, ratio 1.13     the waist, where the plate hugs
#     y 174 -> 0.683 down, ratio 1.16
#     y 189 -> 0.802 down, ratio 1.32     TASSETS flaring again
#     y 201 -> 0.897 down, ratio 1.39
#  and it runs from 0.03 ABOVE the body's top to 0.06 below its bottom.
# PER ROTATION, because they are genuinely different shapes. From the side the plate is
# NARROWER than the body's side profile (ratios below 1.0 through the torso), while from the
# front and back it is wider everywhere. Reusing the south numbers on east is exactly the
# mistake that once hung 58px of armour off the front of a Hulk.
$PLATE_PROFILE = @{
  south = @(
    @( (-0.008), (1.15) ), @( (0.024), (1.63) ), @( (0.056), (1.47) ), @( (0.087), (1.39) ),
    @( (0.119), (1.35) ), @( (0.183), (1.36) ), @( (0.246), (1.36) ), @( (0.310), (1.30) ),
    @( (0.373), (1.20) ), @( (0.437), (1.02) ), @( (0.500), (1.04) ), @( (0.563), (1.02) ),
    @( (0.627), (1.05) ), @( (0.690), (1.18) ), @( (0.754), (1.22) ), @( (0.817), (1.27) ),
    @( (0.881), (1.37) ), @( (0.944), (1.35) ), @( (1.016), (1.30) )
  )
  north = @(
    @( (-0.008), (1.18) ), @( (0.024), (1.69) ), @( (0.056), (1.51) ), @( (0.087), (1.42) ),
    @( (0.119), (1.41) ), @( (0.183), (1.42) ), @( (0.246), (1.38) ), @( (0.310), (1.32) ),
    @( (0.373), (1.23) ), @( (0.437), (1.06) ), @( (0.500), (1.07) ), @( (0.563), (1.07) ),
    @( (0.627), (1.08) ), @( (0.690), (1.21) ), @( (0.754), (1.27) ), @( (0.817), (1.34) ),
    @( (0.881), (1.41) ), @( (0.944), (1.39) ), @( (1.016), (1.34) )
  )
  east = @(
    @( (-0.008), (0.95) ), @( (0.024), (1.11) ), @( (0.056), (0.92) ), @( (0.087), (0.84) ),
    @( (0.119), (0.79) ), @( (0.183), (0.78) ), @( (0.246), (0.86) ), @( (0.310), (1.02) ),
    @( (0.373), (1.00) ), @( (0.437), (0.96) ), @( (0.500), (1.00) ), @( (0.563), (1.00) ),
    @( (0.627), (1.02) ), @( (0.690), (1.10) ), @( (0.754), (1.18) ), @( (0.817), (1.27) ),
    @( (0.881), (1.37) ), @( (0.944), (1.61) ), @( (1.040), (1.70) )
  )
}

function BuildArmour([string]$rot) {
  $prof = MeasureBody (Join-Path $BODY_DIR "Naked_Male_$rot.png")
  $bmp = New-Object System.Drawing.Bitmap $CANVAS, $CANVAS, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $gfx.Clear((RGB 0 0 0 0))
  $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

  function SPT([double]$sx, [double]$sy) {
    return (New-Object System.Drawing.PointF ([single]($sx*$SS)), ([single]($sy*$SS)))
  }

  # every plate: soft bloom, translucent body, luminous rim. No specular, no bevel - the
  # same emissive treatment the greatsword uses, so wearer and weapon read as one thing.
  function Plate($pts, $fillCol, [int]$fillAlpha, [double]$rimPx) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]$pts)
    foreach ($glowSpec in @(@((17.0), (26)), @((10.0), (40)), @((5.0), (56)))) {
      $penBloom = New-Object System.Drawing.Pen (RGB $C_BLOOM[0] $C_BLOOM[1] $C_BLOOM[2] $glowSpec[1]), ([single]($glowSpec[0]*$SS))
      $penBloom.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
      $gfx.DrawPath($penBloom, $path); $penBloom.Dispose()
    }
    $brush = New-Object System.Drawing.SolidBrush (RGB $fillCol[0] $fillCol[1] $fillCol[2] $fillAlpha)
    $gfx.FillPath($brush, $path); $brush.Dispose()
    if ($rimPx -gt 0) {
      $penRim = New-Object System.Drawing.Pen (RGB $C_RIM[0] $C_RIM[1] $C_RIM[2] $RIM_ALPHA), ([single]($rimPx*$SS))
      $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
      $gfx.DrawPath($penRim, $path); $penRim.Dispose()
    }
    $path.Dispose()
  }

  # A horizontal band across the body at a vertical fraction - the cuirass lames, the belt,
  # the greave straps.
  #
  # $shrink is a FRACTION OF THE BODY'S OWN HALF-WIDTH at that height, not a pixel count.
  # That matters twice over. First it scales across body types, where a fixed inset would be
  # most of a Thin pawn's 52px and nothing on a Fat pawn's 162px. Second, and this is what
  # went wrong on the first pass: at chest height the silhouette INCLUDES THE ARMS, so a band
  # drawn edge-to-edge is as wide as the whole pawn and reads as a slab. A cuirass covers the
  # torso; the arms get bracers. Roughly 0.30 of the half-width per side is the arm.
  function Band([double]$frac, [double]$height, [double]$shrink, $col, [int]$alpha, [double]$rimPx) {
    $edgeTop = EdgeAt $prof $frac
    $edgeBot = EdgeAt $prof ($frac + $height)
    $halfTop = ($edgeTop[1] - $edgeTop[0]) * 0.5
    $halfBot = ($edgeBot[1] - $edgeBot[0]) * 0.5
    $lt = $edgeTop[0] + ($halfTop * $shrink); $rt = $edgeTop[1] - ($halfTop * $shrink)
    $lb = $edgeBot[0] + ($halfBot * $shrink); $rb = $edgeBot[1] - ($halfBot * $shrink)
    if ($rt -le $lt -or $rb -le $lb) { return }
    Plate @( (SPT $lt $edgeTop[2]), (SPT $rt $edgeTop[2]), (SPT $rb $edgeBot[2]), (SPT $lb $edgeBot[2]) ) $col $alpha $rimPx
  }

  $isSide = ($rot -eq "east")

  # ---- ONE CONTINUOUS PLATE OUTLINE, from the measured profile ----
  # Centre is taken PER ROW from the body's own left/right, never assumed to be 127.5 - the
  # side sprites are not centred there (Female east sits on x=113) and a mirrored half-width
  # hangs armour off the front of the body on every side view.
  $left = @(); $right = @()
  $useProfile = if ($PLATE_PROFILE.ContainsKey($rot)) { $PLATE_PROFILE[$rot] } else { $PLATE_PROFILE["south"] }
  foreach ($node in $useProfile) {
    $edge = EdgeAt $prof ([Math]::Max([double]0.0, [Math]::Min([double]1.0, $node[0])))
    $bodyHalf = ($edge[1] - $edge[0]) * 0.5
    $centre   = ($edge[0] + $edge[1]) * 0.5
    # the profile can run past the body at both ends, so place those rows by extrapolating
    $yy = $prof.Top + (($prof.Bottom - $prof.Top) * $node[0])
    $half = $bodyHalf * $node[1]
    $left  += ,@(($centre - $half), $yy)
    $right += ,@(($centre + $half), $yy)
  }
  $outline = @()
  foreach ($pt in $left) { $outline += (SPT $pt[0] $pt[1]) }
  for ($idx = $right.Count - 1; $idx -ge 0; $idx--) { $outline += (SPT $right[$idx][0] $right[$idx][1]) }
  # FUR across the shoulders, under the plate - the reference''s most obvious soft element and
  # the one thing that stops a plate cuirass reading as sterile. Dimmer and wider than the
  # plate, with a ragged lower edge so it does not read as another band.
  $furTop = EdgeAt $prof 0.055
  $furBot = EdgeAt $prof 0.150
  $furHalfT = (($furTop[1] - $furTop[0]) * 0.5) * 1.44
  $furHalfB = (($furBot[1] - $furBot[0]) * 0.5) * 1.30
  $furCx = ($furTop[0] + $furTop[1]) * 0.5
  $furPts = @()
  $furPts += (SPT ($furCx - $furHalfT) $furTop[2])
  $furPts += (SPT ($furCx + $furHalfT) $furTop[2])
  for ($notch = 0; $notch -le 6; $notch++) {
    $ff = $notch / 6.0
    $jag = if (($notch % 2) -eq 0) { 0.0 } else { ($furBot[2] - $furTop[2]) * 0.22 }
    $furPts += (SPT ($furCx + $furHalfB - (2.0 * $furHalfB * $ff)) ($furBot[2] - $jag))
  }
  Plate $furPts $C_FUR 96 0.0

  Plate $outline $C_MID $PLATE_ALPHA 1.6

  # ---- internal detail. Lines only - at this size a filled sub-plate is another stripe. ----
  function DetailLine([double]$frac, [double]$shrink, $col, [int]$alpha, [double]$widthPx) {
    $edge = EdgeAt $prof $frac
    $bodyHalf = ($edge[1] - $edge[0]) * 0.5
    $centre   = ($edge[0] + $edge[1]) * 0.5
    $half = $bodyHalf * $shrink
    $pen = New-Object System.Drawing.Pen (RGB $col[0] $col[1] $col[2] $alpha), ([single]($widthPx*$SS))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $gfx.DrawLine($pen, (SPT ($centre - $half) $edge[2]), (SPT ($centre + $half) $edge[2]))
    $pen.Dispose()
  }
  DetailLine 0.075 1.05 $C_RIM 150 1.3    # gorget, under the chin
  DetailLine 0.535 1.10 $C_RIM 170 1.7    # the waist band - the plate's narrowest point
  DetailLine 0.700 1.14 $C_RIM 130 1.2    # top of the tassets

  # the central seam down the breastplate, and the split between the tassets
  if ($rot -ne "east") {
    $eA = EdgeAt $prof 0.100; $eB = EdgeAt $prof 0.520
    $penSeam = New-Object System.Drawing.Pen (RGB $C_RIM[0] $C_RIM[1] $C_RIM[2] 120), ([single](1.2*$SS))
    $gfx.DrawLine($penSeam, (SPT (($eA[0]+$eA[1])*0.5) $eA[2]), (SPT (($eB[0]+$eB[1])*0.5) $eB[2]))
    $eC = EdgeAt $prof 0.715; $eD = EdgeAt $prof 0.980
    $gfx.DrawLine($penSeam, (SPT (($eC[0]+$eC[1])*0.5) $eC[2]), (SPT (($eD[0]+$eD[1])*0.5) $eD[2]))
    $penSeam.Dispose()
  }
  $gfx.Dispose()
  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfin = [System.Drawing.Graphics]::FromImage($final)
  $gfin.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gfin.Clear((RGB 0 0 0 0))
  $gfin.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0,0,$SIZE,$SIZE))
  $gfin.Dispose(); $bmp.Dispose()
  return $final
}

# =====================================================================================
#  THE HORNED HELM
#
#  Sized against the HEAD, not the body. A head occupies about 60x74 inside a 192 head frame
#  while both quads are 1.5 world units, so head-worn art drawn on a body-sized 256 frame
#  must be about 80x100px or it comes out comically small - the first Dragon Aspect helm was
#  drawn at 62x76 AND at draw size 0.93 and stacked into less than half a head.
# =====================================================================================
function BuildHelm([string]$rot) {
  $bmp = New-Object System.Drawing.Bitmap $CANVAS, $CANVAS, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $gfx.Clear((RGB 0 0 0 0))
  $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

  function HPT([double]$sx, [double]$sy) {
    return (New-Object System.Drawing.PointF ([single]($sx*$SS)), ([single]($sy*$SS)))
  }
  function Piece($pts, $fillCol, [int]$fillAlpha, [double]$rimPx) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]$pts)
    foreach ($glowSpec in @(@((17.0), (26)), @((10.0), (40)), @((5.0), (56)))) {
      $penBloom = New-Object System.Drawing.Pen (RGB $C_BLOOM[0] $C_BLOOM[1] $C_BLOOM[2] $glowSpec[1]), ([single]($glowSpec[0]*$SS))
      $penBloom.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
      $gfx.DrawPath($penBloom, $path); $penBloom.Dispose()
    }
    $brush = New-Object System.Drawing.SolidBrush (RGB $fillCol[0] $fillCol[1] $fillCol[2] $fillAlpha)
    $gfx.FillPath($brush, $path); $brush.Dispose()
    $penRim = New-Object System.Drawing.Pen (RGB $C_RIM[0] $C_RIM[1] $C_RIM[2] $RIM_ALPHA), ([single]($rimPx*$SS))
    $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $gfx.DrawPath($penRim, $path); $penRim.Dispose()
    $path.Dispose()
  }

  # SIZE, and this is what "the helmet is very off" was. A head is 60px inside a 192 frame,
  # so on our 256 frame it draws about 80px wide - half-width 40. Measured against a real
  # medieval helm, the helm is 1.31x the head's width south, 1.27 north, 1.45 east. So the
  # half-width should be about 52. The previous attempt used 27 - barely half.
  $cx = 128.0; $cy = 128.0
  $hw = $(if ($rot -eq "east") { 58.0 } else { 52.0 })
  $hh = 55.0

  # A GREAT HELM, not a horned barbarian cap. Dome, brow, visor slit, nasal - the shapes that
  # say medieval knight. The horns were the wrong weapon entirely: they are the Dovahkiin's
  # idiom and this hero must not read as kin to him.
  Piece @(
    (HPT ($cx - $hw*0.74) ($cy + $hh*0.62)),
    (HPT ($cx - $hw*0.94) ($cy - $hh*0.06)),
    (HPT ($cx - $hw*0.66) ($cy - $hh*0.62)),
    (HPT ($cx - $hw*0.24) ($cy - $hh*0.92)),
    (HPT ($cx + $hw*0.24) ($cy - $hh*0.92)),
    (HPT ($cx + $hw*0.66) ($cy - $hh*0.62)),
    (HPT ($cx + $hw*0.94) ($cy - $hh*0.06)),
    (HPT ($cx + $hw*0.74) ($cy + $hh*0.62)),
    (HPT ($cx + $hw*0.40) ($cy + $hh*0.92)),
    (HPT ($cx - $hw*0.40) ($cy + $hh*0.92))
  ) $C_MID 150 1.8

  # NORD HORNS, restored. They were cut on a misreading: "the helmet is very off" was the helm
  # being drawn at HALF SIZE - half-width 27, where a head is 40 and a helm should be 52 - not
  # the horns being wrong. The user's own reference has them, so they return, now at scale.
  #
  # The mirror touches ONLY the x-component, and the rise goes as t-SQUARED so the horn leaves
  # the helm almost level and does its bending near the tip. Applying the side to both the
  # angle and the x is what made an earlier pair curl back to the centre line.
  foreach ($hornSide in @((-1.0), (1.0))) {
    $frontPts = @(); $backPts = @()
    $HSTEPS = 10
    for ($hstep = 0; $hstep -le $HSTEPS; $hstep++) {
      $tt = $hstep / [double]$HSTEPS
      $outward = ($hw * 0.70) + ($tt * $hw * 0.90)
      $upward  = (-$hh * 0.34) + ($tt * $tt * $hh * 1.06)
      $thick   = $hw * 0.18 * (1.0 - ($tt * 0.88))
      $hpx = $cx + ($hornSide * $outward)
      $hpy = $cy - $upward
      $frontPts += (HPT $hpx ($hpy - $thick))
      $backPts  += (HPT $hpx ($hpy + $thick))
    }
    [array]::Reverse($backPts)
    Piece ($frontPts + $backPts) $C_LIT 178 1.4
  }

  if ($rot -ne "north") {
    # the brow band
    Piece @(
      (HPT ($cx - $hw*0.90) ($cy - $hh*0.20)),
      (HPT ($cx + $hw*0.90) ($cy - $hh*0.20)),
      (HPT ($cx + $hw*0.86) ($cy + $hh*0.02)),
      (HPT ($cx - $hw*0.86) ($cy + $hh*0.02))
    ) $C_LIT 178 1.3
    # the visor slit - dark, the one place a dark accent belongs on a spectre, because it is
    # an opening rather than a shaded surface
    $brDark = New-Object System.Drawing.SolidBrush (RGB $C_DEEP[0] $C_DEEP[1] $C_DEEP[2] 150)
    $gfx.FillPolygon($brDark, [System.Drawing.PointF[]]@(
      (HPT ($cx - $hw*0.62) ($cy + $hh*0.10)),
      (HPT ($cx + $hw*0.62) ($cy + $hh*0.10)),
      (HPT ($cx + $hw*0.56) ($cy + $hh*0.30)),
      (HPT ($cx - $hw*0.56) ($cy + $hh*0.30))
    ))
    $brDark.Dispose()
    # the nasal, down the centre
    Piece @(
      (HPT ($cx - $hw*0.11) ($cy - $hh*0.16)),
      (HPT ($cx + $hw*0.11) ($cy - $hh*0.16)),
      (HPT ($cx + $hw*0.09) ($cy + $hh*0.70)),
      (HPT ($cx - $hw*0.09) ($cy + $hh*0.70))
    ) $C_LIT 172 1.2
  }
  $gfx.Dispose()
  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfin = [System.Drawing.Graphics]::FromImage($final)
  $gfin.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gfin.Clear((RGB 0 0 0 0))
  $gfin.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0,0,$SIZE,$SIZE))
  $gfin.Dispose(); $bmp.Dispose()
  return $final
}

# =====================================================================================
#  PREVIEW - over lit ground, on the real body sprite, at the sizes that matter.
#  Its own sheet rather than the Ancient Dragonborn's harness, because that one draws an
#  aura and titles itself after him, and this hero has neither.
# =====================================================================================
$ROTS = @("south", "north", "east")
$armour = @{}
$helms  = @{}
foreach ($rot in $ROTS) { $armour[$rot] = BuildArmour $rot; $helms[$rot] = BuildHelm $rot }

$CELL = 256
$PADX = 26
$sheetW = ($PADX * 4) + ($CELL * 3)
$sheetH = $CELL + 210
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gs = [System.Drawing.Graphics]::FromImage($sheet)
$gs.Clear((RGB 28 30 28 255))
$gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontBig = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontMid = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Bold)
$fontSml = New-Object System.Drawing.Font "Segoe UI", 9.5
$brW = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brG = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brO = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)
$gs.DrawString("CALL OF VALOR - the hero of Sovngarde. NORMAL armour, no aura.", $fontBig, $brW, [single]$PADX, [single]12)
$gs.DrawString("horned helm, fur-trimmed pauldrons, banded cuirass, belt, split skirt, bracers, greaves - plates and straps, not scales", $fontSml, $brG, [single]$PADX, [single]36)

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

# the invisible pawn beneath, at vanilla's own invisibility colour and alpha
function DrawGhostPawn($gfxRef, $img, [double]$cx, [double]$cy, [double]$size) {
  $cm = New-Object System.Drawing.Imaging.ColorMatrix
  $cm.Matrix00 = [single](191/255.0); $cm.Matrix11 = [single](237/255.0); $cm.Matrix22 = [single](250/255.0)
  $cm.Matrix33 = [single]0.5; $cm.Matrix44 = [single]1.0
  $ia = New-Object System.Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $rect = New-Object System.Drawing.Rectangle ([int]($cx-$size/2)), ([int]($cy-$size/2)), ([int]$size), ([int]$size)
  $gfxRef.DrawImage($img, $rect, 0, 0, $img.Width, $img.Height, [System.Drawing.GraphicsUnit]::Pixel, $ia)
  $ia.Dispose()
}

$column = 0
foreach ($rot in $ROTS) {
  $px = $PADX + ($column * ($CELL + $PADX))
  $py = 58
  Ground $gs $px $py $CELL (9 + $column)
  $bodyImg = New-Object System.Drawing.Bitmap (Join-Path $BODY_DIR "Naked_Male_$rot.png")
  $headImg = New-Object System.Drawing.Bitmap (Join-Path $HEAD_DIR "${HEAD_KIND}_$rot.png")
  DrawGhostPawn $gs $bodyImg ($px + $CELL/2) ($py + $CELL/2) $CELL
  DrawGhostPawn $gs $headImg ($px + $CELL/2) ($py + $CELL/2 - (0.34 * ($CELL/1.5))) $CELL
  $gs.DrawImage($armour[$rot], (New-Object System.Drawing.Rectangle ([int]$px), ([int]$py), $CELL, $CELL))
  $gs.DrawImage($helms[$rot], (New-Object System.Drawing.Rectangle ([int]$px), ([int]($py - (0.34 * ($CELL/1.5)))), $CELL, $CELL))
  $bodyImg.Dispose(); $headImg.Dispose()
  # the greatsword, if a candidate has been generated. Same hold as the Ancient Dragonborn's
  # axe preview: head up and back over the shoulder, not pointing at the ground.
  $swordPath = Join-Path $OUT_DIR "valor_gs_A.png"
  if (Test-Path $swordPath) {
    $sword = New-Object System.Drawing.Bitmap $swordPath
    $state = $gs.Save()
    $anchorX = $px + ($CELL/2) + ($(if ($rot -eq "north") { -0.30 } else { 0.30 }) * ($CELL/1.5))
    $anchorY = $py + ($CELL/2) + (0.06 * ($CELL/1.5))
    $gs.TranslateTransform([single]$anchorX, [single]$anchorY)
    $gs.RotateTransform([single]$(if ($rot -eq "north") { -62.0 } else { -70.0 }))
    $swSize = 1.25 * ($CELL/1.5)
    $gs.DrawImage($sword, (New-Object System.Drawing.Rectangle ([int](-$swSize/2)), ([int](-$swSize/2)), ([int]$swSize), ([int]$swSize)))
    $gs.Restore($state)
    $sword.Dispose()
  }
  $gs.DrawString("facing $rot", $fontMid, $brO, [single]$px, [single]($py + $CELL + 6))
  $column++
}

# play distance
$zx = $PADX
$zy = 58 + $CELL + 30
$gs.DrawString("at play distance:", $fontSml, $brG, [single]$zx, [single]($zy - 16))
foreach ($zs in @(96, 64, 48)) {
  Ground $gs $zx $zy $zs 70
  $bodyImg = New-Object System.Drawing.Bitmap (Join-Path $BODY_DIR "Naked_Male_south.png")
  $headImg = New-Object System.Drawing.Bitmap (Join-Path $HEAD_DIR "${HEAD_KIND}_south.png")
  DrawGhostPawn $gs $bodyImg ($zx + $zs/2) ($zy + $zs/2) $zs
  DrawGhostPawn $gs $headImg ($zx + $zs/2) ($zy + $zs/2 - (0.34 * ($zs/1.5))) $zs
  $gs.DrawImage($armour["south"], (New-Object System.Drawing.Rectangle ([int]$zx), ([int]$zy), $zs, $zs))
  $gs.DrawImage($helms["south"], (New-Object System.Drawing.Rectangle ([int]$zx), ([int]($zy - (0.34 * ($zs/1.5)))), $zs, $zs))
  $bodyImg.Dispose(); $headImg.Dispose()
  $gs.DrawString(("" + $zs + "px"), $fontSml, $brG, [single]$zx, [single]($zy + $zs + 2))
  $zx += $zs + 14
}
$gs.Dispose()

$previewPath = Join-Path $OUT_DIR "valor_armour.png"
$sheet.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
foreach ($rot in $ROTS) { $armour[$rot].Dispose(); $helms[$rot].Dispose() }
Write-Output "preview only - nothing written into the mod"
Write-Output ("wrote " + $previewPath)
Write-Output "DONE"









