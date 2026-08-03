# =============================================================================================
#  REJECTED 2026-08-03 - KEPT AS A RECORD, DO NOT BUILD ON THIS.
#
#  This was the eighth hand-drawn creature attempt of that session. Like the seven before it,
#  the STYLE came out right (flat, keylined, RimWorld-ish) and the PROPORTIONS came out wrong -
#  the user's verdict was that it reads as a goat skull or a tribal totem, not as Alduin.
#
#  Named faults, so they are not rediscovered: the cheek spikes radiate like insect legs; the
#  muzzle is too long and narrow; the eyes are rectangles; the head is too tall for its width.
#  What are drawn here as "cheek spikes" are, in the reference, a NECK FRILL behind the skull -
#  a different object entirely.
#
#  THE ROUTE THAT WORKS IS TRACING A GENERATED REFERENCE.
#  See Tools/DRAGON_ART_PIPELINE.md and Tools/GEMINI_CREATURE_PROMPT.md.
# =============================================================================================
# GenerateAlduinHead.ps1 - Alduin's HEAD ONLY, south view, drawn from scratch.
#
# WHY FROM SCRATCH, after a working tracer existed:
# Tracing the reference and then simplifying it produced blur, every time and at every
# setting. That is not a tuning failure, it is the method: RimWorld art is DRAWN SIMPLE FROM
# THE START. You cannot render detail and subtract your way to simplicity - averaging,
# median and area-opening all destroy structure and noise together. Simple art has few, large,
# DELIBERATE shapes with hard edges, and those have to be authored.
#
# Proportions are measured off the reference (Alduin_southview_2.0), so the shapes are his:
#   * two large horns from the back of the skull, sweeping UP and OUT then hooking INWARD
#   * a crown of smaller blades between them
#   * cheek spikes flaring at the jaw
#   * angled eyes about 60% down the skull
#   * a narrow muzzle tapering to the chin
#
# STYLE RULES, from measuring shipped RimWorld-style animals:
#   4 flat tones + a heavy black keyline. No gradients anywhere. Top 3 tones should cover
#   ~85%+ of the creature. The keyline scales with the shape - a fixed px width stops being
#   an outline and becomes the shape.

. "$PSScriptRoot\DovahArtEngine.ps1"

$OUT_DIR = if ($env:DOVAH_DEST) { $env:DOVAH_DEST } else { $PSScriptRoot }
$gfx = Initialize-DovahCanvas -Frame 512 -Supersample 3

# ============================================================ palette
$C_KEY   = New-DovahColor  10  10  13     # outline
$C_DEEP  = New-DovahColor  30  31  37     # shadow: eye sockets, under the brow, flanks
$C_BASE  = New-DovahColor  58  60  69     # the head's main tone
$C_LIT   = New-DovahColor  92  96 108     # lit planes: snout ridge, horn fronts
$C_EDGE  = New-DovahColor 142 147 162     # narrow highlight, used sparingly
$C_EMBER = New-DovahColor 232  96  38
$C_EMBER_HOT = New-DovahColor 255 188 116

$KEY_MAIN  = 0.0150      # skull
$KEY_HORN  = 0.0105      # horns and spikes
$KEY_SMALL = 0.0075      # small details

# ============================================================ geometry helpers
function Add-Curve {
    # A closed shape from control points, drawn as ONE deliberate curve.
    param([object[]]$Points, [double]$Tension = 0.30)
    $devicePts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($pointVal in $Points) { $devicePts.Add((New-DovahPoint $pointVal[0] $pointVal[1])) }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddClosedCurve($devicePts.ToArray(), [float]$Tension)
    return $path
}
function Add-Poly {
    # Angular shapes - brow plates, crown blades. A curve tension rounds off exactly the
    # corners that carry the read.
    param([object[]]$Points)
    $devicePts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($pointVal in $Points) { $devicePts.Add((New-DovahPoint $pointVal[0] $pointVal[1])) }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon($devicePts.ToArray())
    return $path
}
function Draw-Shape {
    param([System.Drawing.Drawing2D.GraphicsPath]$Path, [System.Drawing.Color]$Fill,
          [double]$KeyWidth = 0.0120)
    $pen = New-DovahPen $C_KEY $KeyWidth
    $gfx.DrawPath($pen, $Path)
    $pen.Dispose()
    $brush = New-Object System.Drawing.SolidBrush $Fill
    $gfx.FillPath($brush, $Path)
    $brush.Dispose()
}

