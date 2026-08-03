# FindPalette.ps1 - what tones is this reference ACTUALLY drawn in?
#
# The band list in BuildFromMask.ps1 was hand-written for an EARLIER reference. On this one it
# leaves a whole population stranded between two candidates - 8.4% of the creature decided by a
# coin flip, which renders as speckle on flat surfaces.
#
# So derive the palette from the picture instead of guessing it: k-means over the creature's
# own pixels. The centres land ON the populations rather than between them, which is precisely
# what removes the ambiguity.
#
# Deterministic: seeded by even spacing through the luminance range, never Get-Random. This
# project hash-checks its art, so a palette that changed run to run would be worthless.

Add-Type -AssemblyName System.Drawing

$SRC    = $env:DOVAH_REF
$KMAX   = if ($env:DOVAH_K) { [int]$env:DOVAH_K } else { 5 }
$REPORT = Join-Path $PSScriptRoot "palette.txt"
if (Test-Path $REPORT) { Remove-Item $REPORT }
function Say { param([string]$line) Add-Content -Path $REPORT -Value $line -Encoding UTF8 }

$bmp = [System.Drawing.Bitmap]::FromFile($SRC)
$imgW = $bmp.Width; $imgH = $bmp.Height
$rect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes = New-Object byte[] ($stride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$bmp.UnlockBits($data); $bmp.Dispose()

# collect creature pixels
$sampleR = New-Object System.Collections.Generic.List[int]
$sampleG = New-Object System.Collections.Generic.List[int]
$sampleB = New-Object System.Collections.Generic.List[int]
for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
    $rowBase = $rowIdx * $stride
    for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
        $pixBase = $rowBase + ($colIdx * 4)
        $pxR = [int]$bytes[$pixBase + 2]; $pxG = [int]$bytes[$pixBase + 1]; $pxB = [int]$bytes[$pixBase]
        $luma = [int]((0.2126 * $pxR) + (0.7152 * $pxG) + (0.0722 * $pxB))
        if ($luma -gt 226) { continue }
        $sampleR.Add($pxR); $sampleG.Add($pxG); $sampleB.Add($pxB)
    }
}
$sampleCount = $sampleR.Count
Say ("source          : {0}" -f $SRC)
Say ("creature pixels : {0}" -f $sampleCount)
Say ""

for ($clusterCount = 3; $clusterCount -le $KMAX; $clusterCount++) {
    # deterministic seeding: even steps through 0..160 of luminance
    $centreR = New-Object 'double[]' $clusterCount
    $centreG = New-Object 'double[]' $clusterCount
    $centreB = New-Object 'double[]' $clusterCount
    for ($seedIdx = 0; $seedIdx -lt $clusterCount; $seedIdx++) {
        $seedVal = 160.0 * $seedIdx / [Math]::Max(1, ($clusterCount - 1))
        $centreR[$seedIdx] = $seedVal; $centreG[$seedIdx] = $seedVal; $centreB[$seedIdx] = $seedVal
    }
    $assignOf = New-Object 'int[]' $sampleCount
    for ($iter = 0; $iter -lt 24; $iter++) {
        # assign
        for ($pointIdx = 0; $pointIdx -lt $sampleCount; $pointIdx++) {
            $bestIdx = 0; $bestDist = [double]::MaxValue
            for ($candIdx = 0; $candIdx -lt $clusterCount; $candIdx++) {
                $dR = $sampleR[$pointIdx] - $centreR[$candIdx]
                $dG = $sampleG[$pointIdx] - $centreG[$candIdx]
                $dB = $sampleB[$pointIdx] - $centreB[$candIdx]
                $dist = ($dR * $dR) + ($dG * $dG) + ($dB * $dB)
                if ($dist -lt $bestDist) { $bestDist = $dist; $bestIdx = $candIdx }
            }
            $assignOf[$pointIdx] = $bestIdx
        }
        # update
        $sumR = New-Object 'double[]' $clusterCount
        $sumG = New-Object 'double[]' $clusterCount
        $sumB = New-Object 'double[]' $clusterCount
        $tally = New-Object 'int[]' $clusterCount
        for ($pointIdx = 0; $pointIdx -lt $sampleCount; $pointIdx++) {
            $whichCluster = $assignOf[$pointIdx]
            $sumR[$whichCluster] += $sampleR[$pointIdx]
            $sumG[$whichCluster] += $sampleG[$pointIdx]
            $sumB[$whichCluster] += $sampleB[$pointIdx]
            $tally[$whichCluster]++
        }
        for ($candIdx = 0; $candIdx -lt $clusterCount; $candIdx++) {
            if ($tally[$candIdx] -gt 0) {
                $centreR[$candIdx] = $sumR[$candIdx] / $tally[$candIdx]
                $centreG[$candIdx] = $sumG[$candIdx] / $tally[$candIdx]
                $centreB[$candIdx] = $sumB[$candIdx] / $tally[$candIdx]
            }
        }
    }
    # order by luminance, darkest first
    $order = 0..($clusterCount - 1) | Sort-Object -Property @{ Expression = {
        (0.2126 * $centreR[$_]) + (0.7152 * $centreG[$_]) + (0.0722 * $centreB[$_]) } }
    Say ("=== k = {0} ===" -f $clusterCount)
    $csv = @()
    foreach ($clusterIdx in $order) {
        $rVal = [int][Math]::Round($centreR[$clusterIdx])
        $gVal = [int][Math]::Round($centreG[$clusterIdx])
        $bVal = [int][Math]::Round($centreB[$clusterIdx])
        Say ("   ({0,3},{1,3},{2,3})  {3,8} px  {4,5:N1}%" -f $rVal, $gVal, $bVal,
            $tally[$clusterIdx], (100.0 * $tally[$clusterIdx] / $sampleCount))
        $csv += ("{0},{1},{2}" -f $rVal, $gVal, $bVal)
    }
    Say ("   PALETTE : {0}" -f ($csv -join ";"))

    # how ambiguous is this palette? same margin test as the diagnosis.
    $coinFlips = 0
    for ($pointIdx = 0; $pointIdx -lt $sampleCount; $pointIdx++) {
        $bestDist = [double]::MaxValue; $secondDist = [double]::MaxValue
        for ($candIdx = 0; $candIdx -lt $clusterCount; $candIdx++) {
            $dR = $sampleR[$pointIdx] - $centreR[$candIdx]
            $dG = $sampleG[$pointIdx] - $centreG[$candIdx]
            $dB = $sampleB[$pointIdx] - $centreB[$candIdx]
            $dist = ($dR * $dR) + ($dG * $dG) + ($dB * $dB)
            if ($dist -lt $bestDist) { $secondDist = $bestDist; $bestDist = $dist }
            elseif ($dist -lt $secondDist) { $secondDist = $dist }
        }
        $ratio = if ($secondDist -le 0) { 1.0 } else { [Math]::Sqrt($bestDist) / [Math]::Sqrt($secondDist) }
        if ($ratio -gt 0.80) { $coinFlips++ }
    }
    Say ("   on-the-boundary pixels : {0} px  {1:N1}%   (the hand-written list scored 8.4%)" -f `
        $coinFlips, (100.0 * $coinFlips / $sampleCount))
    Say ""
}
Say "DONE"
Write-Output "DONE"
