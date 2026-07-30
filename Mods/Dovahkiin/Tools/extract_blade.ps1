# EXTRACT THE BLADE THE USER PAINTED.
#
# They took a render of candidate A and painted WHITE over every part of the blade they want
# gone. This reads that image and turns it into polygon numbers in the generator's OWN
# (outward, along) blade coordinates, ready to paste into $BLADE. No eyeballing.
#
# Method:
#   1. classify their screenshot into WHITE (the mask) / GROUND / AXE
#   2. do the same to the render they painted over, so the two can be aligned
#   3. solve scale+offset from the topmost and bottommost AXE pixel in each - the long spike
#      tip and the bottom of the ring pommel, both sharp, both outside the painted area
#   4. rasterise the KNOWN blade polygon, subtract the mask, keep the largest blob
#   5. trace that blob's boundary and simplify it (Douglas-Peucker)
#   6. convert each surviving point back through the blade basis the generator published
#
# Results go to a file: this project's PowerShell hosts eat console output when they die, and
# a silent empty result is the dangerous failure here, not an error.
#
# ============================================================================================
# TO RE-RUN THIS, FIRST EMPTY $TRACED_BLADE IN GenerateAncientAxeDragonbone.ps1
#
# This dot-sources that generator to get the blade polygon and the coordinate basis, and it
# measures (that polygon MINUS the paint). With $TRACED_BLADE set - which it is, because the
# trace shipped - it would measure the ALREADY-TRACED blade rather than the original that was
# painted over, and silently give a different answer. That exact mistake happened during the
# original run: 2262px of blade instead of 4659, with no error anywhere.
#
# The generator keeps the original fan polygon in place for this reason, with a comment saying
# it must not be edited. Empty $TRACED_BLADE, run this, put the result back.
#
# It also needs axe_candidate_A.png, which the generator writes to $env:DOVAH_PREVIEW.
# ============================================================================================
Add-Type -AssemblyName System.Drawing

$SCRATCH = $PSScriptRoot
$OUT = Join-Path $SCRATCH "extract_blade.txt"
if (Test-Path $OUT) { Remove-Item $OUT }
function Say([string]$m) { Add-Content $OUT $m }

# Kept in the repo beside this script, so the trace does not depend on a file in someone's
# Downloads folder that will be cleared out.
$PAINTED = Join-Path $PSScriptRoot "BladeTraceReference.png"
if (-not (Test-Path $PAINTED)) { $PAINTED = "C:\Users\User\Downloads\Capture-battleaxe.PNG" }
$GEN = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Tools\GenerateAncientAxeDragonbone.ps1"

$env:DOVAH_PREVIEW = $SCRATCH
. $GEN | Out-Null
# re-run for variant A specifically: the dot-source above leaves the vars set from the LAST
# variant built (the straight one), which is the wrong basis.
$tmp = BuildAxe $BOW
$tmp.Dispose()
$basis = $BLADE_BASIS
$verts = $BLADE_VERTS
Say ("blade basis: anchor ({0:N4}, {1:N4})  out {2:N4}  along {3:N4}" -f $basis.AnchorX, $basis.AnchorY, $basis.Out, $basis.Along)
Say ("            hu ({0:N4}, {1:N4})  hn ({2:N4}, {3:N4})" -f $basis.Hux, $basis.Huy, $basis.Hnx, $basis.Hny)
Say ""

# ---------- load both images as byte arrays ----------
function LoadRGBA([string]$path) {
  $bmp = New-Object System.Drawing.Bitmap $path
  $w = $bmp.Width; $h = $bmp.Height
  $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object 'byte[]' ($data.Stride * $h)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $stride = $data.Stride
  $bmp.UnlockBits($data); $bmp.Dispose()
  return @{ W = $w; H = $h; Stride = $stride; Bytes = $bytes }
}

$pImg = LoadRGBA $PAINTED
$rImg = LoadRGBA (Join-Path $SCRATCH "axe_candidate_A.png")
Say ("painted image : {0}x{1}" -f $pImg.W, $pImg.H)
Say ("render        : {0}x{1}" -f $rImg.W, $rImg.H)