# A HORN: a curved spine with a thickness profile, lofted. Root broad, tip a point, and the
# tip hooks back inward - that hook is what separates a dragon horn from a cow's.
function Draw-Horn {
    param([object[]]$SpinePoints, [double]$RootHalf, [System.Drawing.Color]$Fill,
          [double]$KeyWidth = 0.0105, [double]$TipHalf = 0.0035)
    $spine = New-DovahSpine -ControlPoints $SpinePoints -Samples 60
    $profile = @(
        @( 0.00, $RootHalf ),
        @( 0.35, ($RootHalf * 0.66) ),
        @( 0.70, ($RootHalf * 0.30) ),
        @( 1.00, $TipHalf )
    )
    $path = New-DovahLoft -Spine $spine -ThicknessProfile $profile -CurveTension 0.16
    Draw-Shape $path $Fill $KeyWidth
    return $path
}

# ============================================================ 1. HORNS (behind the skull)
# Root at the back of the skull, sweeping up and OUT, then hooking IN at the tip.
# PASS 2: thicker at the root and rooted closer to the centre line. At half-width 0.052 and
# splayed to x 0.298 they read as ANTENNAE - a horn has to be a heavy thing growing out of a
# skull, not a wire standing beside it.
foreach ($sideSign in @(1, -1)) {
    $hornSpine = @(
        @( (Get-DovahMirrored 0.454 $sideSign), 0.352 ),
        @( (Get-DovahMirrored 0.396 $sideSign), 0.238 ),
        @( (Get-DovahMirrored 0.352 $sideSign), 0.122 ),
        @( (Get-DovahMirrored 0.382 $sideSign), 0.044 ),
        @( (Get-DovahMirrored 0.430 $sideSign), 0.022 )
    )
    $hornPath = Draw-Horn $hornSpine 0.074 $C_BASE $KEY_HORN
    # lit front face: a second, thinner loft offset toward the light (top-left)
    $litSpine = @(
        @( (Get-DovahMirrored 0.468 $sideSign), 0.346 ),
        @( (Get-DovahMirrored 0.412 $sideSign), 0.238 ),
        @( (Get-DovahMirrored 0.370 $sideSign), 0.128 ),
        @( (Get-DovahMirrored 0.396 $sideSign), 0.058 )
    )
    $litProfile = @( @(0.00, 0.030), @(0.55, 0.016), @(1.00, 0.004) )
    $litPath = New-DovahLoft -Spine (New-DovahSpine -ControlPoints $litSpine -Samples 40) `
                             -ThicknessProfile $litProfile -CurveTension 0.16
    $litBrush = New-Object System.Drawing.SolidBrush $C_LIT
    $gfx.FillPath($litBrush, $litPath)
    $litBrush.Dispose(); $litPath.Dispose()
    $hornPath.Dispose()
}

# ============================================================ 2. CROWN BLADES
# Between the horns. Few and large - a dense row reads as a zip at play distance.
# PASS 2: three, larger. Five small stubs read as a comb; three big blades read as a crest.
$CROWN = @(
    @( 0.500, 0.300, 0.500, 0.128, 0.040 ),
    @( 0.440, 0.336, 0.406, 0.192, 0.030 ),
    @( 0.560, 0.336, 0.594, 0.192, 0.030 )
)
foreach ($blade in $CROWN) {
    $bladePath = New-DovahSpike -RootX $blade[0] -RootY $blade[1] -TipX $blade[2] -TipY $blade[3] `
                                -RootHalfWidth $blade[4] -TaperPower 0.62
    if ($bladePath) { Draw-Shape $bladePath $C_BASE $KEY_HORN; $bladePath.Dispose() }
}

