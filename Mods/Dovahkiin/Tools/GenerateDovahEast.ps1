# =============================================================================================
#  SUPERSEDED 2026-08-03 - KEPT AS THE ART ENGINE'S WORKED EXAMPLE, NOT AS SHIPPING ART.
#
#  The user's verdict on this output: "still looks very...'child's drawing' ish".
#
#  It remains the best demonstration of DovahArtEngine.ps1 - a whole creature from ~40 lines of
#  anatomy config, spine loft plus catenary wings - and it is worth reading for that. But the
#  engine is the SECOND choice for creature art. Trace a generated reference when one can exist.
#  See Tools/DRAGON_ART_PIPELINE.md.
# =============================================================================================
# GenerateDovahEast.ps1 - the fallback dragon "Dovah", EAST view, built with the art engine.
#
# This is the engine's first real customer and its proof: a side view, which is the one view
# that could never be traced because no reference for it existed.
#
# Everything here is high-level anatomy. There is not one hand-placed polygon: the body is a
# spine plus a thickness profile, the crest is a decorator over that same spine, the wing is
# joint coordinates with a catenary membrane between them.

. "$PSScriptRoot\DovahArtEngine.ps1"

$OUT_DIR = if ($env:DOVAH_DEST) { $env:DOVAH_DEST } else { $PSScriptRoot }

# ============================================================ ANATOMY
# East view: head to the LEFT, tail to the RIGHT, seen from the side and slightly above.
$SPINE_CONTROL = @(
    @( 0.075, 0.470 ),   # snout
    @( 0.190, 0.436 ),   # jaw hinge
    @( 0.330, 0.470 ),   # base of neck
    @( 0.470, 0.520 ),   # shoulders
    @( 0.610, 0.545 ),   # hips
    @( 0.760, 0.512 ),   # tail root
    @( 0.895, 0.560 ),   # tail sweep
    @( 0.968, 0.498 )    # tail tip
)
# @(t, halfWidth). Widest at the CHEST, not the belly - a dragon is deep-chested and the
# mass has to sit forward or it reads as a lizard.
$BODY_THICKNESS = @(
    @( 0.000, 0.012 ),
    @( 0.055, 0.040 ),   # muzzle
    @( 0.120, 0.052 ),   # skull
    @( 0.190, 0.030 ),   # neck pinch, right behind the jaw
    @( 0.300, 0.044 ),
    @( 0.420, 0.086 ),   # chest
    @( 0.520, 0.078 ),
    @( 0.640, 0.062 ),   # haunch
    @( 0.740, 0.036 ),
    @( 0.860, 0.018 ),
    @( 1.000, 0.004 )    # tail point
)

$WING_ROOT   = @( 0.470, 0.480 )
$WING_ELBOW  = @( 0.560, 0.250 )
$WING_WRIST  = @( 0.700, 0.130 )
$WING_FINGER = @(
    @( 0.870, 0.108 ),
    @( 0.905, 0.250 ),
    @( 0.860, 0.372 ),
    @( 0.740, 0.430 )
)
$WING_TUCK   = @( 0.545, 0.452 )

# far wing, behind the body: same joints pulled in and up so it reads as depth, not a clone
$FARWING_ROOT   = @( 0.452, 0.452 )
$FARWING_ELBOW  = @( 0.520, 0.286 )
$FARWING_WRIST  = @( 0.628, 0.196 )
$FARWING_FINGER = @(
    @( 0.762, 0.176 ),
    @( 0.792, 0.286 ),
    @( 0.752, 0.378 ),
    @( 0.660, 0.418 )
)
$FARWING_TUCK   = @( 0.512, 0.436 )

$LEGS = @(
    #  hipX   hipY  kneeX  kneeY  footX  footY  girth   near?
    @( 0.545, 0.560, 0.588, 0.660, 0.556, 0.742, 0.030, $true  ),
    @( 0.470, 0.548, 0.436, 0.638, 0.470, 0.706, 0.026, $true  ),
    @( 0.520, 0.540, 0.552, 0.628, 0.524, 0.700, 0.024, $false ),
    @( 0.452, 0.532, 0.424, 0.612, 0.452, 0.672, 0.022, $false )
)