# ---------- classify the painted screenshot ----------
# BGRA order in memory.
# WHITE  = the mask the user painted
# GROUND = the tan preview terrain, R>G>B and mid-brightness
# AXE    = anything else: blue blade, orange haft, dark keyline
# DO NOT try to identify the ground by its colour band. The first attempt did and classified
# the ENTIRE screenshot as axe (extremes x 0..516), because a rescaled screenshot's ground no
# longer sits inside the narrow band the generator drew, and the image has a dark border too.
# The alignment then solved to nonsense and the whole extraction silently produced 4 points.
#
# Align on colours that cannot be confused instead, and that the paint never touches:
#   ORANGE  r>170, b<100, r-b>90   -> the lower haft and ring pommel. Ground never reaches
#                                     an r-b gap of 90; it sits around 22..60.
#   BLUE    b>r+25, b>110          -> the blade and upper haft. Ground has r>b always.
# The white paint is neither, so both survive it.
$CLS_WHITE = 1
$pWhite = New-Object 'byte[]' ($pImg.W * $pImg.H)
$pOrange = New-Object 'byte[]' ($pImg.W * $pImg.H)
$pBlue = New-Object 'byte[]' ($pImg.W * $pImg.H)
for ($y = 0; $y -lt $pImg.H; $y++) {
  $row = $y * $pImg.Stride
  for ($x = 0; $x -lt $pImg.W; $x++) {
    $i = $row + $x*4
    $b = $pImg.Bytes[$i]; $g = $pImg.Bytes[$i+1]; $r = $pImg.Bytes[$i+2]
    $idx = $y*$pImg.W + $x
    $mx = [Math]::Max($r, [Math]::Max($g, $b)); $mn = [Math]::Min($r, [Math]::Min($g, $b))
    # CATCH THE ANTI-ALIASED FRINGE TOO. At >225/<22 only the solid core of each brush stroke
    # counted as white, so a 1-2px semi-transparent rim along every stroke edge came through as
    # "kept" - and that fringe is what produced the zig-zag in the first traced polygon, which
    # was then wrongly blamed on the drawing being rough. The strokes are straight-edged.
    if ($mx -gt 198 -and ($mx - $mn) -lt 46) { $pWhite[$idx] = 1; continue }
    if ($r -gt 170 -and $b -lt 100 -and ($r - $b) -gt 90) { $pOrange[$idx] = 1 }
    elseif ($b -gt ($r + 25) -and $b -gt 110) { $pBlue[$idx] = 1 }
  }
}
$rOrange = New-Object 'byte[]' ($rImg.W * $rImg.H)
$rBlue = New-Object 'byte[]' ($rImg.W * $rImg.H)
for ($y = 0; $y -lt $rImg.H; $y++) {
  $row = $y * $rImg.Stride
  for ($x = 0; $x -lt $rImg.W; $x++) {
    $i = $row + $x*4
    if ($rImg.Bytes[$i+3] -le 24) { continue }
    $b = $rImg.Bytes[$i]; $g = $rImg.Bytes[$i+1]; $r = $rImg.Bytes[$i+2]
    $idx = $y*$rImg.W + $x
    if ($r -gt 170 -and $b -lt 100 -and ($r - $b) -gt 90) { $rOrange[$idx] = 1 }
    elseif ($b -gt ($r + 25) -and $b -gt 110) { $rBlue[$idx] = 1 }
  }
}
function MaskExtremes($m, [int]$w, [int]$h) {
  $minY = 99999; $maxY = -1; $minX = 99999; $maxX = -1; $n = 0
  for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
      if ($m[$y*$w + $x] -eq 1) {
        $n++
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
      }
    }
  }
  return @($minX, $maxX, $minY, $maxY, $n)
}
$po = MaskExtremes $pOrange $pImg.W $pImg.H
$pb = MaskExtremes $pBlue  $pImg.W $pImg.H
$ro = MaskExtremes $rOrange $rImg.W $rImg.H
$rb = MaskExtremes $rBlue  $rImg.W $rImg.H
$pw = MaskExtremes $pWhite $pImg.W $pImg.H
Say ""
Say ("painted ORANGE: x {0}..{1} y {2}..{3}  n={4}" -f $po[0], $po[1], $po[2], $po[3], $po[4])
Say ("render  ORANGE: x {0}..{1} y {2}..{3}  n={4}" -f $ro[0], $ro[1], $ro[2], $ro[3], $ro[4])
Say ("painted BLUE  : x {0}..{1} y {2}..{3}  n={4}" -f $pb[0], $pb[1], $pb[2], $pb[3], $pb[4])
Say ("render  BLUE  : x {0}..{1} y {2}..{3}  n={4}" -f $rb[0], $rb[1], $rb[2], $rb[3], $rb[4])
Say ("painted WHITE : x {0}..{1} y {2}..{3}  n={4}" -f $pw[0], $pw[1], $pw[2], $pw[3], $pw[4])

