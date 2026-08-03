# BuildFromMask.ps1 - generalised: traced outline + flat-banded interior.
#
# Same pipeline that produced Dovah001, parameterised:
#   DOVAH_REF  source image
#   DOVAH_MASK its mask (from TraceRef.ps1)
#   DOVAH_TAG  output name
#
# Aspect is PRESERVED - a long side-on creature letterboxes inside the square frame rather
# than being stretched to fill it.

Add-Type -AssemblyName System.Drawing

$SRC_IMG  = if ($env:DOVAH_REF)  { $env:DOVAH_REF }  else { "C:\Users\User\Downloads\Gemini_Generated_Image_8ykfbv8ykfbv8ykf.png" }
$TAG      = if ($env:DOVAH_TAG)  { $env:DOVAH_TAG }  else { "gem" }
# WHICH VIEW THIS REFERENCE IS. Defaults to "east" so every earlier call behaves exactly as
# before. It decides three things that were previously hardcoded to east:
#   the output filename, whether a mirrored WEST is emitted, and every caption on the sheet.
#
# ONLY EAST MIRRORS. A profile view seen from the other side is the same drawing flipped;
# a north or south view is a DIFFERENT PROJECTION and has no free partner - mirroring one
# would just produce the same rear view again under a name claiming it is something else.
#
# The captions are parameterised rather than left saying "east" with a note attached: this
# harness has already shipped a sheet asserting the wrong subject, and the rule that came out
# of it is RE-RENDER, DO NOT ANNOTATE. A caption is read as fact.
$VIEW     = if ($env:DOVAH_VIEW) { $env:DOVAH_VIEW } else { "east" }
$MIRRORS  = ($VIEW -eq "east")
$SCRATCH  = $PSScriptRoot
$MASK_SRC = Join-Path $SCRATCH ("{0}_mask.png" -f $TAG)
$OUT_DIR  = if ($env:DOVAH_DEST) { $env:DOVAH_DEST } else { $SCRATCH }
$REPORT   = Join-Path $SCRATCH ("build_{0}.txt" -f $TAG)
if (Test-Path $REPORT) { Remove-Item $REPORT }
function Say { param([string]$line) Add-Content -Path $REPORT -Value $line -Encoding UTF8 }

$FRAME  = 512.0
$MARGIN = 0.030
$SUPER  = 3