# ============================================================ 3. CHEEK SPIKES
# PASS 2: TWO per side, not three, and raked BACK and DOWN rather than radiating outward.
# Three evenly-spaced spikes fanning straight out of the cheek read as insect legs - and with
# the horns above them the whole head became a tribal mask.
foreach ($sideSign in @(1, -1)) {
    $cheekSet = @(
        @( 0.628, 0.512, 0.812, 0.462, 0.040 ),
        @( 0.600, 0.622, 0.760, 0.664, 0.032 )
    )
    foreach ($spike in $cheekSet) {
        $spikePath = New-DovahSpike -RootX (Get-DovahMirrored $spike[0] $sideSign) -RootY $spike[1] `
                                    -TipX (Get-DovahMirrored $spike[2] $sideSign) -TipY $spike[3] `
                                    -RootHalfWidth $spike[4] -TaperPower 0.66
        if ($spikePath) { Draw-Shape $spikePath $C_BASE $KEY_HORN; $spikePath.Dispose() }
    }
}

# ============================================================ 4. THE SKULL
# One continuous curve: crown -> brow -> cheek -> jaw -> muzzle -> chin.
# PASS 2: broader, with a real JAW CORNER, and a low curve tension so it reads as a skull
# with planes rather than an egg. The muzzle sides run near-parallel before the chin.
$skullPath = Add-Curve @(
    @( 0.500, 0.252 ),
    @( 0.582, 0.286 ),
    @( 0.650, 0.372 ),
    @( 0.678, 0.482 ),   # cheek, widest
    @( 0.638, 0.578 ),   # jaw corner - a distinct angle, not a curve
    @( 0.566, 0.652 ),
    @( 0.546, 0.756 ),   # muzzle side, near parallel
    @( 0.514, 0.858 ),
    @( 0.486, 0.858 ),
    @( 0.454, 0.756 ),
    @( 0.434, 0.652 ),
    @( 0.362, 0.578 ),
    @( 0.322, 0.482 ),
    @( 0.350, 0.372 ),
    @( 0.418, 0.286 )
) 0.14
Draw-Shape $skullPath $C_BASE $KEY_MAIN

# ---- shading, clipped to the skull. FLAT regions only, never a gradient.
$savedClip = $gfx.Clip
$gfx.SetClip($skullPath)

# darker flanks: the sides of the skull turn away from the light
foreach ($sideSign in @(1, -1)) {
    $flankPath = Add-Curve @(
        @( (Get-DovahMirrored 0.660 $sideSign), 0.372 ),
        @( (Get-DovahMirrored 0.694 $sideSign), 0.490 ),
        @( (Get-DovahMirrored 0.646 $sideSign), 0.600 ),
        @( (Get-DovahMirrored 0.558 $sideSign), 0.780 ),
        @( (Get-DovahMirrored 0.528 $sideSign), 0.868 ),
        @( (Get-DovahMirrored 0.582 $sideSign), 0.700 ),
        @( (Get-DovahMirrored 0.606 $sideSign), 0.520 ),
        @( (Get-DovahMirrored 0.596 $sideSign), 0.380 )
    ) 0.30
    $flankBrush = New-Object System.Drawing.SolidBrush $C_DEEP
    $gfx.FillPath($flankBrush, $flankPath)
    $flankBrush.Dispose(); $flankPath.Dispose()
}

# lit ridge down the centre of the muzzle - one shape, flat, no falloff
$ridgePath = Add-Curve @(
    @( 0.500, 0.330 ),
    @( 0.536, 0.430 ),
    @( 0.530, 0.580 ),
    @( 0.516, 0.740 ),
    @( 0.500, 0.812 ),
    @( 0.484, 0.740 ),
    @( 0.470, 0.580 ),
    @( 0.464, 0.430 )
) 0.28
$ridgeBrush = New-Object System.Drawing.SolidBrush $C_LIT
$gfx.FillPath($ridgeBrush, $ridgePath)
$ridgeBrush.Dispose(); $ridgePath.Dispose()

# brow plates - angular, dark, sitting over each eye. This is the single feature that stops
# a dragon skull reading as a snake head.
foreach ($sideSign in @(1, -1)) {
    $browPath = Add-Poly @(
        @( (Get-DovahMirrored 0.508 $sideSign), 0.418 ),
        @( (Get-DovahMirrored 0.656 $sideSign), 0.458 ),
        @( (Get-DovahMirrored 0.668 $sideSign), 0.522 ),
        @( (Get-DovahMirrored 0.516 $sideSign), 0.482 )
    )
    $browBrush = New-Object System.Drawing.SolidBrush $C_DEEP
    $gfx.FillPath($browBrush, $browPath)
    $browBrush.Dispose()
    $browPen = New-DovahPen $C_KEY $KEY_SMALL
    $gfx.DrawPath($browPen, $browPath)
    $browPen.Dispose(); $browPath.Dispose()
}