# Landmarks untouched by the paint (which is all on the blade's right and lower-right):
#   bottom of the ring pommel (orange maxY), left of the ring (orange minX),
#   the long spike tip (blue minY and blue minX - the spikes point up-LEFT)
# Scale from the long vertical baseline, then cross-check on x.
$scaleY = ($po[3] - $pb[2]) / [double]($ro[3] - $rb[2])
$scaleX = ($po[1] - $po[0]) / [double]($ro[1] - $ro[0])
Say ""
Say ("scale from ring-bottom to spike-tip: {0:N4}" -f $scaleY)
Say ("scale from the ring's own width    : {0:N4}   (cross-check)" -f $scaleX)
$scale = $scaleY
$offY = $pb[2] - ($rb[2] * $scale)
$offX = $po[0] - ($ro[0] * $scale)
Say ("using scale {0:N4}, offset ({1:N2}, {2:N2})" -f $scale, $offX, $offY)
# verify: the ring bottom should land where it lands
$predRingBottom = ($ro[3] * $scale) + $offY
Say ("check: ring bottom predicted y {0:N1}, actually {1}  (delta {2:N1}px)" -f $predRingBottom, $po[3], ($predRingBottom - $po[3]))
$predSpikeLeft = ($rb[0] * $scale) + $offX
Say ("check: spike-tip left predicted x {0:N1}, actually {1}  (delta {2:N1}px)" -f $predSpikeLeft, $pb[0], ($predSpikeLeft - $pb[0]))

# ---------- rasterise the known blade polygon in the RENDER's 256 frame ----------
$RN = $rImg.W          # 256
$bladeMask = New-Object 'byte[]' ($RN * $RN)
$poly = @()
foreach ($v in $verts) { $poly += (New-Object System.Drawing.PointF ([single]($v[0]*$RN)), ([single]($v[1]*$RN))) }
$mb = New-Object System.Drawing.Bitmap $RN, $RN, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mg = [System.Drawing.Graphics]::FromImage($mb)
$mg.Clear([System.Drawing.Color]::Black)
$mg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$brW = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$mg.FillPolygon($brW, [System.Drawing.PointF[]]$poly)
$brW.Dispose(); $mg.Dispose()
$md = $mb.LockBits((New-Object System.Drawing.Rectangle 0,0,$RN,$RN), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mbytes = New-Object 'byte[]' ($md.Stride * $RN)
[System.Runtime.InteropServices.Marshal]::Copy($md.Scan0, $mbytes, 0, $mbytes.Length)
$mstride = $md.Stride
$mb.UnlockBits($md); $mb.Dispose()
$bladeCount = 0
for ($y = 0; $y -lt $RN; $y++) {
  for ($x = 0; $x -lt $RN; $x++) {
    if ($mbytes[$y*$mstride + $x*4] -gt 127) { $bladeMask[$y*$RN + $x] = 1; $bladeCount++ }
  }
}
Say ""
Say ("blade polygon covers $bladeCount px of the 256 frame")

# ---------- subtract the painted-out region ----------
$kept = New-Object 'byte[]' ($RN * $RN)
$keptCount = 0; $removedCount = 0
for ($y = 0; $y -lt $RN; $y++) {
  for ($x = 0; $x -lt $RN; $x++) {
    if ($bladeMask[$y*$RN + $x] -eq 0) { continue }
    $px = [int][Math]::Round(($x * $scale) + $offX)
    $py = [int][Math]::Round(($y * $scale) + $offY)
    if ($px -lt 0 -or $py -lt 0 -or $px -ge $pImg.W -or $py -ge $pImg.H) { continue }
    if ($pWhite[$py*$pImg.W + $px] -eq 1) { $removedCount++; continue }
    $kept[$y*$RN + $x] = 1; $keptCount++
  }
}
Say ("of that: KEPT $keptCount px, painted out $removedCount px  ({0:N1}% removed)" -f (100.0*$removedCount/[Math]::Max(1,$bladeCount)))

# ---------- morphological cleanup, so the trace follows the DRAWN edges ----------
# CLOSE then OPEN, 4-neighbour, one pixel each. Close fills the pinholes left where the paint
# was slightly translucent; open removes isolated specks. Without this the contour tracer walks
# every single-pixel wobble and Douglas-Peucker faithfully preserves it as a spurious vertex -
# which is exactly what made the first trace look like a scribble.
function Dilate($src, [int]$n) {
  $dst = New-Object 'byte[]' ($n * $n)
  for ($yy = 0; $yy -lt $n; $yy++) {
    for ($xx = 0; $xx -lt $n; $xx++) {
      if ($src[$yy*$n + $xx] -eq 1) { $dst[$yy*$n + $xx] = 1; continue }
      $hit = 0
      foreach ($d in @(@((1),(0)), @((-1),(0)), @((0),(1)), @((0),(-1)))) {
        $ax = $xx + $d[0]; $ay = $yy + $d[1]
        if ($ax -lt 0 -or $ay -lt 0 -or $ax -ge $n -or $ay -ge $n) { continue }
        if ($src[$ay*$n + $ax] -eq 1) { $hit = 1; break }
      }
      $dst[$yy*$n + $xx] = $hit
    }
  }
  return $dst
}
function Erode($src, [int]$n) {
  $dst = New-Object 'byte[]' ($n * $n)
  for ($yy = 0; $yy -lt $n; $yy++) {
    for ($xx = 0; $xx -lt $n; $xx++) {
      if ($src[$yy*$n + $xx] -ne 1) { continue }
      $all = 1
      foreach ($d in @(@((1),(0)), @((-1),(0)), @((0),(1)), @((0),(-1)))) {
        $ax = $xx + $d[0]; $ay = $yy + $d[1]
        if ($ax -lt 0 -or $ay -lt 0 -or $ax -ge $n -or $ay -ge $n) { continue }
        if ($src[$ay*$n + $ax] -ne 1) { $all = 0; break }
      }
      $dst[$yy*$n + $xx] = $all
    }
  }
  return $dst
}
$kept = Erode (Dilate $kept $RN) $RN      # close
$kept = Dilate (Erode $kept $RN) $RN      # open
$cleanCount = 0
for ($ci = 0; $ci -lt ($RN*$RN); $ci++) { if ($kept[$ci] -eq 1) { $cleanCount++ } }
Say ("after close+open cleanup: $cleanCount px")

# ---------- largest connected blob of kept pixels ----------
$label = New-Object 'int[]' ($RN * $RN)
$best = 0; $bestLabel = 0; $next = 0
$stack = New-Object System.Collections.Generic.Stack[int]
for ($flatIdx = 0; $flatIdx -lt ($RN*$RN); $flatIdx++) {
  if ($kept[$flatIdx] -ne 1 -or $label[$flatIdx] -ne 0) { continue }
  $next++
  $size = 0
  $stack.Push($flatIdx)
  $label[$flatIdx] = $next
  while ($stack.Count -gt 0) {
    $cur = $stack.Pop(); $size++
    $cx = $cur % $RN; $cy = [int][Math]::Floor($cur / $RN)
    foreach ($d in @(@((1),(0)), @((-1),(0)), @((0),(1)), @((0),(-1)))) {
      $nx = $cx + $d[0]; $ny = $cy + $d[1]
      if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $RN -or $ny -ge $RN) { continue }
      $ni = $ny*$RN + $nx
      if ($kept[$ni] -eq 1 -and $label[$ni] -eq 0) { $label[$ni] = $next; $stack.Push($ni) }
    }
  }
  if ($size -gt $best) { $best = $size; $bestLabel = $next }
}
Say ("connected blobs: $next, largest $best px")

