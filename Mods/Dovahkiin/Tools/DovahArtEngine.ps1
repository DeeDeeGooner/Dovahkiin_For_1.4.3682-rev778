# =============================================================================================
#  DovahArtEngine.ps1  -  procedural creature-art library for the Dovahkiin mod.
#
#  DOT-SOURCE THIS. Do not copy pieces of it into a generator; that copy-paste drift is what
#  made every creature built in one long session on 2026-08-02 come out a sibling of the first.
#
#      . "$PSScriptRoot\DovahArtEngine.ps1"
#      Initialize-DovahCanvas -Frame 512 -Supersample 3
#      ...build paths...
#      Save-DovahSprite -Path "out.png"
#
#  WHY THIS EXISTS, and what it does NOT solve
#  -------------------------------------------
#  Six hand-authored dragons failed in a row, every one of them on the SILHOUETTE. One traced
#  from a reference succeeded immediately. This engine makes geometry cheaper and consistent -
#  it is a better chisel, not a better eye. When a reference image exists, TRACE IT
#  (Tools/TraceRef.ps1 + Tools/BuildFromMask.ps1). Reach for this engine when there is no
#  reference: the east and north views of a creature only drawn once, new creatures, variants.
#
#  DESIGN RULES BAKED IN, each one paid for by a playtest or a wasted round:
#    * every dimension is a FRACTION of the frame, never pixels, so the art rescales cleanly
#    * the keyline SCALES WITH THE SHAPE - a fixed px outline stops being an outline
#    * all jitter is a deterministic sine hash, never Get-Random, so hash checks against an
#      approved snapshot stay meaningful
#    * colours are clamped in ONE place (New-DovahColor)
#    * no single-letter variable names anywhere (this has cost this project time four times)
#
#  Built 2026-08-03. Architecture proposed by the user via Gemini; module 4 of that proposal
#  (a vertex-count "complexity gate") was REJECTED on evidence and replaced by
#  Test-DovahSilhouette - see its header.
# =============================================================================================

Add-Type -AssemblyName System.Drawing

# ---------------------------------------------------------------------------- canvas state
$script:DovahFrame       = 512.0
$script:DovahSupersample = 3
$script:DovahCanvas      = $null
$script:DovahGfx         = $null

