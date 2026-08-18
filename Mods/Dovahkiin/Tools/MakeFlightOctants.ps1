# MakeFlightOctants.ps1 - EIGHT flight facings from ONE top-down sprite.
#
# The user, 2026-08-04: creatures move diagonally, so a flying dragon locked to four facings
# reads badly. Diagonals are for FLIGHT ONLY - it is the only top-down state, and top-down is
# the only projection that can be rotated at all. Soar and grounded are eye-level views and
# each facing has to be its own drawing (see DRAGON_ART_PIPELINE.md).
#
# ============================================================================================
# WHY THE FRAME GROWS FROM 512 TO 704 - MEASURED, NOT PICKED
# ============================================================================================
# Rotation is about the FRAME CENTRE, so what has to fit is the farthest ink pixel from that
# centre, swept round a circle. Measured on Alduin_flight_northview.png: the ink spans
# 491 x 485 of a 512 frame, and its farthest corner is 344.4 px from the centre - against a
# frame half-size of 256. A 45-degree turn would therefore CLIP THE WINGS, silently, and the
# only sign would be a dragon whose wingtips are cut off on the diagonals.
#
# Minimum frame is 2 x 344.4 = 689. This uses 704.
#
# ============================================================================================
# THE DRAW SIZE MUST CHANGE WITH THE FRAME, OR HE SHRINKS
# ============================================================================================
# RimWorld's drawSize scales the whole FRAME, not the creature. The same creature in a bigger
# frame therefore draws SMALLER. On-screen size is drawSize x (creature px / frame px):
#
#     512 frame: 5.6 x (491/512) = 5.369 cells
#     704 frame: drawSize x (491/704) = 5.369  ->  drawSize = 7.70
#
# So the flight octants need drawSize 7.70 to look identical to the 512-frame flight art.
# That number is derived here and consumed in AlduinGraphicsUtility.cs; if this frame size ever
# changes, that constant changes with it.
#
# The eight are emitted as SINGLE images, for Graphic_Single - one texture used at every Rot4,
# because the octant IS the facing and the engine's Rot4 is irrelevant once we pick by heading.

Add-Type -AssemblyName System.Drawing

$SOURCE = if ($env:DOVAH_FLIGHT) { $env:DOVAH_FLIGHT } else {
    "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Tools\DragonArt_2026-08-03\Alduin_flight_northview.png" }
$OUT_DIR = if ($env:DOVAH_DEST) { $env:DOVAH_DEST } else { Split-Path $SOURCE -Parent }
$PREFIX  = if ($env:DOVAH_PREFIX) { $env:DOVAH_PREFIX } else { "Alduin_flight_oct" }
$FRAME   = if ($env:DOVAH_FRAME) { [int]$env:DOVAH_FRAME } else { 704 }

# Compass bearings, clockwise from north. MakeFlightRotations.ps1 established the convention:
# north is the source as drawn, and EAST is Rotate90FlipNone - a CLOCKWISE quarter turn. So a
# heading of X degrees clockwise from north is the source turned X degrees clockwise.
$OCTANTS = @(
    @( "N",   0.0 ), @( "NE",  45.0 ), @( "E",   90.0 ), @( "SE", 135.0 ),
    @( "S", 180.0 ), @( "SW", 225.0 ), @( "W",  270.0 ), @( "NW", 315.0 )
)

$src = [System.Drawing.Bitmap]::FromFile($SOURCE)
Write-Output ("source : {0}  ({1} x {2})  ->  frame {3}" -f (Split-Path $SOURCE -Leaf), $src.Width, $src.Height, $FRAME)

foreach ($oct in $OCTANTS) {
    $name = $oct[0]; $deg = [double]$oct[1]
    $out = New-Object System.Drawing.Bitmap ($FRAME, $FRAME, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    # rotate about the OUTPUT centre, then draw the source centred on it
    $g.TranslateTransform([float]($FRAME / 2.0), [float]($FRAME / 2.0))
    $g.RotateTransform([float]$deg)
    $g.TranslateTransform([float](-$src.Width / 2.0), [float](-$src.Height / 2.0))
    $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    $g.Dispose()
    $path = Join-Path $OUT_DIR ("{0}_{1}.png" -f $PREFIX, $name)
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    Write-Output ("  wrote {0}_{1}.png   ({2} deg clockwise)" -f $PREFIX, $name, $deg)
}
$src.Dispose()

# ---------------------------------------------------------------- validate the rotation SIGN
# "Which way does a positive angle turn" is exactly the kind of thing that is easy to get
# backwards and free to check, so it is checked rather than reasoned about: the 90-degree
# octant must reproduce the EXISTING east cardinal, which was made by an exact Rotate90FlipNone.
# If the sign were inverted this would come back as the WEST sprite and the error would be
# enormous instead of a resampling difference.
$eastRef = Join-Path (Split-Path $SOURCE -Parent) "Alduin_flight_eastview.png"
$mine    = Join-Path $OUT_DIR ("{0}_E.png" -f $PREFIX)
if ((Test-Path $eastRef) -and (Test-Path $mine)) {
    $a = [System.Drawing.Bitmap]::FromFile($eastRef)
    $bFull = [System.Drawing.Bitmap]::FromFile($mine)
    # crop my larger frame back to the reference's size, about the shared centre
    $off = [int](($FRAME - $a.Width) / 2)
    $sum = 0.0; $n = 0
    for ($y = 0; $y -lt $a.Height; $y += 3) {
        for ($x = 0; $x -lt $a.Width; $x += 3) {
            $pa = $a.GetPixel($x, $y)
            $pb = $bFull.GetPixel($x + $off, $y + $off)
            $sum += [Math]::Abs($pa.A - $pb.A); $n++
        }
    }
    $a.Dispose(); $bFull.Dispose()
    $meanAlphaDelta = $sum / [Math]::Max(1, $n)
    Write-Output ""
    Write-Output ("SIGN CHECK vs the existing east cardinal: mean alpha delta {0:N2} of 255" -f $meanAlphaDelta)
    if ($meanAlphaDelta -lt 12.0) {
        Write-Output "  PASS - clockwise is correct. (Small non-zero is expected: this resamples, RotateFlip did not.)"
    } else {
        Write-Output "  ** FAIL ** - the rotation direction is probably inverted. Do NOT ship these."
    }
} else {
    Write-Output "SIGN CHECK SKIPPED - east cardinal not found beside the source."
}
Write-Output "DONE"
