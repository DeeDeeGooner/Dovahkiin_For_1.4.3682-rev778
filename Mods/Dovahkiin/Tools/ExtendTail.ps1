# ExtendTail.ps1 - the reference's tail runs off the bottom of the card, so the traced
# silhouette ends in a straight cut and the sprite gets a hard horizontal bar.
#
# Fixed in SOURCE space rather than in the mask or the sprite. Extending the reference image
# means trace and build both run completely unchanged - no new code paths, no second way for
# the pipeline to behave. The extended tail is drawn by REFLECTING the reference's own pixels
# upward, so the scale texture continues instead of becoming a flat blob.

Add-Type -AssemblyName System.Drawing

$SCRATCH  = $PSScriptRoot
$SRC_IMG  = Join-Path $SCRATCH "alduin20_src.png"
$MASK_SRC = Join-Path $SCRATCH "ald20_mask_125.png"
$OUT_IMG  = Join-Path $SCRATCH "alduin20_tailed.png"
$REPORT   = Join-Path $SCRATCH "extend_tail.txt"
if (Test-Path $REPORT) { Remove-Item $REPORT }
function Say { param([string]$line) Add-Content -Path $REPORT -Value $line -Encoding UTF8 }

# how far past the cut the tail continues, as a fraction of the ORIGINAL image height
$EXTEND_FRACTION = 0.34
$TIP_WIDTH_RATIO = 0.10    # half-width at the very tip, as a fraction of the width at the cut
$SWAY            = 0.055   # lateral drift of the tail tip, fraction of image width

