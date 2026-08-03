# TraceRef.ps1 - generalised reference tracer.
#
# Same proven pipeline as the first Alduin trace, but parameterised and with a DOWNSCALE
# step: this source is ~1400px, and the flood fill plus morphology at that size is about
# 26 million PowerShell loop iterations. Tracing at ~620px is still far finer than the
# 166px crop and runs in seconds.
#
# Set DOVAH_REF to the source image.

Add-Type -AssemblyName System.Drawing

$SRC      = if ($env:DOVAH_REF) { $env:DOVAH_REF } else { "C:\Users\User\Downloads\Gemini_Generated_Image_8ykfbv8ykfbv8ykf.png" }
$TAG      = if ($env:DOVAH_TAG) { $env:DOVAH_TAG } else { "ref" }
$SCRATCH  = $PSScriptRoot
$REPORT   = Join-Path $SCRATCH ("trace_{0}.txt" -f $TAG)
$MASK_OUT = Join-Path $SCRATCH ("{0}_mask.png" -f $TAG)
if (Test-Path $REPORT) { Remove-Item $REPORT }
function Say { param([string]$line) Add-Content -Path $REPORT -Value $line -Encoding UTF8 }

$WORK_MAX = 620   # long side of the working copy

# ---------------------------------------------------------------- load + downscale
$original = [System.Drawing.Bitmap]::FromFile($SRC)
$origW = $original.Width; $origH = $original.Height
Say ("source        : {0}" -f $SRC)
Say ("original      : {0} x {1}" -f $origW, $origH)