$PALETTE = New-DovahPalette `
    -Shadow   @(  26,  21,  16 ) `
    -Dark     @(  62,  49,  33 ) `
    -Mid      @( 104,  82,  50 ) `
    -Light    @( 152, 124,  82 ) `
    -Keyline  @(  13,  11,   9 ) `
    -RimLight @( 190, 166, 122 ) `
    -Accent   @( 228,  96,  34 )

# ============================================================ BUILD
$gfx = Initialize-DovahCanvas -Frame 512 -Supersample 3
$spine = New-DovahSpine -ControlPoints $SPINE_CONTROL -Samples 140

function Build-Limb {
    param([double]$HipX, [double]$HipY, [double]$KneeX, [double]$KneeY,
          [double]$FootX, [double]$FootY, [double]$Girth)
    $limbSpine = New-DovahSpine -ControlPoints @(
        @($HipX, $HipY), @($KneeX, $KneeY), @($FootX, $FootY)) -Samples 28
    $limbThickness = @( @(0.0, ($Girth * 1.00)), @(0.55, ($Girth * 0.62)), @(1.0, ($Girth * 0.42)) )
    return (New-DovahLoft -Spine $limbSpine -ThicknessProfile $limbThickness -CurveTension 0.30)
}

# ---- far wing and far legs first: everything behind the body
$farWingPath = New-DovahWing -Root $FARWING_ROOT -Elbow $FARWING_ELBOW -Wrist $FARWING_WRIST `
                             -Fingers $FARWING_FINGER -Tuck $FARWING_TUCK -SideSign 1 -Sag 0.30
Add-DovahPart -Graphics $gfx -Path $farWingPath -Palette $PALETTE -FillColor $PALETTE.Dark -KeyWidth 0.0120
Add-DovahWingStruts -Graphics $gfx -Root $FARWING_ROOT -Elbow $FARWING_ELBOW -Wrist $FARWING_WRIST `
                    -Fingers $FARWING_FINGER -SideSign 1 -BoneColor $PALETTE.Mid `
                    -ShadowColor $PALETTE.Keyline -BoneWidth 0.0078 -ClipPath $farWingPath

foreach ($leg in $LEGS) {
    if ($leg[7]) { continue }
    $legPath = Build-Limb $leg[0] $leg[1] $leg[2] $leg[3] $leg[4] $leg[5] $leg[6]
    Add-DovahPart -Graphics $gfx -Path $legPath -Palette $PALETTE -FillColor $PALETTE.Dark -KeyWidth 0.0110
    $legPath.Dispose()
}

# ---- dorsal crest, behind the body so the blades root under the back line
$crest = Get-DovahCrestBlades -Spine $spine -Count 15 -FromT 0.17 -ToT 0.95 `
                              -PeakLength 0.040 -PeakAt 0.36 -RootWidthRatio 0.50 -Lean 0.42 `
                              -ThicknessProfile $BODY_THICKNESS
foreach ($blade in $crest) {
    Add-DovahPart -Graphics $gfx -Path $blade -Palette $PALETTE -FillColor $PALETTE.Mid -KeyWidth 0.0062
    $blade.Dispose()
}

# ---- the body: ONE lofted silhouette, snout through tail tip
$bodyPath = New-DovahLoft -Spine $spine -ThicknessProfile $BODY_THICKNESS -CurveTension 0.20
Add-DovahKeyline -Graphics $gfx -Path $bodyPath -Color $PALETTE.Keyline -WidthFraction 0.0135
Add-DovahFlatFill -Graphics $gfx -Path $bodyPath -Color $PALETTE.Mid
Add-DovahCellShade -Graphics $gfx -Path $bodyPath -ShadowColor $PALETTE.Dark -LightDirY -1.0 -Coverage 0.44

