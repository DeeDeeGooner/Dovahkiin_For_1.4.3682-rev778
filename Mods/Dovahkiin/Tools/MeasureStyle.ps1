# MeasureStyle.ps1 - how SIMPLE is a real RimWorld creature sprite, in numbers?
#
# "Make it look like a thrumbo" is a style target, and this project's rule is to measure a
# reference rather than guess at it. Vanilla art is packed into Unity bundles and cannot be
# read, so this measures shipped MOD animals, which are authored to match vanilla.
#
# Reports, per sprite, over the OPAQUE area only:
#   distinct   - unique RGB values (raw, and quantised to 8-level steps)
#   tone count - unique LUMINANCE values quantised to 16 steps: how many tonal "bands" the
#                artist actually used. This is the number that says "flat" or "rendered".
#   top tones  - how much of the creature the few most common tones cover. A flat sprite is
#                dominated by a handful; a rendered one has a long tail.

Add-Type -AssemblyName System.Drawing

$TARGETS = @(
    @( "Dragon's Descent - black dragon", "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dragons Descent\Textures\Things\Pawn\Animal\BDragon\BDragon1_south.png" ),
    @( "Divine Order - horse",            "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Divine Order\Textures\Things\Pawn\Animal\DivineOrderHorse\DivineOrderHorse_south.png" ),
    @( "OURS - Alduin (flattened)",         (Join-Path $PSScriptRoot "Alduin27_clean.png") )
)

foreach ($target in $TARGETS) {
    $label = $target[0]; $path = $target[1]
    if (-not (Test-Path $path)) { Write-Output ("MISSING: {0}" -f $path); continue }
    $bmp = [System.Drawing.Bitmap]::FromFile($path)
    $imgW = $bmp.Width; $imgH = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buffer = New-Object byte[] ($stride * $imgH)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    $bmp.UnlockBits($data); $bmp.Dispose()

    $rawColours = @{}
    $coarseColours = @{}
    $toneTally = New-Object 'int[]' 16
    $opaque = 0
    for ($rowIdx = 0; $rowIdx -lt $imgH; $rowIdx++) {
        for ($colIdx = 0; $colIdx -lt $imgW; $colIdx++) {
            $pixBase = ($rowIdx * $stride) + ($colIdx * 4)
            if ([int]$buffer[$pixBase + 3] -lt 200) { continue }
            $opaque++
            $blueVal = [int]$buffer[$pixBase]; $greenVal = [int]$buffer[$pixBase + 1]; $redVal = [int]$buffer[$pixBase + 2]
            $rawKey = "$redVal,$greenVal,$blueVal"
            if ($rawColours.ContainsKey($rawKey)) { $rawColours[$rawKey]++ } else { $rawColours[$rawKey] = 1 }
            $coarseKey = "{0},{1},{2}" -f ([int]($redVal/8)*8), ([int]($greenVal/8)*8), ([int]($blueVal/8)*8)
            if ($coarseColours.ContainsKey($coarseKey)) { $coarseColours[$coarseKey]++ } else { $coarseColours[$coarseKey] = 1 }
            $luma = [int]((0.2126 * $redVal) + (0.7152 * $greenVal) + (0.0722 * $blueVal))
            $toneTally[[Math]::Min(15, [int]($luma / 16))]++
        }
    }
    if ($opaque -eq 0) { Write-Output ("{0}: EMPTY" -f $label); continue }

    $usedTones = 0
    foreach ($count in $toneTally) { if (($count / [double]$opaque) -gt 0.01) { $usedTones++ } }
    $ranked = $coarseColours.GetEnumerator() | Sort-Object -Property Value -Descending
    $topThree = 0; $topSix = 0; $idx = 0
    foreach ($entry in $ranked) {
        if ($idx -lt 3) { $topThree += $entry.Value }
        if ($idx -lt 6) { $topSix += $entry.Value }
        $idx++
    }
    Write-Output ""
    Write-Output ("=== {0} ===" -f $label)
    Write-Output ("  frame            : {0} x {1}   opaque {2} px" -f $imgW, $imgH, $opaque)
    Write-Output ("  distinct colours : {0} raw   {1} at 8-step" -f $rawColours.Count, $coarseColours.Count)
    Write-Output ("  tone bands >1%   : {0} of 16" -f $usedTones)
    Write-Output ("  top 3 colours    : {0:N1}% of the creature" -f (100.0 * $topThree / $opaque))
    Write-Output ("  top 6 colours    : {0:N1}% of the creature" -f (100.0 * $topSix / $opaque))
    $shown = 0
    foreach ($entry in $ranked) {
        if ($shown -ge 5) { break }
        Write-Output ("      ({0,-12}) {1,6:N1}%" -f $entry.Key, (100.0 * $entry.Value / $opaque))
        $shown++
    }
}
Write-Output ""
Write-Output "DONE"
