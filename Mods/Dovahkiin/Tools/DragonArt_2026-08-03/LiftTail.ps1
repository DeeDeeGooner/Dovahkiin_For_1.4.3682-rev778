# LiftTail.ps1 - swing the tail up about the hip so it clears the legs.
#
# WHY A ROTATION AND NOT A REDRAW: the tail carries the dorsal spine ridge, the lighter stripe
# down its length and its own taper - all of it Gemini's, all of it matching the rest of the
# animal. Redrawing would mean re-inventing those. Rotating moves the real artwork.
#
# MEASURED CONSTRAINTS (MapEast.ps1):
#   frame margins are 5px left, 4px right - there is NO horizontal room, so the canvas is
#   PADDED on the left before anything moves. A 20deg lift throws the tip ~100px further left.
#   the tail underside currently reaches y=851 while the near foot bottoms at y~770, so the tail
#   hangs 81px BELOW the ground he stands on. That is the defect.
#
# HOW THE TAIL IS ISOLATED: flood fill from a seed inside the tail, restricted to creature
# pixels on the far side of a CUT half-plane at the hip. The half-plane alone is not enough -
# it also contains the left wing - but the wing is not CONNECTED to the tail, so the flood
# never reaches it. The cut is placed where the leg crosses the tail, so the seam it leaves is
# hidden behind the leg.

Add-Type -AssemblyName System.Drawing

$SRC = $env:DOVAH_REF
$OUT = $env:DOVAH_OUT
$DEBUG_PATH = $env:DOVAH_DEBUG

$PAD_LEFT  = 170                       # room for the lifted tip
$CUT       = @( 390.0, 655.0 )         # on the tail centreline, where the leg crosses it
$TAIL_DIR  = @( -0.695, 0.719 )        # unit vector pointing AWAY from the body, down-left
$SEED      = @( 200, 800 )             # a pixel unambiguously inside the tail
$LIFT_DEG  = if ($env:DOVAH_LIFT) { [double]$env:DOVAH_LIFT } else { 20.0 }
$EXTRUDE   = 0.0                     # how far the root is swept back behind the hip