function RGB {
    param([int]$redVal, [int]$greenVal, [int]$blueVal, [int]$alphaVal = 255)
    $redVal   = [Math]::Max(0, [Math]::Min(255, $redVal))
    $greenVal = [Math]::Max(0, [Math]::Min(255, $greenVal))
    $blueVal  = [Math]::Max(0, [Math]::Min(255, $blueVal))
    $alphaVal = [Math]::Max(0, [Math]::Min(255, $alphaVal))
    return [System.Drawing.Color]::FromArgb($alphaVal, $redVal, $greenVal, $blueVal)
}
# ---- COLOUR MATCHING, not luminance banding.
# This source is already flat 3-colour art. Measured by tallying the image:
#     black outline (0,0,0)   body (104,64,0)   membrane (136,96,56)
# Banding by LUMINANCE merges two of them - body reads 68 and membrane 102 against a
# range stretched to 165 by anti-aliased edge pixels, so both land in the same band.
# Matching each pixel to its NEAREST source colour is exact for flat art, and it keeps
# our own palette independent of theirs.
#
# WHITE MUST BE IN THIS LIST. The mask fills interior holes so the outer trace is clean,
# which means the reference's own background gaps - between the near wing and the body,
# and between the wing scallops - end up INSIDE the silhouette. Without white as a
# candidate, every one of those gaps matched to its nearest available colour, and that is
# the membrane tan: distance 79043 against 124307 for the body brown. So all the gaps came
# out light, and the near wing read as though its texture were inverted.
#
# A filled hole is background, so it emits ALPHA 0 rather than a colour.
#
# ---- THESE ARE PER-REFERENCE. DERIVE THEM, DO NOT REUSE THEM. ----
#
# The values below were hand-written for the FIRST reference and are kept only as the default.
# Measured against the 2026-08-03 grounded reference they put 8.4% of the creature - 24,076
# pixels - within a coin flip of the wrong band, and 92% of those were torn between "body dark"
# and "body light". That renders as SPECKLE across flat surfaces, and the user reported it
# immediately as "extra shades on his back".
#
# The cause was NOT a missing tone, which was the obvious guess and was wrong. It is that
# (124,128,140) sits well ABOVE that reference's true light cluster of (110,111,120), so the
# dark/light boundary landed in a dense part of the distribution instead of in the empty gap
# between two populations.
#
# RUN Tools/FindPalette.ps1 (k-means over the creature's own pixels) FOR EVERY NEW REFERENCE
# and pass the result in. On the grounded reference a derived k=4 scored 1.7% against 8.4% -
# five times fewer ambiguous pixels - with no change to any other part of the pipeline.
#
# Format: "r,g,b;r,g,b;..."  DOVAH_OUT_ALPHA is a matching ";"-separated list of 0-255.
# The LAST entry is by convention the background/gap tone and emits alpha 0.
function Parse-Palette {
    param([string]$spec)
    $rows = @()
    foreach ($chunk in ($spec -split ';')) {
        $parts = $chunk -split ','
        $rows += , @( [int]$parts[0], [int]$parts[1], [int]$parts[2] )
    }
    return , $rows
}
$SOURCE_COLOURS = if ($env:DOVAH_SRC_PALETTE) { Parse-Palette $env:DOVAH_SRC_PALETTE } else { @(
    @(   0,   0,   0 ),   # outline
    @(  64,  64,  76 ),   # body dark
    @( 124, 128, 140 ),   # body light
    @( 255, 255, 255 )    # background / gaps
) }
# our palette, cool charcoal so a black creature separates from brown RimWorld soil
$OUR_COLOURS = if ($env:DOVAH_OUT_PALETTE) { Parse-Palette $env:DOVAH_OUT_PALETTE } else { @(
    @(  10,  10,  13 ),
    @(  52,  54,  64 ),
    @( 104, 108, 122 ),
    @(   0,   0,   0 )
) }
$OUR_ALPHA = if ($env:DOVAH_OUT_ALPHA) { @($env:DOVAH_OUT_ALPHA -split ';' | ForEach-Object { [int]$_ }) } else { @( 255, 255, 255, 0 ) }
if ($SOURCE_COLOURS.Count -ne $OUR_COLOURS.Count -or $OUR_COLOURS.Count -ne $OUR_ALPHA.Count) {
    Write-Output ("ABORT: palette lengths disagree - source {0}, out {1}, alpha {2}" -f `
        $SOURCE_COLOURS.Count, $OUR_COLOURS.Count, $OUR_ALPHA.Count)
    exit 1
}
$LUMA_SMOOTH = 0        # flat art needs no pre-blur; blurring would invent in-between tones
$K_LINE = RGB 10 9 8
$K_RIM  = RGB 176 156 120

function Hash01 {
    param([double]$seedA, [double]$seedB, [double]$salt = 37.719)
    $raw = [Math]::Sin(($seedA * 12.9898) + ($seedB * 78.233) + $salt) * 43758.5453
    return $raw - [Math]::Floor($raw)
}
function LoadScaled {
    param([string]$path, [int]$targetW, [int]$targetH)
    $bmp = [System.Drawing.Bitmap]::FromFile($path)
    $scaled = New-Object System.Drawing.Bitmap ($targetW, $targetH,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfxTmp = [System.Drawing.Graphics]::FromImage($scaled)
    $gfxTmp.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $gfxTmp.FillRectangle($whiteBrush, 0, 0, $targetW, $targetH)
    $whiteBrush.Dispose()
    $gfxTmp.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $targetW, $targetH))
    $gfxTmp.Dispose(); $bmp.Dispose()
    $rect = New-Object System.Drawing.Rectangle 0, 0, $targetW, $targetH
    $data = $scaled.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buf = New-Object byte[] ($data.Stride * $targetH)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
    $scaled.UnlockBits($data)
    $result = @{ Stride = $data.Stride; Bytes = $buf }
    $scaled.Dispose()
    return $result
}

# mask dictates the working resolution
$maskBmp = [System.Drawing.Bitmap]::FromFile($MASK_SRC)
$imgW = $maskBmp.Width; $imgH = $maskBmp.Height
$mRect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
$mData = $maskBmp.LockBits($mRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                           [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mBuf = New-Object byte[] ($mData.Stride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($mData.Scan0, $mBuf, 0, $mBuf.Length)
$mStride = $mData.Stride
$maskBmp.UnlockBits($mData); $maskBmp.Dispose()

$srcScaled = LoadScaled $SRC_IMG $imgW $imgH
Say ("working at {0} x {1}" -f $imgW, $imgH)

# ---------------------------------------------------------------- FULL-RES colour source
# The mask is downscaled so the flood fill and trace are affordable. Colour classification
# must NOT be: where the wing's black struts crowd together, a 1408 -> 620 downscale
# averages strut and membrane into a mid-brown, which then matches BODY instead of
# MEMBRANE. That is what turned the outer half of the far wing dark.
# Trace on the small copy; read colour from the original.
$fullBmp = [System.Drawing.Bitmap]::FromFile($SRC_IMG)
$fullW = $fullBmp.Width; $fullH = $fullBmp.Height
$fRect = New-Object System.Drawing.Rectangle 0, 0, $fullW, $fullH
$fData = $fullBmp.LockBits($fRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                           [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$fStride = $fData.Stride
$fBytes = New-Object byte[] ($fStride * $fullH)
[System.Runtime.InteropServices.Marshal]::Copy($fData.Scan0, $fBytes, 0, $fBytes.Length)
$fullBmp.UnlockBits($fData); $fullBmp.Dispose()
$maskToFull = $fullW / [double]$imgW
Say ("colour source : {0} x {1}  (mask->full scale {2:N4})" -f $fullW, $fullH, $maskToFull)

$solid = New-Object 'bool[]' ($imgW * $imgH)
$srcR = New-Object 'int[]' ($imgW * $imgH)
$srcG = New-Object 'int[]' ($imgW * $imgH)
$srcB = New-Object 'int[]' ($imgW * $imgH)
$bandOf = New-Object 'int[]' ($imgW * $imgH)
$solidCount = 0
$bandTally = New-Object 'int[]' $SOURCE_COLOURS.Count
for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
    for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
        $flatIdx = ($rowIdx * $imgW) + $colIdx
        $solid[$flatIdx] = ([int]$mBuf[($rowIdx * $mStride) + ($colIdx * 4) + 1] -lt 128)
        $sBase = ($rowIdx * $srcScaled.Stride) + ($colIdx * 4)
        $srcR[$flatIdx] = [int]$srcScaled.Bytes[$sBase + 2]
        $srcG[$flatIdx] = [int]$srcScaled.Bytes[$sBase + 1]
        $srcB[$flatIdx] = [int]$srcScaled.Bytes[$sBase]
        if (-not $solid[$flatIdx]) { $bandOf[$flatIdx] = -1; continue }
        $solidCount++
        # nearest source colour, squared RGB distance
        $bestBand = 0; $bestDist = [double]::MaxValue
        for ($candIdx = 0; $candIdx -lt $SOURCE_COLOURS.Count; $candIdx++) {
            $dR = $srcR[$flatIdx] - $SOURCE_COLOURS[$candIdx][0]
            $dG = $srcG[$flatIdx] - $SOURCE_COLOURS[$candIdx][1]
            $dB = $srcB[$flatIdx] - $SOURCE_COLOURS[$candIdx][2]
            $dist = ($dR * $dR) + ($dG * $dG) + ($dB * $dB)
            if ($dist -lt $bestDist) { $bestDist = $dist; $bestBand = $candIdx }
        }
        $bandOf[$flatIdx] = $bestBand
        $bandTally[$bestBand]++
    }
}
for ($bandIdx = 0; $bandIdx -lt $SOURCE_COLOURS.Count; $bandIdx++) {
    Say ("band {0} ({1},{2},{3}) : {4} px  {5:N1}%" -f $bandIdx, `
        $SOURCE_COLOURS[$bandIdx][0], $SOURCE_COLOURS[$bandIdx][1], $SOURCE_COLOURS[$bandIdx][2], `
        $bandTally[$bandIdx], (100.0 * $bandTally[$bandIdx] / [Math]::Max(1,$solidCount)))
}
# ---------------------------------------------------------------- trace
function Trace-Boundary {
    param([bool[]]$region, [int]$regionW, [int]$regionH)
    $startX = -1; $startY = -1
    for ($rowIdx = 0; ($rowIdx -lt $regionH) -and ($startY -lt 0); $rowIdx++) {
        for ($colIdx = 0; $colIdx -lt $regionW; $colIdx++) {
            if ($region[($rowIdx * $regionW) + $colIdx]) { $startX = $colIdx; $startY = $rowIdx; break }
        }
    }
    $contour = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    if ($startY -lt 0) { return $contour }
    $stepX = @(-1, -1,  0,  1, 1, 1, 0, -1)
    $stepY = @( 0, -1, -1, -1, 0, 1, 1,  1)
    $atX = $startX; $atY = $startY; $backDir = 0
    $maxSteps = $regionW * $regionH * 4
    $stepCount = 0
    do {
        $contour.Add((New-Object System.Drawing.PointF ([float]$atX, [float]$atY)))
        $found = $false
        for ($probe = 1; $probe -le 8; $probe++) {
            $dirIdx = ($backDir + $probe) % 8
            $tryX = $atX + $stepX[$dirIdx]; $tryY = $atY + $stepY[$dirIdx]
            if (($tryX -lt 0) -or ($tryY -lt 0) -or ($tryX -ge $regionW) -or ($tryY -ge $regionH)) { continue }
            if ($region[($tryY * $regionW) + $tryX]) {
                $backDir = ($dirIdx + 5) % 8
                $atX = $tryX; $atY = $tryY; $found = $true; break
            }
        }
        if (-not $found) { break }
        $stepCount++
    } while ((($atX -ne $startX) -or ($atY -ne $startY)) -and ($stepCount -lt $maxSteps))
    return $contour
}
function Smooth-Closed {
    param([System.Collections.Generic.List[System.Drawing.PointF]]$points, [int]$halfWindow, [int]$passes)
    $working = $points
    for ($passIdx = 0; $passIdx -lt $passes; $passIdx++) {
        $next = New-Object System.Collections.Generic.List[System.Drawing.PointF]
        $count = $working.Count
        for ($idx = 0; $idx -lt $count; $idx++) {
            $sumX = 0.0; $sumY = 0.0; $taken = 0
            for ($offset = -$halfWindow; $offset -le $halfWindow; $offset++) {
                $pickIdx = (($idx + $offset) % $count + $count) % $count
                $sumX += $working[$pickIdx].X; $sumY += $working[$pickIdx].Y; $taken++
            }
            $next.Add((New-Object System.Drawing.PointF ([float]($sumX / $taken), [float]($sumY / $taken))))
        }
        $working = $next
    }
    return $working
}
function Simplify-Contour {
    param([System.Collections.Generic.List[System.Drawing.PointF]]$points, [double]$tolerance)
    if ($points.Count -lt 3) { return $points }
    $keep = New-Object 'bool[]' $points.Count
    $keep[0] = $true; $keep[$points.Count - 1] = $true
    $pending = New-Object System.Collections.Generic.Stack[object]
    $pending.Push(@(0, ($points.Count - 1)))
    while ($pending.Count -gt 0) {
        $segment = $pending.Pop()
        $fromIdx = $segment[0]; $toIdx = $segment[1]
        if (($toIdx - $fromIdx) -lt 2) { continue }
        $ax = $points[$fromIdx].X; $ay = $points[$fromIdx].Y
        $bx = $points[$toIdx].X;   $by = $points[$toIdx].Y
        $segDX = $bx - $ax; $segDY = $by - $ay
        $segLen = [Math]::Sqrt(($segDX * $segDX) + ($segDY * $segDY))
        $worstDist = -1.0; $worstIdx = -1
        for ($probeIdx = $fromIdx + 1; $probeIdx -lt $toIdx; $probeIdx++) {
            $px = $points[$probeIdx].X; $py = $points[$probeIdx].Y
            if ($segLen -le 0.0) { $dist = [Math]::Sqrt((($px-$ax)*($px-$ax)) + (($py-$ay)*($py-$ay))) }
            else { $dist = [Math]::Abs((($segDY * $px) - ($segDX * $py) + ($bx * $ay) - ($by * $ax))) / $segLen }
            if ($dist -gt $worstDist) { $worstDist = $dist; $worstIdx = $probeIdx }
        }
        if (($worstDist -gt $tolerance) -and ($worstIdx -gt 0)) {
            $keep[$worstIdx] = $true
            $pending.Push(@($fromIdx, $worstIdx)); $pending.Push(@($worstIdx, $toIdx))
        }
    }
    $result = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($idx = 0; $idx -lt $points.Count; $idx++) { if ($keep[$idx]) { $result.Add($points[$idx]) } }
    return $result
}

$rawContour = Trace-Boundary $solid $imgW $imgH
$smoothed = Smooth-Closed $rawContour 1 1
$outline  = Simplify-Contour $smoothed 0.55
Say ("contour {0} -> simplified {1} points" -f $rawContour.Count, $outline.Count)
if ($outline.Count -lt 24) { Say "!! ABORT"; Write-Output "ABORT"; exit 1 }

$bMinX = [double]::MaxValue; $bMaxX = [double]::MinValue
$bMinY = [double]::MaxValue; $bMaxY = [double]::MinValue
foreach ($pointVal in $rawContour) {
    if ($pointVal.X -lt $bMinX) { $bMinX = $pointVal.X }
    if ($pointVal.X -gt $bMaxX) { $bMaxX = $pointVal.X }
    if ($pointVal.Y -lt $bMinY) { $bMinY = $pointVal.Y }
    if ($pointVal.Y -gt $bMaxY) { $bMaxY = $pointVal.Y }
}
$usable   = 1.0 - (2.0 * $MARGIN)
$fitScale = [Math]::Min(($usable / ($bMaxX - $bMinX)), ($usable / ($bMaxY - $bMinY)))
$offX = 0.5 - ((($bMinX + $bMaxX) * 0.5) * $fitScale)
$offY = 0.5 - ((($bMinY + $bMaxY) * 0.5) * $fitScale)
Say ("fit scale {0:N5}" -f $fitScale)

# ---------------------------------------------------------------- shading
$outSize = [int]$FRAME
$shaded = New-Object System.Drawing.Bitmap ($outSize, $outSize,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$shRect = New-Object System.Drawing.Rectangle 0, 0, $outSize, $outSize
$shData = $shaded.LockBits($shRect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
                           [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$shBuf = New-Object byte[] ($shData.Stride * $outSize)
# NEAREST-NEIGHBOUR sampling of the band index, not bilinear of a colour. Interpolating
# between two flat regions would invent an in-between tone along every internal edge, which
# is exactly the soft mush this build is meant to avoid. The outline path carries the
# antialiasing; the interior stays flat by construction.
for ($outY = 0; $outY -lt $outSize; $outY++) {
    $srcY = ((($outY + 0.5) / $FRAME) - $offY) / $fitScale
    $sy = [int][Math]::Round($srcY)
    $fullY = [int][Math]::Round($srcY * $maskToFull)
    for ($outX = 0; $outX -lt $outSize; $outX++) {
        $srcX = ((($outX + 0.5) / $FRAME) - $offX) / $fitScale
        $sx = [int][Math]::Round($srcX)
        $fullX = [int][Math]::Round($srcX * $maskToFull)
        $pixBase = ($outY * $shData.Stride) + ($outX * 4)
        if (($sx -lt 0) -or ($sy -lt 0) -or ($sx -ge $imgW) -or ($sy -ge $imgH)) { $shBuf[$pixBase + 3] = 0; continue }
        # classify from the ORIGINAL pixels, not the downscaled copy
        if (($fullX -lt 0) -or ($fullY -lt 0) -or ($fullX -ge $fullW) -or ($fullY -ge $fullH)) {
            $shBuf[$pixBase + 3] = 0; continue
        }
        $fBase = ($fullY * $fStride) + ($fullX * 4)
        $pxR = [int]$fBytes[$fBase + 2]; $pxG = [int]$fBytes[$fBase + 1]; $pxB = [int]$fBytes[$fBase]
        $bandIdx = 0; $bestDist = [double]::MaxValue
        for ($candIdx = 0; $candIdx -lt $SOURCE_COLOURS.Count; $candIdx++) {
            $dR = $pxR - $SOURCE_COLOURS[$candIdx][0]
            $dG = $pxG - $SOURCE_COLOURS[$candIdx][1]
            $dB = $pxB - $SOURCE_COLOURS[$candIdx][2]
            $dist = ($dR * $dR) + ($dG * $dG) + ($dB * $dB)
            if ($dist -lt $bestDist) { $bestDist = $dist; $bandIdx = $candIdx }
        }
        $shBuf[$pixBase]     = [byte]$OUR_COLOURS[$bandIdx][2]
        $shBuf[$pixBase + 1] = [byte]$OUR_COLOURS[$bandIdx][1]
        $shBuf[$pixBase + 2] = [byte]$OUR_COLOURS[$bandIdx][0]
        $shBuf[$pixBase + 3] = [byte]$OUR_ALPHA[$bandIdx]
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($shBuf, 0, $shData.Scan0, $shBuf.Length)
$shaded.UnlockBits($shData)

# ---------------------------------------------------------------- compose
$device = [int]($FRAME * $SUPER)
$canvas = New-Object System.Drawing.Bitmap ($device, $device,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($canvas)
$gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gfx.Clear([System.Drawing.Color]::Transparent)
$devicePts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
foreach ($pointVal in $outline) {
    $fxOut = ($pointVal.X * $fitScale) + $offX
    $fyOut = ($pointVal.Y * $fitScale) + $offY
    $devicePts.Add((New-Object System.Drawing.PointF ([float]($fxOut * $FRAME * $SUPER), [float]($fyOut * $FRAME * $SUPER))))
}
$outlinePath = New-Object System.Drawing.Drawing2D.GraphicsPath
$outlinePath.AddClosedCurve($devicePts.ToArray(), 0.18)
$keyPen = New-Object System.Drawing.Pen ($K_LINE, [float](0.0165 * $FRAME * $SUPER))
$keyPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$gfx.DrawPath($keyPen, $outlinePath); $keyPen.Dispose()
$savedClip = $gfx.Clip
$gfx.SetClip($outlinePath)
$gfx.DrawImage($shaded, (New-Object System.Drawing.Rectangle 0, 0, $device, $device))
$gfx.Clip = $savedClip
$rimPen = New-Object System.Drawing.Pen ((RGB $K_RIM.R $K_RIM.G $K_RIM.B 120), [float](0.0044 * $FRAME * $SUPER))
$rimPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$gfx.DrawPath($rimPen, $outlinePath); $rimPen.Dispose()
$edgePen = New-Object System.Drawing.Pen ((RGB $K_LINE.R $K_LINE.G $K_LINE.B 215), [float](0.0040 * $FRAME * $SUPER))
$edgePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$gfx.DrawPath($edgePen, $outlinePath); $edgePen.Dispose()
$outlinePath.Dispose(); $gfx.Dispose(); $shaded.Dispose()

$final = New-Object System.Drawing.Bitmap ([int]$FRAME, [int]$FRAME,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$finalGfx = [System.Drawing.Graphics]::FromImage($final)
$finalGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$finalGfx.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$finalGfx.Clear([System.Drawing.Color]::Transparent)
$finalGfx.DrawImage($canvas, (New-Object System.Drawing.Rectangle 0, 0, ([int]$FRAME), ([int]$FRAME)))
$finalGfx.Dispose(); $canvas.Dispose()
$final.Save((Join-Path $OUT_DIR ("{0}_{1}.png" -f $TAG, $VIEW)), [System.Drawing.Imaging.ImageFormat]::Png)
Say ("wrote         : {0}_{1}.png" -f $TAG, $VIEW)

# mirrored copy = the WEST view, free - EAST ONLY. See $MIRRORS at the top.
$mirrored = $null
if ($MIRRORS) {
    $mirrored = New-Object System.Drawing.Bitmap $final
    $mirrored.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
    $mirrored.Save((Join-Path $OUT_DIR ("{0}_west.png" -f $TAG)), [System.Drawing.Imaging.ImageFormat]::Png)
    Say ("wrote         : {0}_west.png  (mirrored)" -f $TAG)
} else {
    Say ("no mirror     : '{0}' is its own projection, west is not derivable from it" -f $VIEW)
}

# ---------------------------------------------------------------- preview
$CELL = 384
$sheet = New-Object System.Drawing.Bitmap ((($CELL * 3) + 48), (($CELL + 190)),
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sheetGfx = [System.Drawing.Graphics]::FromImage($sheet)
$sheetGfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$sheetGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sheetGfx.Clear((RGB 28 28 30))
$titleFont = New-Object System.Drawing.Font ("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
$labelFont = New-Object System.Drawing.Font ("Segoe UI", 10)
$textBrush = New-Object System.Drawing.SolidBrush (RGB 235 235 235)
$dimBrush  = New-Object System.Drawing.SolidBrush (RGB 168 168 168)
$viewBlurb = switch ($VIEW) {
    "east"  { "a SIDE view - west comes free by mirroring it" }
    "west"  { "a SIDE view" }
    "north" { "the view from BEHIND - back of the skull, head up. Its own drawing; nothing mirrors to it" }
    "south" { "the view from the FRONT - face visible, head up. Its own drawing; nothing mirrors to it" }
    default { "view '$VIEW'" }
}
$sheetGfx.DrawString(("TRACED FROM YOUR REFERENCE - {0} - {1}" -f $TAG, $viewBlurb),
    $titleFont, $textBrush, 14, 10)
function Paint-Ground {
    param([int]$originX, [int]$originY, [int]$sizePx)
    for ($groundY = 0; $groundY -lt $sizePx; $groundY += 4) {
        for ($groundX = 0; $groundX -lt $sizePx; $groundX += 4) {
            $noise = Hash01 ($groundX + $originX) ($groundY + $originY) 5.11
            $tint = [int](($noise - 0.5) * 26)
            $groundBrush = New-Object System.Drawing.SolidBrush (RGB (122 + $tint) (106 + $tint) (84 + $tint))
            $sheetGfx.FillRectangle($groundBrush, ($originX + $groundX), ($originY + $groundY), 4, 4)
            $groundBrush.Dispose()
        }
    }
}
$rowTop = 50
$sheetGfx.DrawString("your reference", $labelFont, $dimBrush, 14, ($rowTop - 18))
$whiteBrush2 = New-Object System.Drawing.SolidBrush (RGB 245 245 245)
$sheetGfx.FillRectangle($whiteBrush2, 14, $rowTop, $CELL, $CELL)
$whiteBrush2.Dispose()
$refShow = [System.Drawing.Bitmap]::FromFile($SRC_IMG)
$refFit = [Math]::Min(($CELL / [double]$refShow.Width), ($CELL / [double]$refShow.Height))
$rw = [int]($refShow.Width * $refFit); $rh = [int]($refShow.Height * $refFit)
$sheetGfx.DrawImage($refShow, (14 + [int](($CELL - $rw) / 2)), ($rowTop + [int](($CELL - $rh) / 2)), $rw, $rh)
$refShow.Dispose()
$cell2X = 14 + $CELL + 16
$sheetGfx.DrawString(("OURS - {0} - over lit ground" -f $VIEW), $labelFont, $dimBrush, $cell2X, ($rowTop - 18))
Paint-Ground $cell2X $rowTop $CELL
$sheetGfx.DrawImage($final, $cell2X, $rowTop, $CELL, $CELL)
# Cell 3 is the mirrored partner when there IS one, and the traced MASK when there is not -
# an empty third cell would read as something having failed, and the mask is the one artefact
# worth looking at anyway.
$cell3X = $cell2X + $CELL + 16
if ($MIRRORS) {
    $sheetGfx.DrawString("west - mirrored, free", $labelFont, $dimBrush, $cell3X, ($rowTop - 18))
    Paint-Ground $cell3X $rowTop $CELL
    $sheetGfx.DrawImage($mirrored, $cell3X, $rowTop, $CELL, $CELL)
} else {
    $sheetGfx.DrawString("the traced silhouette (no mirror for this view)", $labelFont, $dimBrush, $cell3X, ($rowTop - 18))
    $maskShow = [System.Drawing.Bitmap]::FromFile($MASK_SRC)
    $sheetGfx.DrawImage($maskShow, $cell3X, $rowTop, $CELL, $CELL)
    $maskShow.Dispose()
}
$stripY = $rowTop + $CELL + 30
$sheetGfx.DrawString("play distance:", $labelFont, $dimBrush, 14, ($stripY - 20))
$stripX = 14
foreach ($playSize in @(160, 120, 92, 68, 52)) {
    Paint-Ground $stripX $stripY $playSize
    $sheetGfx.DrawImage($final, $stripX, $stripY, $playSize, $playSize)
    $sheetGfx.DrawString(("{0}px" -f $playSize), $labelFont, $dimBrush, $stripX, ($stripY + $playSize + 2))
    $stripX += ($playSize + 18)
}
$sheetGfx.Dispose()
$sheet.Save((Join-Path $OUT_DIR ("{0}_preview.png" -f $TAG)), [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose(); $final.Dispose()
if ($null -ne $mirrored) { $mirrored.Dispose() }
Say "DONE"
Write-Output "DONE"