# ---------------------------------------------------------------- load mask, find the cut
$maskBmp = [System.Drawing.Bitmap]::FromFile($MASK_SRC)
$maskW = $maskBmp.Width; $maskH = $maskBmp.Height
$mRect = New-Object System.Drawing.Rectangle 0, 0, $maskW, $maskH
$mData = $maskBmp.LockBits($mRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                           [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mBuf = New-Object byte[] ($mData.Stride * $maskH)
[System.Runtime.InteropServices.Marshal]::Copy($mData.Scan0, $mBuf, 0, $mBuf.Length)
$mStride = $mData.Stride
$maskBmp.UnlockBits($mData); $maskBmp.Dispose()

# Find the TAIL, not merely the bottom-most ink. The mask carries a spurious full-width
# hairline at its very bottom (the card's own edge surviving the threshold), and taking the
# lowest solid row reported a "tail" 596px wide - the entire image. The tail is the bottom-most
# row whose CENTRE-CONTAINING RUN is narrow.
$MAX_TAIL_FRACTION = 0.20
$centreColProbe = [int]($maskW / 2)
$cutRow = -1
for ($rowIdx = $maskH - 1; $rowIdx -ge 0; $rowIdx--) {
    if ([int]$mBuf[($rowIdx * $mStride) + ($centreColProbe * 4) + 1] -ge 128) { continue }
    $probeLeft = $centreColProbe
    while (($probeLeft -gt 0) -and ([int]$mBuf[($rowIdx * $mStride) + (($probeLeft - 1) * 4) + 1] -lt 128)) { $probeLeft-- }
    $probeRight = $centreColProbe
    while (($probeRight -lt ($maskW - 1)) -and ([int]$mBuf[($rowIdx * $mStride) + (($probeRight + 1) * 4) + 1] -lt 128)) { $probeRight++ }
    $runWidth = $probeRight - $probeLeft + 1
    if (($runWidth / [double]$maskW) -le $MAX_TAIL_FRACTION) { $cutRow = $rowIdx; break }
}
if ($cutRow -lt 0) { Say "!! ABORT: no narrow centre run found - is the tail actually at the centre?"; Write-Output "ABORT"; exit 1 }

# the tail is the solid run nearest the horizontal centre on the cut row
$centreCol = [int]($maskW / 2)
$runLeft = -1; $runRight = -1
if ([int]$mBuf[($cutRow * $mStride) + ($centreCol * 4) + 1] -lt 128) {
    $runLeft = $centreCol
    while (($runLeft -gt 0) -and ([int]$mBuf[($cutRow * $mStride) + (($runLeft - 1) * 4) + 1] -lt 128)) { $runLeft-- }
    $runRight = $centreCol
    while (($runRight -lt ($maskW - 1)) -and ([int]$mBuf[($cutRow * $mStride) + (($runRight + 1) * 4) + 1] -lt 128)) { $runRight++ }
} else {
    # centre column is background - take the run whose midpoint is closest to centre
    $bestDistance = [int]::MaxValue
    $scanIdx = 0
    while ($scanIdx -lt $maskW) {
        if ([int]$mBuf[($cutRow * $mStride) + ($scanIdx * 4) + 1] -lt 128) {
            $spanStart = $scanIdx
            while (($scanIdx -lt $maskW) -and ([int]$mBuf[($cutRow * $mStride) + ($scanIdx * 4) + 1] -lt 128)) { $scanIdx++ }
            $spanEnd = $scanIdx - 1
            $distance = [Math]::Abs((($spanStart + $spanEnd) / 2) - $centreCol)
            if ($distance -lt $bestDistance) { $bestDistance = $distance; $runLeft = $spanStart; $runRight = $spanEnd }
        } else { $scanIdx++ }
    }
}
Say ("mask          : {0} x {1}" -f $maskW, $maskH)
Say ("cut row       : {0}  (bottom-most solid row)" -f $cutRow)
Say ("tail run      : x {0}..{1}  width {2}px  centre {3:N1}" -f $runLeft, $runRight, ($runRight - $runLeft + 1), (($runLeft + $runRight) / 2.0))

# ---------------------------------------------------------------- load the source
$srcBmp = [System.Drawing.Bitmap]::FromFile($SRC_IMG)
$srcW = $srcBmp.Width; $srcH = $srcBmp.Height
$sRect = New-Object System.Drawing.Rectangle 0, 0, $srcW, $srcH
$sData = $srcBmp.LockBits($sRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sStride = $sData.Stride
$sBuf = New-Object byte[] ($sStride * $srcH)
[System.Runtime.InteropServices.Marshal]::Copy($sData.Scan0, $sBuf, 0, $sBuf.Length)
$srcBmp.UnlockBits($sData); $srcBmp.Dispose()

$maskToSrc = $srcW / [double]$maskW
$cutRowSrc     = $cutRow * $maskToSrc
$tailCentreSrc = (($runLeft + $runRight) / 2.0) * $maskToSrc
$tailHalfSrc   = ((($runRight - $runLeft) + 1) / 2.0) * $maskToSrc
Say ("source        : {0} x {1}   mask->src {2:N4}" -f $srcW, $srcH, $maskToSrc)
Say ("cut in source : y {0:N1}   tail centre x {1:N1}   half-width {2:N1}px" -f $cutRowSrc, $tailCentreSrc, $tailHalfSrc)

# background colour: sample the parchment from a corner well inside the frame
$probeBase = ([int]($srcH * 0.06) * $sStride) + ([int]($srcW * 0.06) * 4)
$bgB = [int]$sBuf[$probeBase]; $bgG = [int]$sBuf[$probeBase + 1]; $bgR = [int]$sBuf[$probeBase + 2]
Say ("parchment     : ({0},{1},{2})" -f $bgR, $bgG, $bgB)

# ---------------------------------------------------------------- build the extended canvas
$extendPx = [int]($srcH * $EXTEND_FRACTION)
$newH = $srcH + $extendPx
$outBmp = New-Object System.Drawing.Bitmap ($srcW, $newH,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$oRect = New-Object System.Drawing.Rectangle 0, 0, $srcW, $newH
$oData = $outBmp.LockBits($oRect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$oStride = $oData.Stride
$oBuf = New-Object byte[] ($oStride * $newH)

# fill everything with parchment, then copy the original in
for ($rowIdx = 0; $rowIdx -lt $newH; $rowIdx++) {
    for ($colIdx = 0; $colIdx -lt $srcW; $colIdx++) {
        $outBase = ($rowIdx * $oStride) + ($colIdx * 4)
        $oBuf[$outBase] = [byte]$bgB; $oBuf[$outBase + 1] = [byte]$bgG
        $oBuf[$outBase + 2] = [byte]$bgR; $oBuf[$outBase + 3] = 255
    }
}
# Copy the card's INTERIOR only. Once the tail runs past the card, the frame's bottom line
# crosses it - so the frame and the dragon become one blob and "largest blob" keeps both.
# Dropping the frame here removes the problem at its source instead of fighting it downstream,
# and it means the trace no longer needs an inset at all.
$FRAME_INSET = 26
for ($rowIdx = $FRAME_INSET; $rowIdx -lt ($srcH - $FRAME_INSET); $rowIdx++) {
    for ($colIdx = $FRAME_INSET; $colIdx -lt ($srcW - $FRAME_INSET); $colIdx++) {
        $inBase  = ($rowIdx * $sStride) + ($colIdx * 4)
        $outBase = ($rowIdx * $oStride) + ($colIdx * 4)
        $oBuf[$outBase]     = $sBuf[$inBase]
        $oBuf[$outBase + 1] = $sBuf[$inBase + 1]
        $oBuf[$outBase + 2] = $sBuf[$inBase + 2]
        $oBuf[$outBase + 3] = 255
    }
}

# ---------------------------------------------------------------- draw the continued tail
# For each new row, taper the half-width and drift the centre. Sample by REFLECTING the
# reference upward from the cut, stretched horizontally to the narrowing width - so the scale
# texture carries on down the tail rather than the extension reading as a smooth cone.
$tailStartY = [int][Math]::Floor($cutRowSrc) - 2
$tailEndY   = $newH - 1
$tailSpan   = [double]($tailEndY - $tailStartY)
$writtenPx = 0
for ($rowIdx = $tailStartY; $rowIdx -le $tailEndY; $rowIdx++) {
    $along = ($rowIdx - $tailStartY) / $tailSpan
    if ($along -lt 0.0) { $along = 0.0 }
    # taper: broad at the root, coming to a point. Power < 1 keeps it thick then snaps in.
    $halfWidth = $tailHalfSrc * (((1.0 - $TIP_WIDTH_RATIO) * [Math]::Pow((1.0 - $along), 0.78)) + $TIP_WIDTH_RATIO)
    if ($along -ge 0.995) { $halfWidth = $halfWidth * 0.35 }
    # a lazy sway so it reads as alive rather than as a spike
    $centreX = $tailCentreSrc + ($SWAY * $srcW * [Math]::Sin($along * 2.05) * [Math]::Pow($along, 1.25))
    # reflect upward, and slow the reflection so the pattern does not repeat too fast
    $sampleRow = [int]($cutRowSrc - (($rowIdx - $cutRowSrc) * 0.85))
    if ($sampleRow -lt 2) { $sampleRow = 2 }
    if ($sampleRow -ge $srcH) { $sampleRow = $srcH - 1 }
    $stretch = $tailHalfSrc / [Math]::Max(1.0, $halfWidth)

    $fromX = [int][Math]::Floor($centreX - $halfWidth)
    $toX   = [int][Math]::Ceiling($centreX + $halfWidth)
    for ($colIdx = $fromX; $colIdx -le $toX; $colIdx++) {
        if (($colIdx -lt 0) -or ($colIdx -ge $srcW) -or ($rowIdx -lt 0) -or ($rowIdx -ge $newH)) { continue }
        $sampleX = [int]($tailCentreSrc + (($colIdx - $centreX) * $stretch))
        if (($sampleX -lt 0) -or ($sampleX -ge $srcW)) { continue }
        $inBase  = ($sampleRow * $sStride) + ($sampleX * 4)
        $outBase = ($rowIdx * $oStride) + ($colIdx * 4)
        $oBuf[$outBase]     = $sBuf[$inBase]
        $oBuf[$outBase + 1] = $sBuf[$inBase + 1]
        $oBuf[$outBase + 2] = $sBuf[$inBase + 2]
        $oBuf[$outBase + 3] = 255
        $writtenPx++
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($oBuf, 0, $oData.Scan0, $oBuf.Length)
$outBmp.UnlockBits($oData)
$outBmp.Save($OUT_IMG, [System.Drawing.Imaging.ImageFormat]::Png)
$outBmp.Dispose()

Say ("extended by   : {0}px  -> new canvas {1} x {2}" -f $extendPx, $srcW, $newH)
Say ("tail painted  : {0} px" -f $writtenPx)
Say "DONE"
Write-Output ("DONE - {0}" -f $OUT_IMG)