# ---------- trace the blob's boundary (Moore neighbourhood) ----------
$blob = New-Object 'byte[]' ($RN * $RN)
for ($flatIdx = 0; $flatIdx -lt ($RN*$RN); $flatIdx++) { if ($label[$flatIdx] -eq $bestLabel) { $blob[$flatIdx] = 1 } }
$startIdx = -1
for ($flatIdx = 0; $flatIdx -lt ($RN*$RN); $flatIdx++) { if ($blob[$flatIdx] -eq 1) { $startIdx = $flatIdx; break } }
$contour = @()
if ($startIdx -ge 0) {
  $nb = @(@((-1),(0)), @((-1),(-1)), @((0),(-1)), @((1),(-1)), @((1),(0)), @((1),(1)), @((0),(1)), @((-1),(1)))
  $sx = $startIdx % $RN; $sy = [int][Math]::Floor($startIdx / $RN)
  $cx = $sx; $cy = $sy; $dir = 0
  $guard = 0
  do {
    $contour += ,@(($cx), ($cy))
    $found = $false
    for ($k = 0; $k -lt 8; $k++) {
      $d = ($dir + 6 + $k) % 8
      $nx = $cx + $nb[$d][0]; $ny = $cy + $nb[$d][1]
      if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $RN -or $ny -ge $RN) { continue }
      if ($blob[$ny*$RN + $nx] -eq 1) { $cx = $nx; $cy = $ny; $dir = $d; $found = $true; break }
    }
    if (-not $found) { break }
    $guard++
  } while ((-not ($cx -eq $sx -and $cy -eq $sy)) -and $guard -lt 20000)
}
Say ("contour points: {0}" -f $contour.Count)

