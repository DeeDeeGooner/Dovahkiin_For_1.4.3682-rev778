# AddTail.ps1 - a length of tail emerging from behind his right hip.
#
# The generated pose is right, but the tail is COMPLETELY occluded by his body, so he reads as
# having no tail at all. This adds back just enough of it to imply the rest.
#
# THE FIX IS IN SOURCE SPACE, like ExtendTail.ps1 before it: repair the reference, then trace
# and build run completely unchanged. No new code path in the pipeline, no second way for it to
# behave.
#
# TWO RULES THIS OBEYS, both from measurement rather than taste:
#
# 1. IT CAN ONLY PAINT WHERE THE SOURCE IS BACKGROUND. Every pixel is tested against the
#    original before being written, so the dragon himself is untouchable by construction - the
#    tail cannot bleed over his leg however wrong its geometry is. That is also what makes it
#    read as passing BEHIND him: it is clipped exactly at his own outline.
#
# 2. IT MUST FIT IN THE LOWER-RIGHT POCKET. Measured: his wings span x 5..1018 of 1024, so
#    there is no margin to grow into. Below y~620 the right wing has ended and his leg reaches
#    only x~663, leaving x 663..1020 / y 620..1020 empty. The tail lives there.
#
# Style is matched to THIS image, measured, not to the other references:
#    outline (4,4,5)   body dark (63,64,70)   body light (105,107,116)

Add-Type -AssemblyName System.Drawing

$SRC = $env:DOVAH_REF
$OUT = $env:DOVAH_OUT
$SUPER = 3

$C_LINE  = [System.Drawing.Color]::FromArgb(255,   4,   4,   5)
$C_DARK  = [System.Drawing.Color]::FromArgb(255,  63,  64,  70)
$C_LIGHT = [System.Drawing.Color]::FromArgb(255, 105, 107, 116)

# ---- the spine. Starts INSIDE his body so the visible tail begins flush at his silhouette
# edge rather than floating a few pixels off it.
# THE PATH THREADS THE LEG GAP, AND THE OCCLUSION MASK DOES THE REST.
#
# Two earlier versions left the body at the FLANK - y=583, then y=636 - and both read as a spike
# stuck through his right leg. The reason generalises: a tail leaving the SIDE of a front-on
# creature has nothing in front of it to hide behind, so its whole length is visible at once and
# it becomes the loudest shape on the sprite.
#
# On a front-facing creature a tail is MOSTLY HIDDEN. It should read as two small pieces - one
# seen THROUGH THE GAP BETWEEN HIS LEGS, and a TIP clearing his right foot - with the leg itself
# covering the run between them. That gap is the perspective cue, and it is what makes the tail
# read as going away BEHIND him instead of sticking out sideways.
#
# This needs no extra code. One continuous tail is drawn, and the "never paint where he already
# is" rule cuts the middle out of it for free.
#
# Measured geometry it is threaded through (MeasureLegGap.ps1):
#   legs merge into the body above  y~605
#   gap between the legs   y=660: x 534..573 (40px)  y=700: 494..568 (75px)  y=720: 456..581
#   right leg / foot       y=760: x 580..705         y=780: 567..721        y=800: 623..721
# Crossing HIGH in the gap (y~670) failed too: up there the gap is only 40px wide, so the tail
# was clipped to a sliver that read as shadow between his legs, and the tip past the foot was so
# short it read as a blunt stick. BOTH visible pieces have to be big enough to be recognisable,
# or the eye gets two unrelated marks instead of one tail behind a leg.
#
# So cross LOW, where the gap is at its widest (y 730-770, about 130-150px), and run the tip out
# far enough to actually taper. Length is what makes a tip read as a tip.
# TIP ON THE LEFT, AND NOTHING ELSE VISIBLE.
#
# Three versions were rejected before this: out of the right flank (a spike through his leg),
# threaded through the leg gap high (a sliver that read as shadow, plus a blunt stub), and
# threaded low (better, but the tip ran on as a rod). The user's call is the least of all of
# them - just a tip, on the LEFT.
#
# So the whole path stays INSIDE his silhouette until it clears his left foot, and the occlusion
# mask hides all of it. Nothing appears between the legs at all.
#
# Left foot's outer edge, measured: x~362 at y=740, ~318 at y=760, ~302 at y=780-800.
$SPINE = @(
    @( 460, 690 ),   # hidden inside his left leg (it spans 395..506 here). Was 500: at half-width
                     # 24 that reached x=524 and an 18px sliver showed in the LEG GAP, which is
                     # the thing that was just rejected. Centre must be <= 506-24 to stay hidden.
    @( 400, 745 ),   # still inside it (350..430 here)
    @( 320, 785 ),   # still inside the foot (301..456 here) - emerges just past this
    @( 255, 801 ),   # clear of the foot's outer edge - the visible tip starts about here
    @( 185, 809 )    # the point
)
$ROOT_HALF = 24.0
$TIP_HALF  =  2.5   # finer than the 12px keyline that wraps it, or the pen's round cap ends the
                    # shape in a blob and the tip reads as a cut-off bar rather than a point