# ---------------------------------------------------------------- load and pad
$src = [System.Drawing.Bitmap]::FromFile($SRC)
$origW = $src.Width; $origH = $src.Height
$imgW = $origW + $PAD_LEFT; $imgH = $origH
$padded = New-Object System.Drawing.Bitmap ($imgW, $imgH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$pg = [System.Drawing.Graphics]::FromImage($padded)
$pg.Clear([System.Drawing.Color]::White)
$pg.DrawImage($src, $PAD_LEFT, 0, $origW, $origH)
$pg.Dispose(); $src.Dispose()

$rect = New-Object System.Drawing.Rectangle 0, 0, $imgW, $imgH
$pd = $padded.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $pd.Stride
$buf = New-Object byte[] ($stride * $imgH)
[System.Runtime.InteropServices.Marshal]::Copy($pd.Scan0, $buf, 0, $buf.Length)
$padded.UnlockBits($pd); $padded.Dispose()

# shift the measured coordinates into padded space
$cutX = $CUT[0] + $PAD_LEFT; $cutY = $CUT[1]
$seedX = [int]($SEED[0] + $PAD_LEFT); $seedY = [int]$SEED[1]

$luma = New-Object 'int[]' ($imgW * $imgH)
for ($y = 0; $y -lt $imgH; $y++) {
    $rb = $y * $stride; $lr = $y * $imgW
    for ($x = 0; $x -lt $imgW; $x++) {
        $b = $rb + ($x * 4)
        $luma[$lr + $x] = [int]((0.2126 * [int]$buf[$b+2]) + (0.7152 * [int]$buf[$b+1]) + (0.0722 * [int]$buf[$b]))
    }
}

# ---------------------------------------------------------------- isolate the tail
if ($luma[($seedY * $imgW) + $seedX] -gt 226) {
    Write-Output ("ABORT: seed ({0},{1}) is background, not tail" -f $seedX, $seedY); exit 1
}
$isTail = New-Object 'bool[]' ($imgW * $imgH)
$qx = New-Object 'int[]' ($imgW * $imgH); $qy = New-Object 'int[]' ($imgW * $imgH)
$head = 0; $tail = 0
$qx[$tail] = $seedX; $qy[$tail] = $seedY; $tail++
$isTail[($seedY * $imgW) + $seedX] = $true
$tailCount = 0
$tMinX = $imgW; $tMaxX = -1; $tMinY = $imgH; $tMaxY = -1
while ($head -lt $tail) {
    $ax = $qx[$head]; $ay = $qy[$head]; $head++
    $tailCount++
    if ($ax -lt $tMinX) { $tMinX = $ax }; if ($ax -gt $tMaxX) { $tMaxX = $ax }
    if ($ay -lt $tMinY) { $tMinY = $ay }; if ($ay -gt $tMaxY) { $tMaxY = $ay }
    foreach ($st in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
        $nx = $ax + $st[0]; $ny = $ay + $st[1]
        if (($nx -lt 0) -or ($ny -lt 0) -or ($nx -ge $imgW) -or ($ny -ge $imgH)) { continue }
        $ni = ($ny * $imgW) + $nx
        if ($isTail[$ni]) { continue }
        if ($luma[$ni] -gt 226) { continue }
        # the CUT: only pixels on the tail side of the half-plane
        $proj = (($nx - $cutX) * $TAIL_DIR[0]) + (($ny - $cutY) * $TAIL_DIR[1])
        if ($proj -le 0.0) { continue }
        $isTail[$ni] = $true
        $qx[$tail] = $nx; $qy[$tail] = $ny; $tail++
    }
}
Write-Output ("tail isolated : {0} px   bbox x {1}..{2}  y {3}..{4}" -f $tailCount, $tMinX, $tMaxX, $tMinY, $tMaxY)

# debug: write the isolated tail on its own so it can be LOOKED AT before anything is moved
if ($DEBUG_PATH) {
    $debugBmp = New-Object System.Drawing.Bitmap ($imgW, $imgH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $dd = $debugBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $db = New-Object byte[] ($dd.Stride * $imgH)
    for ($y = 0; $y -lt $imgH; $y++) {
        for ($x = 0; $x -lt $imgW; $x++) {
            $o = ($y * $dd.Stride) + ($x * 4); $i = ($y * $imgW) + $x; $s = ($y * $stride) + ($x * 4)
            if ($isTail[$i]) { $db[$o]=$buf[$s]; $db[$o+1]=$buf[$s+1]; $db[$o+2]=$buf[$s+2]; $db[$o+3]=255 }
            else { $db[$o]=250; $db[$o+1]=225; $db[$o+2]=225; $db[$o+3]=255 }   # pale red = NOT taken
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($db, 0, $dd.Scan0, $db.Length)
    $debugBmp.UnlockBits($dd); $debugBmp.Save($DEBUG_PATH, [System.Drawing.Imaging.ImageFormat]::Png); $debugBmp.Dispose()
    Write-Output ("debug mask    : {0}" -f $DEBUG_PATH)
}

# ---------------------------------------------------------------- erase, rotate, recompose
# base = the image with the tail removed
$baseBuf = New-Object byte[] $buf.Length
[Array]::Copy($buf, $baseBuf, $buf.Length)
for ($y = 0; $y -lt $imgH; $y++) {
    for ($x = 0; $x -lt $imgW; $x++) {
        if ($isTail[($y * $imgW) + $x]) {
            $o = ($y * $stride) + ($x * 4)
            $baseBuf[$o]=255; $baseBuf[$o+1]=255; $baseBuf[$o+2]=255; $baseBuf[$o+3]=255
        }
    }
}

# BEND, DO NOT ROTATE RIGIDLY.
#
# A rigid rotation moves the root as much as the tip, so the tail tears away from the hip and
# leaves a cut that has to be patched. Patching it - by extruding the cut cross-section - fills
# the hole but drags the tail's own keylines and stripe into the body as straight bands, and
# THAT is the seam that stayed visible.
#
# So the angle RAMPS along the tail instead: 0 degrees at the root, rising to the full lift
# further out. At the root nothing moves at all, so there is no join to blend - the pixels are
# written back exactly where they came from. The tail curves out of the hip the way a real one
# does when it is raised.
#
# This is exact, not an approximation: rotation about the pivot PRESERVES DISTANCE FROM THE
# PIVOT, so a destination pixel's radius is the same as its source's. The angle to undo is
# therefore known from the destination alone, and the inverse map needs no search.
$R_BLEND = 300.0        # over what radius the bend is spread
function LiftAngleAt {
    param([double]$radius)
    $t = $radius / $R_BLEND
    if ($t -ge 1.0) { return $LIFT_DEG }
    # SMOOTHSTEP, not a power law. A power ramp is flat at the root but arrives at the full
    # angle with slope still climbing, so the curvature jumps at R_BLEND and leaves a faint
    # KINK across the tail. 3t^2-2t^3 has zero slope at BOTH ends: the bend eases out of the
    # hip and eases into the straight run, with no discontinuity anywhere to catch the eye.
    return $LIFT_DEG * ((3.0 * $t * $t) - (2.0 * $t * $t * $t))
}
$outBmp = New-Object System.Drawing.Bitmap ($imgW, $imgH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$od = $outBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$ob = New-Object byte[] ($od.Stride * $imgH)
[Array]::Copy($baseBuf, $ob, $baseBuf.Length)

$painted = 0; $blocked = 0
for ($y = 0; $y -lt $imgH; $y++) {
    for ($x = 0; $x -lt $imgW; $x++) {
        # where did this destination pixel come from in the unbent tail?
        # radius is invariant under the bend, so the angle to undo is known from here alone.
        $dx = $x - $cutX; $dy = $y - $cutY
        $radius = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
        $degHere = LiftAngleAt $radius
        $undo = $degHere * [Math]::PI / 180.0     # +ve undoes the lift (screen y is DOWN)
        $cosI = [Math]::Cos($undo); $sinI = [Math]::Sin($undo)
        $sx = ($dx * $cosI) + ($dy * $sinI) + $cutX
        $sy = (-$dx * $sinI) + ($dy * $cosI) + $cutY
        # EXTRUDE THE ROOT. Rotating a cut shape about the centre of its cut leaves a wedge:
        # the cut's corners swing by +/- halfWidth*sin(theta), about 22px at 20deg on a 65px
        # half-width, and that wedge showed as a WHITE NOTCH between the tail and the leg.
        #
        # Filling it with a flat colour would be a patch. Instead the tail's cut cross-section
        # is swept BACKWARD along the tail's own axis, so the root genuinely carries on behind
        # the hip - which is what a real tail does. Anything that lands where the body already
        # is gets rejected by the under-the-dragon test below, so the extrusion is only ever
        # visible in the gap it exists to close.
        $proj = (($sx - $cutX) * $TAIL_DIR[0]) + (($sy - $cutY) * $TAIL_DIR[1])
        if ($proj -lt 0.0) {
            if ($proj -lt -$EXTRUDE) { continue }
            # slide the sample point back onto the cut line (just past it, since the flood
            # required proj > 0 strictly and the line itself is not in the mask)
            $slide = 1.5 - $proj
            $sx = $sx + ($slide * $TAIL_DIR[0])
            $sy = $sy + ($slide * $TAIL_DIR[1])
        }
        $ix = [int][Math]::Round($sx); $iy = [int][Math]::Round($sy)
        if (($ix -lt 0) -or ($iy -lt 0) -or ($ix -ge $imgW) -or ($iy -ge $imgH)) { continue }
        if (-not $isTail[($iy * $imgW) + $ix]) { continue }
        $o = ($y * $od.Stride) + ($x * 4)
        # UNDER the dragon: never overwrite anything still standing in the base image
        $bl = [int]((0.2126*[int]$baseBuf[$o+2]) + (0.7152*[int]$baseBuf[$o+1]) + (0.0722*[int]$baseBuf[$o]))
        if ($bl -le 240) { $blocked++; continue }
        $s = ($iy * $stride) + ($ix * 4)
        $ob[$o] = $buf[$s]; $ob[$o+1] = $buf[$s+1]; $ob[$o+2] = $buf[$s+2]; $ob[$o+3] = 255
        $painted++
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($ob, 0, $od.Scan0, $ob.Length)
$outBmp.UnlockBits($od)
$outBmp.Save($OUT, [System.Drawing.Imaging.ImageFormat]::Png)
$outBmp.Dispose()
Write-Output ("lift {0} deg  : {1} px placed, {2} px fell behind the body" -f $LIFT_DEG, $painted, $blocked)
Write-Output ("canvas        : {0} x {1}  (padded {2}px on the left)" -f $imgW, $imgH, $PAD_LEFT)
Write-Output ("wrote         : {0}" -f $OUT)