# a single mouth line - one stroke, no shading
$mouthPen = New-DovahPen $C_KEY 0.0068
$gfx.DrawCurve($mouthPen, @(
    (New-DovahPoint 0.452 0.756),
    (New-DovahPoint 0.500 0.784),
    (New-DovahPoint 0.548 0.756)
), 0.5)
$mouthPen.Dispose()

$gfx.Clip = $savedClip
$skullPath.Dispose()

# ============================================================ 5. EYES
# Angled almonds, inner corner LOW - that tilt is most of the menace. Two tones only:
# a hot core inside an ember body, over a dark socket already painted by the brow.
foreach ($sideSign in @(1, -1)) {
    # PASS 2: moved APART. With the inner corner at 0.478 the two eyes nearly met at the
    # centre line and read as one orange bar - a moustache, not a pair of eyes.
    $eyePath = Add-Poly @(
        @( (Get-DovahMirrored 0.538 $sideSign), 0.532 ),
        @( (Get-DovahMirrored 0.618 $sideSign), 0.500 ),
        @( (Get-DovahMirrored 0.638 $sideSign), 0.528 ),
        @( (Get-DovahMirrored 0.564 $sideSign), 0.558 )
    )
    $socketPen = New-DovahPen $C_KEY 0.0060
    $gfx.DrawPath($socketPen, $eyePath)
    $socketPen.Dispose()
    $eyeBrush = New-Object System.Drawing.SolidBrush $C_EMBER
    $gfx.FillPath($eyeBrush, $eyePath)
    $eyeBrush.Dispose(); $eyePath.Dispose()

    $corePath = Add-Poly @(
        @( (Get-DovahMirrored 0.560 $sideSign), 0.528 ),
        @( (Get-DovahMirrored 0.610 $sideSign), 0.508 ),
        @( (Get-DovahMirrored 0.620 $sideSign), 0.524 ),
        @( (Get-DovahMirrored 0.576 $sideSign), 0.544 )
    )
    $coreBrush = New-Object System.Drawing.SolidBrush $C_EMBER_HOT
    $gfx.FillPath($coreBrush, $corePath)
    $coreBrush.Dispose(); $corePath.Dispose()
}

# ============================================================ 6. NOSTRILS
foreach ($sideSign in @(1, -1)) {
    $nostrilBrush = New-Object System.Drawing.SolidBrush $C_KEY
    $centre = New-DovahPoint (Get-DovahMirrored 0.524 $sideSign) 0.712
    $radiusX = ConvertTo-DovahLength 0.013
    $radiusY = ConvertTo-DovahLength 0.009
    $gfx.FillEllipse($nostrilBrush, [float]($centre.X - $radiusX), [float]($centre.Y - $radiusY),
                     [float]($radiusX * 2.0), [float]($radiusY * 2.0))
    $nostrilBrush.Dispose()
}

# ============================================================ out
$sprite = Save-DovahSprite -Path (Join-Path $OUT_DIR "AlduinHead_south.png")
$report = Test-DovahSilhouette -Sprite $sprite
Write-Output "--- silhouette ---"
Write-Output ("  bbox {0}  aspect {1}  fill {2}  concavities {3}  small-scale {4}" -f `
    $report.BoundingBox, $report.Aspect, $report.FillDensity, $report.Concavities, $report.SmallScaleRead)
foreach ($note in $report.Notes) { Write-Output ("  * {0}" -f $note) }

$sheet = New-DovahPreviewSheet -Sprite $sprite `
    -Title "ALDUIN - HEAD ONLY, drawn from scratch (flat tones, heavy keyline)" `
    -Path (Join-Path $OUT_DIR "alduin_head_preview.png")
$sheet.Dispose(); $sprite.Dispose()
Write-Output "DONE"