$TAPER_POW = 1.00   # linear. Only the last quarter of this path is visible, so the taper has to
                    # still be delivering width when it emerges - a fast taper would leave the
                    # exposed part a uniform thin rod, which is exactly what was just rejected.
$KEYLINE   = 12.0   # slightly under his own 15 - this is further away and behind him

function CatmullRom {
    param($pts, [double]$t)
    $segCount = $pts.Count - 1
    $scaled = $t * $segCount
    $i = [int][Math]::Floor($scaled)
    if ($i -ge $segCount) { $i = $segCount - 1 }
    $local = $scaled - $i
    $p0 = $pts[[Math]::Max(0, $i - 1)]
    $p1 = $pts[$i]
    $p2 = $pts[$i + 1]
    $p3 = $pts[[Math]::Min($pts.Count - 1, $i + 2)]
    $t2 = $local * $local; $t3 = $t2 * $local
    $x = 0.5 * ((2*$p1[0]) + (-$p0[0] + $p2[0]) * $local + (2*$p0[0] - 5*$p1[0] + 4*$p2[0] - $p3[0]) * $t2 + (-$p0[0] + 3*$p1[0] - 3*$p2[0] + $p3[0]) * $t3)
    $y = 0.5 * ((2*$p1[1]) + (-$p0[1] + $p2[1]) * $local + (2*$p0[1] - 5*$p1[1] + 4*$p2[1] - $p3[1]) * $t2 + (-$p0[1] + 3*$p1[1] - 3*$p2[1] + $p3[1]) * $t3)
    return @($x, $y)
}

# sample the spine, build left and right offset walls
$SAMPLES = 90
$centres = @(); $halves = @()
for ($s = 0; $s -le $SAMPLES; $s++) {
    $t = $s / [double]$SAMPLES
    $centres += , (CatmullRom $SPINE $t)
    $halves  += ($TIP_HALF + (($ROOT_HALF - $TIP_HALF) * [Math]::Pow((1.0 - $t), $TAPER_POW)))
}
function BuildBand {
    param($centres, $halves, [double]$widthScale, [double]$normalShift)
    $left = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $right = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($s = 0; $s -lt $centres.Count; $s++) {
        $prev = $centres[[Math]::Max(0, $s - 1)]
        $next = $centres[[Math]::Min($centres.Count - 1, $s + 1)]
        $dx = $next[0] - $prev[0]; $dy = $next[1] - $prev[1]
        $len = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
        if ($len -le 0.0) { $len = 1.0 }
        $nx = -$dy / $len; $ny = $dx / $len          # unit normal
        $h  = $halves[$s] * $widthScale
        $cx = $centres[$s][0] + ($nx * $halves[$s] * $normalShift)
        $cy = $centres[$s][1] + ($ny * $halves[$s] * $normalShift)
        $left.Add((New-Object System.Drawing.PointF ([float](($cx + $nx * $h) * $SUPER), [float](($cy + $ny * $h) * $SUPER))))
        $right.Add((New-Object System.Drawing.PointF ([float](($cx - $nx * $h) * $SUPER), [float](($cy - $ny * $h) * $SUPER))))
    }
    $poly = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    foreach ($p in $left) { $poly.Add($p) }
    for ($s = $right.Count - 1; $s -ge 0; $s--) { $poly.Add($right[$s]) }
    # ",": without it PowerShell UNROLLS the List on return, the caller gets an Object[] of
    # PointF, and $poly.ToArray() then binds to each ELEMENT - "PointF does not contain a method
    # named ToArray". The script carries on and writes a tail-less image, because that is a
    # non-terminating error.
    return , $poly.ToArray()
}

$bmpSrc = [System.Drawing.Bitmap]::FromFile($SRC)
$imgW = $bmpSrc.Width; $imgH = $bmpSrc.Height