function Initialize-DovahCanvas {
    param([double]$Frame = 512.0, [int]$Supersample = 3)
    $script:DovahFrame       = $Frame
    $script:DovahSupersample = $Supersample
    $deviceSize = [int]($Frame * $Supersample)
    if ($script:DovahGfx)    { $script:DovahGfx.Dispose() }
    if ($script:DovahCanvas) { $script:DovahCanvas.Dispose() }
    $script:DovahCanvas = New-Object System.Drawing.Bitmap ($deviceSize, $deviceSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $script:DovahGfx = [System.Drawing.Graphics]::FromImage($script:DovahCanvas)
    $script:DovahGfx.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $script:DovahGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $script:DovahGfx.Clear([System.Drawing.Color]::Transparent)
    return $script:DovahGfx
}

function Get-DovahGfx { return $script:DovahGfx }

# fraction of frame -> device point
function New-DovahPoint {
    param([double]$FracX, [double]$FracY)
    return New-Object System.Drawing.PointF (
        ($FracX * $script:DovahFrame * $script:DovahSupersample),
        ($FracY * $script:DovahFrame * $script:DovahSupersample))
}
function ConvertTo-DovahLength {
    param([double]$Fraction)
    return ($Fraction * $script:DovahFrame * $script:DovahSupersample)
}
function Get-DovahMirrored {
    param([double]$FracX, [int]$SideSign)
    return (0.5 + (($FracX - 0.5) * $SideSign))
}

# ---------------------------------------------------------------------------- colour
function New-DovahColor {
    # The ONE place colours are constructed. Clamping here makes the whole class of
    # "FromArgb threw because alpha was multiplied downstream" impossible.
    param([int]$Red, [int]$Green, [int]$Blue, [int]$Alpha = 255)
    $Red   = [Math]::Max(0, [Math]::Min(255, $Red))
    $Green = [Math]::Max(0, [Math]::Min(255, $Green))
    $Blue  = [Math]::Max(0, [Math]::Min(255, $Blue))
    $Alpha = [Math]::Max(0, [Math]::Min(255, $Alpha))
    return [System.Drawing.Color]::FromArgb($Alpha, $Red, $Green, $Blue)
}
function Get-DovahHash01 {
    param([double]$SeedA, [double]$SeedB, [double]$Salt = 37.719)
    $raw = [Math]::Sin(($SeedA * 12.9898) + ($SeedB * 78.233) + $Salt) * 43758.5453
    return ($raw - [Math]::Floor($raw))
}
function New-DovahPen {
    param([System.Drawing.Color]$Color, [double]$WidthFraction)
    $pen = New-Object System.Drawing.Pen ($Color, [float](ConvertTo-DovahLength $WidthFraction))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    return $pen
}

# ---------------------------------------------------------------------------- 1. SPINE
# A creature is a CURVE with thickness, not a stack of blobs. Every body built by hand in
# this project came out as a tube because the spine was implicitly a vertical line.
function New-DovahSpine {
    <#
      Catmull-Rom through the given control points. Returns an ordered list of
      @{X;Y;T;TangentX;TangentY;NormalX;NormalY} sampled along the curve.
      ControlPoints: array of @(fracX, fracY), at least 2.
    #>
    param(
        [object[]]$ControlPoints,
        [int]$Samples = 96
    )
    if ($ControlPoints.Count -lt 2) { throw "New-DovahSpine needs at least 2 control points" }

    # duplicate the ends so the curve actually reaches its first and last control point
    $padded = New-Object System.Collections.Generic.List[object]
    $padded.Add($ControlPoints[0])
    foreach ($controlPoint in $ControlPoints) { $padded.Add($controlPoint) }
    $padded.Add($ControlPoints[$ControlPoints.Count - 1])

    $spine = New-Object System.Collections.Generic.List[object]
    $segmentCount = $padded.Count - 3
    for ($sampleIdx = 0; $sampleIdx -le $Samples; $sampleIdx++) {
        $globalT = $sampleIdx / [double]$Samples
        $scaled = $globalT * $segmentCount
        $segIdx = [int][Math]::Floor($scaled)
        if ($segIdx -gt ($segmentCount - 1)) { $segIdx = $segmentCount - 1 }
        $localT = $scaled - $segIdx

        $p0 = $padded[$segIdx]; $p1 = $padded[$segIdx + 1]
        $p2 = $padded[$segIdx + 2]; $p3 = $padded[$segIdx + 3]
        $tSq = $localT * $localT
        $tCu = $tSq * $localT

        $posX = 0.5 * ((2.0 * $p1[0]) + ((-$p0[0] + $p2[0]) * $localT) +
                ((2.0*$p0[0] - 5.0*$p1[0] + 4.0*$p2[0] - $p3[0]) * $tSq) +
                ((-$p0[0] + 3.0*$p1[0] - 3.0*$p2[0] + $p3[0]) * $tCu))
        $posY = 0.5 * ((2.0 * $p1[1]) + ((-$p0[1] + $p2[1]) * $localT) +
                ((2.0*$p0[1] - 5.0*$p1[1] + 4.0*$p2[1] - $p3[1]) * $tSq) +
                ((-$p0[1] + 3.0*$p1[1] - 3.0*$p2[1] + $p3[1]) * $tCu))
        # tangent = derivative of the same basis
        $tanX = 0.5 * ((-$p0[0] + $p2[0]) +
                (2.0 * (2.0*$p0[0] - 5.0*$p1[0] + 4.0*$p2[0] - $p3[0]) * $localT) +
                (3.0 * (-$p0[0] + 3.0*$p1[0] - 3.0*$p2[0] + $p3[0]) * $tSq))
        $tanY = 0.5 * ((-$p0[1] + $p2[1]) +
                (2.0 * (2.0*$p0[1] - 5.0*$p1[1] + 4.0*$p2[1] - $p3[1]) * $localT) +
                (3.0 * (-$p0[1] + 3.0*$p1[1] - 3.0*$p2[1] + $p3[1]) * $tSq))
        $tanLen = [Math]::Sqrt(($tanX * $tanX) + ($tanY * $tanY))
        if ($tanLen -le 1e-9) { $tanX = 0.0; $tanY = 1.0; $tanLen = 1.0 }
        $tanX = $tanX / $tanLen; $tanY = $tanY / $tanLen

        $spine.Add([pscustomobject]@{
            X = $posX; Y = $posY; T = $globalT
            TangentX = $tanX;  TangentY = $tanY
            NormalX  = -$tanY; NormalY  = $tanX
        })
    }
    return $spine
}

function Get-DovahThicknessAt {
    <#
      ThicknessProfile: array of @(t, halfWidth), t ascending in 0..1. Linear between stops.
      Kept separate from the spine so one body outline can be re-lofted at different
      girths without rebuilding the curve.
    #>
    param([object[]]$ThicknessProfile, [double]$AtT)
    if ($ThicknessProfile.Count -eq 0) { return 0.0 }
    if ($AtT -le $ThicknessProfile[0][0]) { return $ThicknessProfile[0][1] }
    $lastIdx = $ThicknessProfile.Count - 1
    if ($AtT -ge $ThicknessProfile[$lastIdx][0]) { return $ThicknessProfile[$lastIdx][1] }
    for ($stopIdx = 0; $stopIdx -lt $lastIdx; $stopIdx++) {
        $lowT = $ThicknessProfile[$stopIdx][0]
        $highT = $ThicknessProfile[$stopIdx + 1][0]
        if (($AtT -ge $lowT) -and ($AtT -le $highT)) {
            $span = $highT - $lowT
            if ($span -le 1e-9) { return $ThicknessProfile[$stopIdx][1] }
            $blend = ($AtT - $lowT) / $span
            # smoothstep, so a girth change reads as a taper rather than a kink
            $blend = $blend * $blend * (3.0 - (2.0 * $blend))
            return ($ThicknessProfile[$stopIdx][1] + (($ThicknessProfile[$stopIdx + 1][1] - $ThicknessProfile[$stopIdx][1]) * $blend))
        }
    }
    return $ThicknessProfile[$lastIdx][1]
}

function New-DovahLoft {
    <#
      Sweep a thickness profile along a spine and close it into one continuous silhouette.
      This is the whole point of the engine: head, neck, chest and tail become ONE organic
      outline instead of separate primitives that have to be made to agree.
    #>
    param(
        $Spine,
        [object[]]$ThicknessProfile,
        [double]$CurveTension = 0.22
    )
    $rightSide = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $leftSide  = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($node in $Spine) {
        $halfWidth = Get-DovahThicknessAt $ThicknessProfile $node.T
        $rightSide.Add((New-DovahPoint ($node.X + ($node.NormalX * $halfWidth)) ($node.Y + ($node.NormalY * $halfWidth))))
        $leftSide.Insert(0, (New-DovahPoint ($node.X - ($node.NormalX * $halfWidth)) ($node.Y - ($node.NormalY * $halfWidth))))
    }
    $allPoints = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $allPoints.AddRange($rightSide)
    $allPoints.AddRange($leftSide)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddClosedCurve($allPoints.ToArray(), [float]$CurveTension)
    return $path
}

# ---------------------------------------------------------------------------- 2. DETAIL
function New-DovahSpike {
    <#
      The one tapered-blade primitive. Horns, crown blades, dorsal spines, claws and wing
      thumbs are all this. Hand-written four separate times before it was factored out.
      TaperPower < 1 keeps the blade broad and snaps to a point late (a horn);
      > 1 tapers immediately (a needle).
    #>
    param(
        [double]$RootX, [double]$RootY, [double]$TipX, [double]$TipY,
        [double]$RootHalfWidth,
        [double]$Bow = 0.0,
        [double]$TaperPower = 0.72,
        [int]$Samples = 16
    )
    $runX = $TipX - $RootX; $runY = $TipY - $RootY
    $runLength = [Math]::Sqrt(($runX * $runX) + ($runY * $runY))
    if ($runLength -le 1e-9) { return $null }
    $perpX = -$runY / $runLength; $perpY = $runX / $runLength
    $sideA = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $sideB = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($sampleIdx = 0; $sampleIdx -le $Samples; $sampleIdx++) {
        $along = $sampleIdx / [double]$Samples
        # bow grows as t^2 so the blade leaves its root straight and curves near the tip
        $bend = $Bow * $along * $along
        $centreX = $RootX + ($runX * $along) + ($perpX * $bend)
        $centreY = $RootY + ($runY * $along) + ($perpY * $bend)
        $halfWidth = $RootHalfWidth * [Math]::Pow((1.0 - $along), $TaperPower)
        $sideA.Add((New-DovahPoint ($centreX + ($perpX * $halfWidth)) ($centreY + ($perpY * $halfWidth))))
        $sideB.Insert(0, (New-DovahPoint ($centreX - ($perpX * $halfWidth)) ($centreY - ($perpY * $halfWidth))))
    }
    $allPoints = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $allPoints.AddRange($sideA); $allPoints.AddRange($sideB)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    # low tension: a blade's corners are the read, and a curve rounds off exactly those
    $path.AddClosedCurve($allPoints.ToArray(), 0.06)
    return $path
}

function Get-DovahCrestBlades {
    <#
      Place tapered blades along a spine. Returns path objects; the caller decides tone and
      draw order. Size follows a bulge curve so the crest peaks over the shoulders and tapers
      both ways - a constant-size row reads as a zip, and 22 small ones read as an insect.
      Lean rakes each blade backward along the spine, which is what makes it read as growth
      rather than as spikes glued on.
    #>
    param(
        $Spine,
        [int]$Count = 11,
        [double]$FromT = 0.15, [double]$ToT = 0.92,
        [double]$PeakLength = 0.045, [double]$PeakAt = 0.30,
        [double]$RootWidthRatio = 0.42,
        [double]$Lean = 0.34,
        [double]$AngleJitter = 0.0,
        [double]$Salt = 11.7,
        # PASS THE BODY'S OWN PROFILE. Without it the blades root on the CENTRELINE and a
        # 0.052 blade on a body of half-width 0.086 is entirely buried - the crest renders
        # perfectly and is invisible, with no error anywhere. Rooting at the SURFACE makes
        # PeakLength mean "how far it projects past the back", which is what it should mean.
        [object[]]$ThicknessProfile = $null
    )
    $blades = New-Object System.Collections.Generic.List[object]
    if ($Count -lt 1) { return $blades }
    for ($bladeIdx = 0; $bladeIdx -lt $Count; $bladeIdx++) {
        $ratio = if ($Count -eq 1) { 0.5 } else { $bladeIdx / [double]($Count - 1) }
        $spineT = $FromT + (($ToT - $FromT) * $ratio)
        $nodeIdx = [int][Math]::Round($spineT * ($Spine.Count - 1))
        if ($nodeIdx -lt 0) { $nodeIdx = 0 }
        if ($nodeIdx -ge $Spine.Count) { $nodeIdx = $Spine.Count - 1 }
        $node = $Spine[$nodeIdx]
        # bulge: 1 at PeakAt, falling away on both sides
        $distanceFromPeak = [Math]::Abs($ratio - $PeakAt) / [Math]::Max(0.001, [Math]::Max($PeakAt, (1.0 - $PeakAt)))
        $bulge = [Math]::Max(0.18, (1.0 - ($distanceFromPeak * $distanceFromPeak)))
        $bladeLength = $PeakLength * $bulge
        $jitter = 0.0
        if ($AngleJitter -gt 0.0) { $jitter = ((Get-DovahHash01 $bladeIdx 3.0 $Salt) - 0.5) * $AngleJitter }
        # sit the root ON THE BODY SURFACE, slightly inside so it never floats
        $surfaceOffset = 0.0
        if ($ThicknessProfile) { $surfaceOffset = (Get-DovahThicknessAt $ThicknessProfile $node.T) }
        $rootX = $node.X - ($node.NormalX * $surfaceOffset * 0.72)
        $rootY = $node.Y - ($node.NormalY * $surfaceOffset * 0.72)
        # blades stand along the NORMAL from the surface, raked back along the tangent
        $tipX = $node.X - ($node.NormalX * ($surfaceOffset + $bladeLength)) - ($node.TangentX * $bladeLength * $Lean) + $jitter
        $tipY = $node.Y - ($node.NormalY * ($surfaceOffset + $bladeLength)) - ($node.TangentY * $bladeLength * $Lean)
        $bladePath = New-DovahSpike -RootX $rootX -RootY $rootY -TipX $tipX -TipY $tipY `
                                    -RootHalfWidth ($bladeLength * $RootWidthRatio) -TaperPower 0.66
        if ($bladePath) { $blades.Add($bladePath) }
    }
    return $blades
}

function New-DovahWing {
    <#
      Membrane between finger struts, sagging as a CATENARY rather than a circular arc.
      A hanging sheet is a cosh curve: nearly flat where it meets each strut, deepest in the
      middle. A bezier scoop is close but reads as machined; the catenary is what makes it
      look like leather with weight in it.

      Root/Elbow/Wrist/Fingers/Tuck are @(fracX, fracY) in the UNMIRRORED (right-hand) pose.
      SideSign +1 draws it, -1 mirrors it.
    #>
    param(
        [object[]]$Root, [object[]]$Elbow, [object[]]$Wrist,
        [object[]]$Fingers, [object[]]$Tuck,
        [int]$SideSign = 1,
        [double]$Sag = 0.30,
        [double]$LeadingBow = 0.045
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath

    # leading edge: root -> elbow -> wrist, bowed forward
    $leadMidX = ($Root[0] + $Elbow[0]) * 0.5
    $leadMidY = (($Root[1] + $Elbow[1]) * 0.5) - $LeadingBow
    $path.AddBezier(
        (New-DovahPoint (Get-DovahMirrored $Root[0] $SideSign) $Root[1]),
        (New-DovahPoint (Get-DovahMirrored $leadMidX $SideSign) $leadMidY),
        (New-DovahPoint (Get-DovahMirrored $leadMidX $SideSign) $leadMidY),
        (New-DovahPoint (Get-DovahMirrored $Elbow[0] $SideSign) $Elbow[1]))
    $wristMidX = ($Elbow[0] + $Wrist[0]) * 0.5
    $wristMidY = (($Elbow[1] + $Wrist[1]) * 0.5) - ($LeadingBow * 0.5)
    $path.AddBezier(
        (New-DovahPoint (Get-DovahMirrored $Elbow[0] $SideSign) $Elbow[1]),
        (New-DovahPoint (Get-DovahMirrored $wristMidX $SideSign) $wristMidY),
        (New-DovahPoint (Get-DovahMirrored $wristMidX $SideSign) $wristMidY),
        (New-DovahPoint (Get-DovahMirrored $Wrist[0] $SideSign) $Wrist[1]))
    $path.AddLine(
        (New-DovahPoint (Get-DovahMirrored $Wrist[0] $SideSign) $Wrist[1]),
        (New-DovahPoint (Get-DovahMirrored $Fingers[0][0] $SideSign) $Fingers[0][1]))

    # trailing edge: a catenary sag between each consecutive pair, ending at the tuck
    $anchors = New-Object System.Collections.Generic.List[object]
    foreach ($finger in $Fingers) { $anchors.Add($finger) }
    $anchors.Add($Tuck)
    for ($spanIdx = 0; $spanIdx -lt ($anchors.Count - 1); $spanIdx++) {
        $fromPoint = $anchors[$spanIdx]
        $toPoint   = $anchors[$spanIdx + 1]
        $isLastSpan = ($spanIdx -eq ($anchors.Count - 2))
        $spanSag = if ($isLastSpan) { $Sag * 0.55 } else { $Sag }
        # sag direction: toward the wrist, i.e. into the wing
        $spanMidX = ($fromPoint[0] + $toPoint[0]) * 0.5
        $spanMidY = ($fromPoint[1] + $toPoint[1]) * 0.5
        $towardWristX = $Wrist[0] - $spanMidX
        $towardWristY = $Wrist[1] - $spanMidY
        $samples = 12
        for ($stepIdx = 1; $stepIdx -le $samples; $stepIdx++) {
            $along = $stepIdx / [double]$samples
            $prevAlong = ($stepIdx - 1) / [double]$samples
            # cosh normalised to 0 at both ends, 1 at the middle
            $coshDepth = { param($aValue)
                $centred = ($aValue - 0.5) * 2.0
                $curveK = 1.9
                return (([Math]::Cosh($curveK) - [Math]::Cosh($curveK * $centred)) / ([Math]::Cosh($curveK) - 1.0))
            }
            $depthNow  = & $coshDepth $along
            $depthPrev = & $coshDepth $prevAlong
            $fromX = $fromPoint[0] + (($toPoint[0] - $fromPoint[0]) * $prevAlong) + ($towardWristX * $spanSag * $depthPrev)
            $fromY = $fromPoint[1] + (($toPoint[1] - $fromPoint[1]) * $prevAlong) + ($towardWristY * $spanSag * $depthPrev)
            $toX   = $fromPoint[0] + (($toPoint[0] - $fromPoint[0]) * $along)     + ($towardWristX * $spanSag * $depthNow)
            $toY   = $fromPoint[1] + (($toPoint[1] - $fromPoint[1]) * $along)     + ($towardWristY * $spanSag * $depthNow)
            $path.AddLine(
                (New-DovahPoint (Get-DovahMirrored $fromX $SideSign) $fromY),
                (New-DovahPoint (Get-DovahMirrored $toX $SideSign) $toY))
        }
    }
    $path.CloseFigure()
    return $path
}

function Add-DovahWingStruts {
    <#
      The finger bones. Drawn as a dark offset shadow first, then the lit bone, so each strut
      sits ON the membrane rather than in it.
    #>
    param(
        [System.Drawing.Graphics]$Graphics,
        [object[]]$Root, [object[]]$Elbow, [object[]]$Wrist, [object[]]$Fingers,
        [int]$SideSign = 1,
        [System.Drawing.Color]$BoneColor,
        [System.Drawing.Color]$ShadowColor,
        [double]$BoneWidth = 0.0100,
        [System.Drawing.Drawing2D.GraphicsPath]$ClipPath = $null
    )
    $savedClip = $Graphics.Clip
    if ($ClipPath) { $Graphics.SetClip($ClipPath) }
    $shadowPen = New-DovahPen $ShadowColor ($BoneWidth * 0.55)
    foreach ($finger in $Fingers) {
        $Graphics.DrawLine($shadowPen,
            (New-DovahPoint (Get-DovahMirrored ($Wrist[0] + 0.005) $SideSign) ($Wrist[1] + 0.008)),
            (New-DovahPoint (Get-DovahMirrored ($finger[0] + 0.005) $SideSign) ($finger[1] + 0.008)))
    }
    $shadowPen.Dispose()
    $bonePen = New-DovahPen $BoneColor $BoneWidth
    foreach ($finger in $Fingers) {
        $Graphics.DrawLine($bonePen,
            (New-DovahPoint (Get-DovahMirrored $Wrist[0] $SideSign) $Wrist[1]),
            (New-DovahPoint (Get-DovahMirrored $finger[0] $SideSign) $finger[1]))
    }
    $Graphics.DrawLine($bonePen,
        (New-DovahPoint (Get-DovahMirrored $Root[0] $SideSign) $Root[1]),
        (New-DovahPoint (Get-DovahMirrored $Elbow[0] $SideSign) $Elbow[1]))
    $Graphics.DrawLine($bonePen,
        (New-DovahPoint (Get-DovahMirrored $Elbow[0] $SideSign) $Elbow[1]),
        (New-DovahPoint (Get-DovahMirrored $Wrist[0] $SideSign) $Wrist[1]))
    $bonePen.Dispose()
    $Graphics.Clip = $savedClip
}

function Get-DovahPlates {
    <#
      Overlapping belly / chest segments along a spine. Returns curve point-sets for the
      caller to stroke.
      NOTE, and it cost a render: a row of plates ACROSS a body plus a spine crest reads as
      an INSECT ABDOMEN. Use these on a side view, or sparingly on a front view (3-4, wide),
      never as a dense ladder down the centre line.
    #>
    param(
        $Spine,
        [int]$Count = 7,
        [double]$FromT = 0.30, [double]$ToT = 0.75,
        [object[]]$ThicknessProfile,
        [double]$WidthRatio = 0.62,
        [double]$Droop = 0.14
    )
    $plates = New-Object System.Collections.Generic.List[object]
    for ($plateIdx = 0; $plateIdx -lt $Count; $plateIdx++) {
        $ratio = ($plateIdx + 0.5) / [double]$Count
        $spineT = $FromT + (($ToT - $FromT) * $ratio)
        $nodeIdx = [int][Math]::Round($spineT * ($Spine.Count - 1))
        if ($nodeIdx -lt 0) { $nodeIdx = 0 }
        if ($nodeIdx -ge $Spine.Count) { $nodeIdx = $Spine.Count - 1 }
        $node = $Spine[$nodeIdx]
        $halfWidth = (Get-DovahThicknessAt $ThicknessProfile $node.T) * $WidthRatio
        $sagX = $node.TangentX * $halfWidth * $Droop
        $sagY = $node.TangentY * $halfWidth * $Droop
        $plates.Add(@(
            (New-DovahPoint ($node.X - ($node.NormalX * $halfWidth)) ($node.Y - ($node.NormalY * $halfWidth))),
            (New-DovahPoint ($node.X + $sagX) ($node.Y + $sagY)),
            (New-DovahPoint ($node.X + ($node.NormalX * $halfWidth)) ($node.Y + ($node.NormalY * $halfWidth)))
        ))
    }
    return $plates
}

# ---------------------------------------------------------------------------- 3. SHADING
# The flat treatment signed off on 2026-08-02: solid tones inside a heavy outline, never a
# continuous gradient. A gradient reads as photographic noise at the 48px the game is played at.
function New-DovahPalette {
    param(
        [int[]]$Shadow    = @( 20, 19, 18 ),
        [int[]]$Dark      = @( 44, 41, 37 ),
        [int[]]$Mid       = @( 82, 77, 70 ),
        [int[]]$Light     = @(140, 133, 122 ),
        [int[]]$Keyline   = @( 11, 10,  9 ),
        [int[]]$RimLight  = @(172, 165, 152 ),
        [int[]]$Accent    = @(226, 92,  36 )
    )
    return [pscustomobject]@{
        Shadow   = (New-DovahColor $Shadow[0]   $Shadow[1]   $Shadow[2])
        Dark     = (New-DovahColor $Dark[0]     $Dark[1]     $Dark[2])
        Mid      = (New-DovahColor $Mid[0]      $Mid[1]      $Mid[2])
        Light    = (New-DovahColor $Light[0]    $Light[1]    $Light[2])
        Keyline  = (New-DovahColor $Keyline[0]  $Keyline[1]  $Keyline[2])
        RimLight = (New-DovahColor $RimLight[0] $RimLight[1] $RimLight[2])
        Accent   = (New-DovahColor $Accent[0]   $Accent[1]   $Accent[2])
    }
}

function Add-DovahKeyline {
    # A dark keyline is NOT optional on a RimWorld sprite - without one a coloured shape on
    # lit ground reads soft however good the silhouette is. It must SCALE WITH THE SHAPE:
    # a fixed pixel width stops being an outline and becomes the shape.
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [System.Drawing.Color]$Color,
        [double]$WidthFraction = 0.0135
    )
    $pen = New-DovahPen $Color $WidthFraction
    $Graphics.DrawPath($pen, $Path)
    $pen.Dispose()
}

function Add-DovahFlatFill {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [System.Drawing.Color]$Color
    )
    $brush = New-Object System.Drawing.SolidBrush $Color
    $Graphics.FillPath($brush, $Path)
    $brush.Dispose()
}

function Add-DovahCellShade {
    <#
      One flat shadow band along a chosen side of a shape, clipped to it. Two tones, hard
      edge - that is cell shading. Direction is a unit vector in frame space; the default
      lights from the top, which is what every other sprite in this mod assumes.
    #>
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [System.Drawing.Color]$ShadowColor,
        [double]$LightDirX = 0.0, [double]$LightDirY = -1.0,
        [double]$Coverage = 0.42
    )
    # NOTE: no ternary operator - this project targets Windows PowerShell 5.1, where `? :`
    # is a parser error, not a fallback.
    $bounds = $Path.GetBounds()
    $savedClip = $Graphics.Clip
    $Graphics.SetClip($Path)
    $brush = New-Object System.Drawing.SolidBrush $ShadowColor
    # shade the band of the bounding box facing AWAY from the light
    $shadeTop = $bounds.Y + ($bounds.Height * (1.0 - $Coverage))
    if ($LightDirY -gt 0) { $shadeTop = $bounds.Y }
    $shadeRect = New-Object System.Drawing.RectangleF (
        [float]$bounds.X, [float]$shadeTop,
        [float]$bounds.Width, [float]($bounds.Height * $Coverage))
    $Graphics.FillRectangle($brush, $shadeRect)
    $brush.Dispose()
    $Graphics.Clip = $savedClip
}

function Add-DovahRimLight {
    # On a dark creature over RimWorld's brown ground, value alone will not carry the
    # internal forms. A cool lit edge inside the keyline is what makes a near-black shape read.
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [System.Drawing.Color]$Color,
        [double]$WidthFraction = 0.0042,
        [int]$Alpha = 125
    )
    $pen = New-DovahPen (New-DovahColor $Color.R $Color.G $Color.B $Alpha) $WidthFraction
    $Graphics.DrawPath($pen, $Path)
    $pen.Dispose()
}

function Add-DovahPart {
    <#
      The standard three-pass treatment for one piece: keyline under, flat fill, rim over.
      Use this rather than hand-sequencing, so every part of every creature matches.
    #>
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        $Palette,
        [System.Drawing.Color]$FillColor,
        [double]$KeyWidth = 0.0135,
        [switch]$NoRim
    )
    Add-DovahKeyline -Graphics $Graphics -Path $Path -Color $Palette.Keyline -WidthFraction $KeyWidth
    Add-DovahFlatFill -Graphics $Graphics -Path $Path -Color $FillColor
    if (-not $NoRim) {
        Add-DovahRimLight -Graphics $Graphics -Path $Path -Color $Palette.RimLight -WidthFraction ($KeyWidth * 0.34)
    }
}

# ---------------------------------------------------------------------------- 4. VALIDATION
function Test-DovahSilhouette {
    <#
      REPLACES the "complexity gate" (fail if under N vertices) from the original proposal.
      That gate was rejected on evidence: on 2026-08-02 the MOST detailed dragon built - 22
      dorsal spines and 13 cross-bands - was the worst (it read as a beetle), and the fix was
      FEWER, LARGER features. A vertex count would have passed the beetle and failed the fix.

      Every failure in that session was the SILHOUETTE, so that is what this measures:

        Aspect        - width/height of the inked area
        FillDensity   - ink as a fraction of its bounding box. Above ~0.72 the shape is a
                        blob; a creature with limbs and wings should sit roughly 0.35-0.62.
        Concavities   - local minima in the radius-from-centroid profile. THIS is the number
                        that separates a dragon from a leaf: notches between wing fingers,
                        the waist, the neck. The redraw that failed had tips but no valleys.
        SmallScaleRead- ink retained after downsampling to 48px and re-thresholding, as a
                        fraction of the full-size ink. Very low means the shape dissolves at
                        play distance.

      Returns the measurements. It does NOT throw - it reports, and the caller decides. A
      validator that blocks on a number nobody has calibrated is how you end up tuning for
      the validator instead of for the picture.
    #>
    param(
        [System.Drawing.Bitmap]$Sprite,
        [int]$AlphaThreshold = 24
    )
    $spriteW = $Sprite.Width; $spriteH = $Sprite.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $spriteW, $spriteH
    $data = $Sprite.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buffer = New-Object byte[] ($stride * $spriteH)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    $Sprite.UnlockBits($data)

    $minX = $spriteW; $maxX = -1; $minY = $spriteH; $maxY = -1
    $inkCount = 0; $sumX = 0.0; $sumY = 0.0
    for ($rowIdx = 0; $rowIdx -lt $spriteH; $rowIdx++) {
        for ($colIdx = 0; $colIdx -lt $spriteW; $colIdx++) {
            if ([int]$buffer[($rowIdx * $stride) + ($colIdx * 4) + 3] -gt $AlphaThreshold) {
                $inkCount++; $sumX += $colIdx; $sumY += $rowIdx
                if ($colIdx -lt $minX) { $minX = $colIdx }
                if ($colIdx -gt $maxX) { $maxX = $colIdx }
                if ($rowIdx -lt $minY) { $minY = $rowIdx }
                if ($rowIdx -gt $maxY) { $maxY = $rowIdx }
            }
        }
    }
    if ($inkCount -eq 0) {
        return [pscustomobject]@{ Ink = 0; Aspect = 0; FillDensity = 0; Concavities = 0; SmallScaleRead = 0; Notes = @("EMPTY SPRITE") }
    }
    $spanX = $maxX - $minX + 1
    $spanY = $maxY - $minY + 1
    $centroidX = $sumX / $inkCount
    $centroidY = $sumY / $inkCount

    # radial profile -> count concave dips
    $rays = 180
    $radii = New-Object 'double[]' $rays
    for ($rayIdx = 0; $rayIdx -lt $rays; $rayIdx++) {
        $angle = ($rayIdx / [double]$rays) * 2.0 * [Math]::PI
        $dirX = [Math]::Cos($angle); $dirY = [Math]::Sin($angle)
        $maxReach = [Math]::Sqrt(($spanX * $spanX) + ($spanY * $spanY))
        $lastHit = 0.0
        for ($step = 1.0; $step -lt $maxReach; $step += 1.0) {
            $probeX = [int][Math]::Round($centroidX + ($dirX * $step))
            $probeY = [int][Math]::Round($centroidY + ($dirY * $step))
            if (($probeX -lt 0) -or ($probeY -lt 0) -or ($probeX -ge $spriteW) -or ($probeY -ge $spriteH)) { break }
            if ([int]$buffer[($probeY * $stride) + ($probeX * 4) + 3] -gt $AlphaThreshold) { $lastHit = $step }
        }
        $radii[$rayIdx] = $lastHit
    }
    $concavities = 0
    $window = 4
    for ($rayIdx = 0; $rayIdx -lt $rays; $rayIdx++) {
        $isDip = $true
        for ($offset = -$window; $offset -le $window; $offset++) {
            if ($offset -eq 0) { continue }
            $probeIdx = ((($rayIdx + $offset) % $rays) + $rays) % $rays
            if ($radii[$probeIdx] -lt $radii[$rayIdx]) { $isDip = $false; break }
        }
        if ($isDip) { $concavities++ }
    }

    # legibility at play distance
    $small = New-Object System.Drawing.Bitmap (48, 48, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $smallGfx = [System.Drawing.Graphics]::FromImage($small)
    $smallGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $smallGfx.Clear([System.Drawing.Color]::Transparent)
    $smallGfx.DrawImage($Sprite, (New-Object System.Drawing.Rectangle 0, 0, 48, 48))
    $smallGfx.Dispose()
    $smallInk = 0
    for ($rowIdx = 0; $rowIdx -lt 48; $rowIdx++) {
        for ($colIdx = 0; $colIdx -lt 48; $colIdx++) {
            if ($small.GetPixel($colIdx, $rowIdx).A -gt 90) { $smallInk++ }
        }
    }
    $small.Dispose()
    $expectedSmall = $inkCount * (48.0 / $spriteW) * (48.0 / $spriteH)
    $smallRead = if ($expectedSmall -gt 0) { $smallInk / $expectedSmall } else { 0 }

    $notes = New-Object System.Collections.Generic.List[string]
    $fillDensity = $inkCount / [double]($spanX * $spanY)
    if ($fillDensity -gt 0.72) { $notes.Add("FillDensity $([Math]::Round($fillDensity,3)) - reads as a blob; limbs and wings are not breaking the outline") }
    if ($concavities -lt 4)    { $notes.Add("Only $concavities concavities - too few valleys. Tips alone read as a leaf, not a creature") }
    if ($smallRead -lt 0.55)   { $notes.Add("SmallScaleRead $([Math]::Round($smallRead,3)) - the shape thins out at play distance") }
    if ($notes.Count -eq 0)    { $notes.Add("silhouette measurements are in range") }

    return [pscustomobject]@{
        Ink            = $inkCount
        Aspect         = [Math]::Round(($spanX / [double]$spanY), 3)
        FillDensity    = [Math]::Round($fillDensity, 3)
        Concavities    = $concavities
        SmallScaleRead = [Math]::Round($smallRead, 3)
        BoundingBox    = "$spanX x $spanY"
        Notes          = $notes
    }
}

# ---------------------------------------------------------------------------- output
function Save-DovahSprite {
    # Downsample the supersampled canvas once, at the end.
    param([string]$Path)
    $final = New-Object System.Drawing.Bitmap ([int]$script:DovahFrame), ([int]$script:DovahFrame),
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalGfx = [System.Drawing.Graphics]::FromImage($final)
    $finalGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $finalGfx.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $finalGfx.Clear([System.Drawing.Color]::Transparent)
    $finalGfx.DrawImage($script:DovahCanvas,
        (New-Object System.Drawing.Rectangle 0, 0, ([int]$script:DovahFrame), ([int]$script:DovahFrame)))
    $finalGfx.Dispose()
    if ($Path) { $final.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
    return $final
}

function New-DovahPreviewSheet {
    <#
      The standard judging sheet this project always uses: on dark, over LIT GROUND, the
      silhouette alone, and a play-distance strip. Two rounds were once lost to judging a
      translucent overlay on a dark backdrop, so the lit-ground cell is not optional.
    #>
    param(
        [System.Drawing.Bitmap]$Sprite,
        [string]$Title,
        [string]$Path,
        [System.Drawing.Bitmap]$CompareWith = $null,
        [string]$CompareLabel = "before"
    )
    $cell = 384
    $columns = if ($CompareWith) { 4 } else { 3 }
    $sheetWidth = ($cell * $columns) + (16 * ($columns + 1))
    $sheet = New-Object System.Drawing.Bitmap $sheetWidth, ($cell + 190),
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($sheet)
    $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gfx.Clear((New-DovahColor 28 28 30))
    $titleFont = New-Object System.Drawing.Font ("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
    $labelFont = New-Object System.Drawing.Font ("Segoe UI", 10)
    $textBrush = New-Object System.Drawing.SolidBrush (New-DovahColor 235 235 235)
    $dimBrush  = New-Object System.Drawing.SolidBrush (New-DovahColor 168 168 168)
    $gfx.DrawString($Title, $titleFont, $textBrush, 14, 10)

    $paintGround = {
        param($originX, $originY, $sizePx)
        for ($groundY = 0; $groundY -lt $sizePx; $groundY += 4) {
            for ($groundX = 0; $groundX -lt $sizePx; $groundX += 4) {
                $noise = Get-DovahHash01 ($groundX + $originX) ($groundY + $originY) 5.11
                $tint = [int](($noise - 0.5) * 26)
                $groundBrush = New-Object System.Drawing.SolidBrush (New-DovahColor (122 + $tint) (106 + $tint) (84 + $tint))
                $gfx.FillRectangle($groundBrush, ($originX + $groundX), ($originY + $groundY), 4, 4)
                $groundBrush.Dispose()
            }
        }
    }

    $rowTop = 50
    $columnX = 16
    if ($CompareWith) {
        $gfx.DrawString($CompareLabel, $labelFont, $dimBrush, $columnX, ($rowTop - 18))
        & $paintGround $columnX $rowTop $cell
        $gfx.DrawImage($CompareWith, $columnX, $rowTop, $cell, $cell)
        $columnX += ($cell + 16)
    }
    $gfx.DrawString("on dark", $labelFont, $dimBrush, $columnX, ($rowTop - 18))
    $gfx.DrawImage($Sprite, $columnX, $rowTop, $cell, $cell)
    $columnX += ($cell + 16)
    $gfx.DrawString("over lit ground - JUDGE HERE", $labelFont, $dimBrush, $columnX, ($rowTop - 18))
    & $paintGround $columnX $rowTop $cell
    $gfx.DrawImage($Sprite, $columnX, $rowTop, $cell, $cell)
    $columnX += ($cell + 16)
    $gfx.DrawString("silhouette - the real test", $labelFont, $dimBrush, $columnX, ($rowTop - 18))
    $whiteBrush = New-Object System.Drawing.SolidBrush (New-DovahColor 245 245 245)
    $gfx.FillRectangle($whiteBrush, $columnX, $rowTop, $cell, $cell)
    $whiteBrush.Dispose()
    $spriteSize = $Sprite.Width
    $silhouette = New-Object System.Drawing.Bitmap $spriteSize, $spriteSize,
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $srcRect = New-Object System.Drawing.Rectangle 0, 0, $spriteSize, $spriteSize
    $srcData = $Sprite.LockBits($srcRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $dstData = $silhouette.LockBits($srcRect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
                                    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $byteCount = $srcData.Stride * $spriteSize
    $srcBuffer = New-Object byte[] $byteCount
    $dstBuffer = New-Object byte[] $byteCount
    [System.Runtime.InteropServices.Marshal]::Copy($srcData.Scan0, $srcBuffer, 0, $byteCount)
    for ($byteIdx = 0; $byteIdx -lt $byteCount; $byteIdx += 4) {
        if ($srcBuffer[$byteIdx + 3] -gt 24) {
            $dstBuffer[$byteIdx] = 28; $dstBuffer[$byteIdx + 1] = 25
            $dstBuffer[$byteIdx + 2] = 25; $dstBuffer[$byteIdx + 3] = 255
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($dstBuffer, 0, $dstData.Scan0, $byteCount)
    $Sprite.UnlockBits($srcData); $silhouette.UnlockBits($dstData)
    $gfx.DrawImage($silhouette, $columnX, $rowTop, $cell, $cell)
    $silhouette.Dispose()

    $stripY = $rowTop + $cell + 30
    $gfx.DrawString("play distance:", $labelFont, $dimBrush, 16, ($stripY - 20))
    $stripX = 16
    foreach ($playSize in @(160, 120, 92, 68, 48)) {
        & $paintGround $stripX $stripY $playSize
        $gfx.DrawImage($Sprite, $stripX, $stripY, $playSize, $playSize)
        $gfx.DrawString(("{0}px" -f $playSize), $labelFont, $dimBrush, $stripX, ($stripY + $playSize + 2))
        $stripX += ($playSize + 18)
    }
    $gfx.Dispose()
    if ($Path) { $sheet.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
    return $sheet
}

Write-Verbose "DovahArtEngine loaded."