$shrink = [Math]::Min(1.0, ($WORK_MAX / [double][Math]::Max($origW, $origH)))
$imgW = [int][Math]::Round($origW * $shrink)
$imgH = [int][Math]::Round($origH * $shrink)
$work = New-Object System.Drawing.Bitmap ($imgW, $imgH,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$workGfx = [System.Drawing.Graphics]::FromImage($work)
$workGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$workGfx.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
# paint white first: a PNG with transparency would otherwise threshold as "dark"
$whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$workGfx.FillRectangle($whiteBrush, 0, 0, $imgW, $imgH)
$whiteBrush.Dispose()
$workGfx.DrawImage($original, (New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH))
$workGfx.Dispose()
$original.Dispose()
Say ("working copy  : {0} x {1}  (scale {2:N4})" -f $imgW, $imgH, $shrink)

$rect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
$data = $work.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                       [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$buffer = New-Object byte[] ($stride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
$work.UnlockBits($data)
$work.Dispose()

# ---------------------------------------------------------------- luminance
$lumaOf = New-Object 'int[]' ($imgW * $imgH)
$histogram = New-Object 'int[]' 256
for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
    $rowBase = $rowIdx * $stride
    for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
        $pixBase = $rowBase + ($colIdx * 4)
        $luma = [int]((0.2126 * [int]$buffer[$pixBase + 2]) + (0.7152 * [int]$buffer[$pixBase + 1]) + (0.0722 * [int]$buffer[$pixBase]))
        if ($luma -lt 0) { $luma = 0 }; if ($luma -gt 255) { $luma = 255 }
        $lumaOf[($rowIdx * $imgW) + $colIdx] = $luma
        $histogram[$luma]++
    }
}
Say ""
Say "luminance histogram, 16 buckets:"
$totalPixels = $imgW * $imgH
for ($bucketIdx = 0; $bucketIdx -lt 16; $bucketIdx++) {
    $bucketSum = 0
    for ($lumaIdx = ($bucketIdx * 16); $lumaIdx -lt (($bucketIdx + 1) * 16); $lumaIdx++) { $bucketSum += $histogram[$lumaIdx] }
    Say ("  {0,3}-{1,3} : {2,7}  {3}" -f ($bucketIdx * 16), ((($bucketIdx + 1) * 16) - 1), $bucketSum,
        ('#' * [int]($bucketSum / [Math]::Max(1, $totalPixels) * 160)))
}

# THRESHOLD and INSET are both per-reference. Read the histogram above and set them; do not
# assume. A cut of 226 suits white paper and calls an entire TAN PARCHMENT card "dragon".
# INSET crops past a card's border frame: a frame rings the image, so the outside-flood below
# finds no seed and every interior pixel counts as a hole - the mask comes back fully solid.
$CUT   = if ($env:DOVAH_CUT)   { [int]$env:DOVAH_CUT }   else { 226 }
$INSET = if ($env:DOVAH_INSET) { [int]$env:DOVAH_INSET } else { 0 }
$cropLeft   = $INSET
$cropTop    = $INSET
$cropRight  = $imgW - 1 - $INSET
$cropBottom = $imgH - 1 - $INSET
$mask = New-Object 'bool[]' ($imgW * $imgH)
$darkCount = 0
for ($rowIdx = $cropTop; $rowIdx -le $cropBottom; $rowIdx++) {
    for ($colIdx = $cropLeft; $colIdx -le $cropRight; $colIdx++) {
        $flatIdx = ($rowIdx * $imgW) + $colIdx
        if ($lumaOf[$flatIdx] -le $CUT) { $mask[$flatIdx] = $true; $darkCount++ }
    }
}
Say ""
Say ("threshold     : {0}   inset {1}px   analysing x {2}..{3}  y {4}..{5}" -f `
    $CUT, $INSET, $cropLeft, $cropRight, $cropTop, $cropBottom)
Say ("below cut     : {0} px ({1:N1}% of frame)" -f $darkCount, (100.0 * $darkCount / $totalPixels))

# ---------------------------------------------------------------- largest blob
$label = New-Object 'int[]' ($imgW * $imgH)
$blobSizes = New-Object System.Collections.Generic.List[int]
$blobSizes.Add(0)
$currentLabel = 0
$queueX = New-Object 'int[]' ($imgW * $imgH)
$queueY = New-Object 'int[]' ($imgW * $imgH)
for ($seedY = 0; $seedY -lt $imgH; $seedY++) {
    for ($seedX = 0; $seedX -lt $imgW; $seedX++) {
        $seedFlat = ($seedY * $imgW) + $seedX
        if ((-not $mask[$seedFlat]) -or ($label[$seedFlat] -ne 0)) { continue }
        $currentLabel++
        $head = 0; $tail = 0
        $queueX[$tail] = $seedX; $queueY[$tail] = $seedY; $tail++
        $label[$seedFlat] = $currentLabel
        $blobArea = 0
        while ($head -lt $tail) {
            $atX = $queueX[$head]; $atY = $queueY[$head]; $head++
            $blobArea++
            foreach ($step in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
                $nextX = $atX + $step[0]; $nextY = $atY + $step[1]
                if (($nextX -lt 0) -or ($nextY -lt 0) -or ($nextX -ge $imgW) -or ($nextY -ge $imgH)) { continue }
                $nextFlat = ($nextY * $imgW) + $nextX
                if ($mask[$nextFlat] -and ($label[$nextFlat] -eq 0)) {
                    $label[$nextFlat] = $currentLabel
                    $queueX[$tail] = $nextX; $queueY[$tail] = $nextY; $tail++
                }
            }
        }
        $blobSizes.Add($blobArea)
    }
}
$biggestLabel = 0; $biggestArea = 0
for ($labelIdx = 1; $labelIdx -lt $blobSizes.Count; $labelIdx++) {
    if ($blobSizes[$labelIdx] -gt $biggestArea) { $biggestArea = $blobSizes[$labelIdx]; $biggestLabel = $labelIdx }
}
Say ("blobs         : {0}   largest {1} px ({2:N1}% of non-white)" -f `
    ($blobSizes.Count - 1), $biggestArea, (100.0 * $biggestArea / [Math]::Max(1,$darkCount)))

$solid = New-Object 'bool[]' ($imgW * $imgH)
for ($flatIdx = 0; $flatIdx -lt ($imgW * $imgH); $flatIdx++) { $solid[$flatIdx] = ($label[$flatIdx] -eq $biggestLabel) }

# ---------------------------------------------------------------- fill interior holes
$outside = New-Object 'bool[]' ($imgW * $imgH)
$head = 0; $tail = 0
for ($edgeX = $cropLeft; $edgeX -le $cropRight; $edgeX++) {
    foreach ($edgeY in @($cropTop, $cropBottom)) {
        $edgeFlat = ($edgeY * $imgW) + $edgeX
        if ((-not $solid[$edgeFlat]) -and (-not $outside[$edgeFlat])) {
            $outside[$edgeFlat] = $true; $queueX[$tail] = $edgeX; $queueY[$tail] = $edgeY; $tail++
        }
    }
}
for ($edgeY = $cropTop; $edgeY -le $cropBottom; $edgeY++) {
    foreach ($edgeX in @($cropLeft, $cropRight)) {
        $edgeFlat = ($edgeY * $imgW) + $edgeX
        if ((-not $solid[$edgeFlat]) -and (-not $outside[$edgeFlat])) {
            $outside[$edgeFlat] = $true; $queueX[$tail] = $edgeX; $queueY[$tail] = $edgeY; $tail++
        }
    }
}
Say ("outside seeds : {0}" -f $tail)
if ($tail -eq 0) { Say "!! ABORT: no background seed"; Write-Output "ABORT"; exit 1 }
while ($head -lt $tail) {
    $atX = $queueX[$head]; $atY = $queueY[$head]; $head++
    foreach ($step in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
        $nextX = $atX + $step[0]; $nextY = $atY + $step[1]
        if (($nextX -lt $cropLeft) -or ($nextY -lt $cropTop) -or ($nextX -gt $cropRight) -or ($nextY -gt $cropBottom)) { continue }
        $nextFlat = ($nextY * $imgW) + $nextX
        if ((-not $solid[$nextFlat]) -and (-not $outside[$nextFlat])) {
            $outside[$nextFlat] = $true
            $queueX[$tail] = $nextX; $queueY[$tail] = $nextY; $tail++
        }
    }
}
# Restricted to the CROP. Run over the whole image and every pixel outside the inset ring -
# which the outside-flood never reached - counts as an interior hole and gets filled.
$holesFilled = 0
for ($rowIdx = $cropTop; $rowIdx -le $cropBottom; $rowIdx++) {
    for ($colIdx = $cropLeft; $colIdx -le $cropRight; $colIdx++) {
        $flatIdx = ($rowIdx * $imgW) + $colIdx
        if ((-not $solid[$flatIdx]) -and (-not $outside[$flatIdx])) { $solid[$flatIdx] = $true; $holesFilled++ }
    }
}
Say ("holes filled  : {0} px" -f $holesFilled)

# ---------------------------------------------------------------- measure
$minX = $imgW; $maxX = -1; $minY = $imgH; $maxY = -1; $solidCount = 0
for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
    for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
        if ($solid[($rowIdx * $imgW) + $colIdx]) {
            $solidCount++
            if ($colIdx -lt $minX) { $minX = $colIdx }
            if ($colIdx -gt $maxX) { $maxX = $colIdx }
            if ($rowIdx -lt $minY) { $minY = $rowIdx }
            if ($rowIdx -gt $maxY) { $maxY = $rowIdx }
        }
    }
}
$spanX = $maxX - $minX + 1
$spanY = $maxY - $minY + 1
$cropArea = $imgW * $imgH
Say ""
Say ("bbox          : x {0}..{1} ({2}px)  y {3}..{4} ({5}px)" -f $minX,$maxX,$spanX,$minY,$maxY,$spanY)
Say ("aspect W/H    : {0:N3}" -f ($spanX / $spanY))
Say ("fill density  : {0:N1}% of bbox    {1:N1}% of frame" -f `
    (100.0 * $solidCount / ($spanX * $spanY)), (100.0 * $solidCount / $cropArea))
if ((100.0 * $solidCount / $cropArea) -gt 85.0) {
    Say "!! WARNING: the mask has swallowed the background."
}

# ---------------------------------------------------------------- write mask
$maskImage = New-Object System.Drawing.Bitmap ($imgW, $imgH,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$maskData = $maskImage.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
                                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$maskBuffer = New-Object byte[] ($maskData.Stride * $imgH)
for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
    for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
        $pixBase = ($rowIdx * $maskData.Stride) + ($colIdx * 4)
        if ($solid[($rowIdx * $imgW) + $colIdx]) {
            $maskBuffer[$pixBase] = 30; $maskBuffer[$pixBase + 1] = 25; $maskBuffer[$pixBase + 2] = 25
        } else {
            $maskBuffer[$pixBase] = 245; $maskBuffer[$pixBase + 1] = 245; $maskBuffer[$pixBase + 2] = 245
        }
        $maskBuffer[$pixBase + 3] = 255
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($maskBuffer, 0, $maskData.Scan0, $maskBuffer.Length)
$maskImage.UnlockBits($maskData)
$maskImage.Save($MASK_OUT, [System.Drawing.Imaging.ImageFormat]::Png)
$maskImage.Dispose()
Say ""
Say "DONE"
Write-Output ("DONE - {0}" -f $MASK_OUT)