# ---- draw the tail on its own transparent supersampled layer
$device = $imgW * $SUPER
$layer = New-Object System.Drawing.Bitmap ($device, $device, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($layer)
$gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gfx.Clear([System.Drawing.Color]::Transparent)

$bodyPoly = BuildBand $centres $halves 1.0 0.0
$bodyPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$bodyPath.AddClosedCurve($bodyPoly, 0.12)
# keyline first, as a stroke centred on the boundary, then the fill over its inner half
$pen = New-Object System.Drawing.Pen ($C_LINE, [float]($KEYLINE * 2.0 * $SUPER))
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$gfx.DrawPath($pen, $bodyPath); $pen.Dispose()
$brush = New-Object System.Drawing.SolidBrush ($C_DARK)
$gfx.FillPath($brush, $bodyPath); $brush.Dispose()

# a slim lighter band along the upper edge, so it is not a flat dark slab. Same two-tone
# treatment his legs already use.
$hiPoly = BuildBand $centres $halves 0.26 -0.50
$hiPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$hiPath.AddClosedCurve($hiPoly, 0.12)
$brush2 = New-Object System.Drawing.SolidBrush ($C_LIGHT)
$gfx.FillPath($brush2, $hiPath); $brush2.Dispose()
$hiPath.Dispose(); $bodyPath.Dispose(); $gfx.Dispose()

# ---- downsample the layer to source resolution
$tail = New-Object System.Drawing.Bitmap ($imgW, $imgH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$tg = [System.Drawing.Graphics]::FromImage($tail)
$tg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$tg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$tg.Clear([System.Drawing.Color]::Transparent)
$tg.DrawImage($layer, (New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH))
$tg.Dispose(); $layer.Dispose()

# ---- composite: tail UNDER the dragon, by refusing to write anywhere he already is
$rect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
$sd = $bmpSrc.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sStride = $sd.Stride
$sBuf = New-Object byte[] ($sStride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($sd.Scan0, $sBuf, 0, $sBuf.Length)
$bmpSrc.UnlockBits($sd)

$td = $tail.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$tStride = $td.Stride
$tBuf = New-Object byte[] ($tStride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($td.Scan0, $tBuf, 0, $tBuf.Length)
$tail.UnlockBits($td)

$outBmp = New-Object System.Drawing.Bitmap ($imgW, $imgH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$od = $outBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$oStride = $od.Stride
$oBuf = New-Object byte[] ($oStride * $imgH)

$painted = 0; $blocked = 0
for ($y = 0; $y -lt $imgH; $y++) {
    for ($x = 0; $x -lt $imgW; $x++) {
        $sb = ($y * $sStride) + ($x * 4)
        $tb = ($y * $tStride) + ($x * 4)
        $ob = ($y * $oStride) + ($x * 4)
        $srcB = [int]$sBuf[$sb]; $srcG = [int]$sBuf[$sb+1]; $srcR = [int]$sBuf[$sb+2]
        $oBuf[$ob] = [byte]$srcB; $oBuf[$ob+1] = [byte]$srcG; $oBuf[$ob+2] = [byte]$srcR; $oBuf[$ob+3] = 255
        $tailA = [int]$tBuf[$tb+3]
        if ($tailA -le 0) { continue }
        $srcLuma = [int]((0.2126 * $srcR) + (0.7152 * $srcG) + (0.0722 * $srcB))
        if ($srcLuma -le 240) { if ($tailA -gt 40) { $blocked++ }; continue }   # he is here - hands off
        $a = $tailA / 255.0
        $oBuf[$ob]   = [byte][int](($srcB * (1.0 - $a)) + ([int]$tBuf[$tb]   * $a))
        $oBuf[$ob+1] = [byte][int](($srcG * (1.0 - $a)) + ([int]$tBuf[$tb+1] * $a))
        $oBuf[$ob+2] = [byte][int](($srcR * (1.0 - $a)) + ([int]$tBuf[$tb+2] * $a))
        $painted++
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($oBuf, 0, $od.Scan0, $oBuf.Length)
$outBmp.UnlockBits($od)
$outBmp.Save($OUT, [System.Drawing.Imaging.ImageFormat]::Png)
$outBmp.Dispose(); $tail.Dispose(); $bmpSrc.Dispose()

Write-Output ("tail painted : {0} px" -f $painted)
Write-Output ("blocked by his own body (drawn behind him) : {0} px" -f $blocked)
Write-Output ("wrote : {0}" -f $OUT)