# ---------- Douglas-Peucker simplify ----------
function PerpDist($p, $a, $b) {
  $dx = $b[0] - $a[0]; $dy = $b[1] - $a[1]
  $den = [Math]::Sqrt(($dx*$dx) + ($dy*$dy))
  if ($den -lt 1e-9) { return [Math]::Sqrt(([Math]::Pow($p[0]-$a[0],2)) + ([Math]::Pow($p[1]-$a[1],2))) }
  return [Math]::Abs((($dy*($p[0]-$a[0])) - ($dx*($p[1]-$a[1])))) / $den
}
function DP($pts, [double]$eps) {
  if ($pts.Count -lt 3) { return $pts }
  $a = $pts[0]; $b = $pts[$pts.Count-1]
  $maxD = -1.0; $maxI = -1
  for ($i = 1; $i -lt ($pts.Count-1); $i++) {
    $d = PerpDist $pts[$i] $a $b
    if ($d -gt $maxD) { $maxD = $d; $maxI = $i }
  }
  if ($maxD -le $eps) { return @($a, $b) }
  $left = DP ($pts[0..$maxI]) $eps
  $right = DP ($pts[$maxI..($pts.Count-1)]) $eps
  return ($left[0..($left.Count-2)] + $right)
}

foreach ($eps in @(1.2, 1.8, 2.5)) {
  $simp = DP $contour $eps
  Say ""
  Say ("=== simplified at eps $eps : {0} points ===" -f $simp.Count)
  Say "     # (outward, along)     as a `$BLADE row"
  $rows = @()
  foreach ($p in $simp) {
    # pixel -> fractional frame -> (outward, along) through the published basis
    $fx = ($p[0] + 0.5) / [double]$RN
    $fy = ($p[1] + 0.5) / [double]$RN
    $dx = $fx - $basis.AnchorX
    $dy = $fy - $basis.AnchorY
    $o = (($dx * $basis.Hnx) + ($dy * $basis.Hny)) / $basis.Out
    $al = (($dx * $basis.Hux) + ($dy * $basis.Huy)) / $basis.Along
    $rows += ("    @( ({0,6:N3}), ({1,6:N3}) )," -f $o, $al)
  }
  foreach ($r in $rows) { Say $r }
}

# ---------- VERIFY BY EYE what the pixel work decided ----------
# A polygon of numbers is not checkable by reading it. Paint the kept region green and the
# painted-out region red over the original render, so the extraction can be confirmed before
# any of these numbers go near the generator.
$Z = 3
$dbg = New-Object System.Drawing.Bitmap ($RN*$Z), ($RN*$Z), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$dg = [System.Drawing.Graphics]::FromImage($dbg)
$dg.Clear([System.Drawing.Color]::FromArgb(255, 26, 28, 26))
$dg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$src = New-Object System.Drawing.Bitmap (Join-Path $SCRATCH "axe_candidate_A.png")
$dg.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, ($RN*$Z), ($RN*$Z)))
$src.Dispose()
$brKeep = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, 60, 235, 90))
$brGone = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, 240, 60, 60))
for ($y = 0; $y -lt $RN; $y++) {
  for ($x = 0; $x -lt $RN; $x++) {
    if ($bladeMask[$y*$RN + $x] -eq 0) { continue }
    $br = if ($kept[$y*$RN + $x] -eq 1) { $brKeep } else { $brGone }
    $dg.FillRectangle($br, ($x*$Z), ($y*$Z), $Z, $Z)
  }
}
$brKeep.Dispose(); $brGone.Dispose()
# outline the simplified polygon we are about to adopt, in white, on top
$simpFinal = DP $contour 1.8
$penP = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]2.5)
$ptsP = @()
foreach ($p in $simpFinal) { $ptsP += (New-Object System.Drawing.Point ([int](($p[0]+0.5)*$Z)), ([int](($p[1]+0.5)*$Z))) }
if ($ptsP.Count -ge 3) { $dg.DrawPolygon($penP, [System.Drawing.Point[]]$ptsP) }
$penP.Dispose()
$fD = New-Object System.Drawing.Font "Segoe UI", 13, ([System.Drawing.FontStyle]::Bold)
$brD = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$dg.DrawString("GREEN = you kept it    RED = you painted it out    WHITE = the polygon fitted to it", $fD, $brD, [single]8, [single]8)
$dg.Dispose()
$dbgPath = Join-Path $SCRATCH "extract_debug.png"
$dbg.Save($dbgPath, [System.Drawing.Imaging.ImageFormat]::Png)
$dbg.Dispose()
Say ""
Say ("wrote verification image " + $dbgPath)
Say ""
Say "DONE"