# belly plates - side view, so a segmented underside is right here (it is a FRONT view where
# a ladder of cross-bands turns a creature into a woodlouse)
$plates = Get-DovahPlates -Spine $spine -Count 9 -FromT 0.40 -ToT 0.74 `
                          -ThicknessProfile $BODY_THICKNESS -WidthRatio 0.66 -Droop 0.16
$savedClip = $gfx.Clip
$gfx.SetClip($bodyPath)
foreach ($plate in $plates) {
    $platePen = New-DovahPen (New-DovahColor $PALETTE.Keyline.R $PALETTE.Keyline.G $PALETTE.Keyline.B 90) 0.0048
    $gfx.DrawCurve($platePen, $plate, 0.5)
    $platePen.Dispose()
}
$gfx.Clip = $savedClip
Add-DovahRimLight -Graphics $gfx -Path $bodyPath -Color $PALETTE.RimLight -WidthFraction 0.0042 -Alpha 135

# ---- head detail: brow, jaw, eye
$browPen = New-DovahPen (New-DovahColor $PALETTE.Keyline.R $PALETTE.Keyline.G $PALETTE.Keyline.B 150) 0.0090
$gfx.DrawCurve($browPen, @(
    (New-DovahPoint 0.104 0.446), (New-DovahPoint 0.150 0.432), (New-DovahPoint 0.196 0.440)), 0.5)
$browPen.Dispose()
$jawPen = New-DovahPen (New-DovahColor $PALETTE.Keyline.R $PALETTE.Keyline.G $PALETTE.Keyline.B 120) 0.0060
$gfx.DrawCurve($jawPen, @(
    (New-DovahPoint 0.082 0.492), (New-DovahPoint 0.140 0.500), (New-DovahPoint 0.194 0.482)), 0.5)
$jawPen.Dispose()

# horns off the back of the skull, raked along the neck
foreach ($hornSpec in @( @(0.176, 0.424, 0.268, 0.376, 0.017), @(0.166, 0.442, 0.246, 0.412, 0.012) )) {
    $hornPath = New-DovahSpike -RootX $hornSpec[0] -RootY $hornSpec[1] -TipX $hornSpec[2] -TipY $hornSpec[3] `
                               -RootHalfWidth $hornSpec[4] -Bow 0.012 -TaperPower 0.62
    if ($hornPath) {
        Add-DovahPart -Graphics $gfx -Path $hornPath -Palette $PALETTE -FillColor $PALETTE.Light -KeyWidth 0.0062
        $hornPath.Dispose()
    }
}
$eyeGlow = New-Object System.Drawing.SolidBrush (New-DovahColor $PALETTE.Accent.R $PALETTE.Accent.G $PALETTE.Accent.B 150)
$eyeCentre = New-DovahPoint 0.138 0.452
$eyeRadius = ConvertTo-DovahLength 0.011
$gfx.FillEllipse($eyeGlow, [float]($eyeCentre.X - $eyeRadius), [float]($eyeCentre.Y - ($eyeRadius * 0.7)),
                 [float]($eyeRadius * 2.0), [float]($eyeRadius * 1.4))
$eyeGlow.Dispose()
$bodyPath.Dispose()

# ---- near legs and near wing, in front
foreach ($leg in $LEGS) {
    if (-not $leg[7]) { continue }
    $legPath = Build-Limb $leg[0] $leg[1] $leg[2] $leg[3] $leg[4] $leg[5] $leg[6]
    Add-DovahPart -Graphics $gfx -Path $legPath -Palette $PALETTE -FillColor $PALETTE.Mid -KeyWidth 0.0115
    $legPath.Dispose()
}
$nearWingPath = New-DovahWing -Root $WING_ROOT -Elbow $WING_ELBOW -Wrist $WING_WRIST `
                              -Fingers $WING_FINGER -Tuck $WING_TUCK -SideSign 1 -Sag 0.32
Add-DovahPart -Graphics $gfx -Path $nearWingPath -Palette $PALETTE -FillColor $PALETTE.Light -KeyWidth 0.0130
Add-DovahWingStruts -Graphics $gfx -Root $WING_ROOT -Elbow $WING_ELBOW -Wrist $WING_WRIST `
                    -Fingers $WING_FINGER -SideSign 1 -BoneColor $PALETTE.Mid `
                    -ShadowColor $PALETTE.Keyline -BoneWidth 0.0092 -ClipPath $nearWingPath
$nearWingPath.Dispose()

# ============================================================ OUT
$sprite = Save-DovahSprite -Path (Join-Path $OUT_DIR "Dovah_east.png")

$report = Test-DovahSilhouette -Sprite $sprite
Write-Output "--- silhouette check ---"
Write-Output ("  ink            : {0}" -f $report.Ink)
Write-Output ("  bounding box   : {0}   aspect {1}" -f $report.BoundingBox, $report.Aspect)
Write-Output ("  fill density   : {0}" -f $report.FillDensity)
Write-Output ("  concavities    : {0}" -f $report.Concavities)
Write-Output ("  small-scale    : {0}" -f $report.SmallScaleRead)
foreach ($note in $report.Notes) { Write-Output ("  * {0}" -f $note) }

$sheet = New-DovahPreviewSheet -Sprite $sprite `
    -Title "DOVAH - EAST, built entirely by DovahArtEngine (spine loft + catenary wings)" `
    -Path (Join-Path $OUT_DIR "dovah_east_preview.png")
$sheet.Dispose()
$sprite.Dispose()
Write-Output "DONE"
