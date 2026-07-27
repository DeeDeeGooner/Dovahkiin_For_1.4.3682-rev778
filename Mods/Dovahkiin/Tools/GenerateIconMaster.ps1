# Shout-icon master, option B3: the comet with A RimWorld of Magic's treatment.
#
# What RWoM's icons actually do, established by reading their files:
#   - 256x256, not 128 (426 of their PNGs are 128, but 413 are 256 and their ability icons skew
#     to 256; RimWorld downsamples, so a larger master just renders crisper)
#   - a solid shape with a DARK OUTLINE, not a soft smoke wisp
#   - a hot near-white core inside saturated colour
# So B3 makes the comet more solid, rims it, and puts a hot centre in the head.
#
# The outline is made by stamping a thresholded dark silhouette at offsets around a circle -
# a cheap dilation. A true max-filter dilation at this resolution is far too slow in PowerShell.
#
# PowerShell traps avoided: variable names are CASE-INSENSITIVE ($out/$OUT, $final/$FINAL
# collide silently); Drawing2D enums are [System.Drawing.Drawing2D.X]::Y.
Add-Type -AssemblyName System.Drawing

$DESTPATH = "C:\Users\User\AppData\Local\Temp\claude\C--Games-Rimworld-RimWorld-RimWorldFolder-DovahkiinClaudePluged\fa5ecdd8-cfc2-4e0c-b98b-1560cbcd8092\scratchpad\fireball3_master.png"
$SIZE = 256          # matches RWoM's ability-icon size
$SS   = 4
$N    = $SIZE * $SS  # 1024 working resolution

# ---------------------------------------------------------------------------------
# 1. Draw the comet, SOLID this time (higher alpha, less falloff) so it can take a rim.
# ---------------------------------------------------------------------------------
$shape = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($shape)
$g.Clear([System.Drawing.Color]::FromArgb(0,255,255,255))
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

$cx = $N / 2.0
$cy = $N / 2.0
$R  = $N * 0.40      # slightly tighter, to leave room for the rim

$seed = 90210
function NextRand {
  $script:seed = ($script:seed * 1103515245 + 12345) -band 0x7FFFFFFF
  return $script:seed / 2147483647.0
}
# $v is the GREY VALUE, and it matters: the tint pass reads luminance to decide how saturated
# a pixel becomes and where the hot core is. A shape drawn flat white has luminance 1.0
# everywhere, so the whole icon gets treated as core and bleaches to white - which is exactly
# what the first B3 did, and why Fire, Frost and Drain came out looking identical.
function Dot([double]$x, [double]$y, [double]$r, [int]$a, [int]$v = 255) {
  if ($a -le 0 -or $r -le 0) { return }
  if ($a -gt 255) { $a = 255 }
  if ($v -gt 255) { $v = 255 }
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a,$v,$v,$v))
  $g.FillEllipse($brush, [single]($x-$r), [single]($y-$r), [single]($r*2), [single]($r*2))
  $brush.Dispose()
}

$headAngle = -2.35
$headDist  = $R * 0.28
$hx = $cx + [Math]::Cos($headAngle) * $headDist
$hy = $cy + [Math]::Sin($headAngle) * $headDist
$SWEEP = 4.55

# Tail - now near-opaque along most of its length so it reads as a solid form.
$steps = 520
for ($s = 0; $s -lt $steps; $s++) {
  $t = $s / [double]($steps - 1)
  $theta = $headAngle + $t * $SWEEP
  $rad = $headDist + ($R * 0.64) * [Math]::Pow($t, 0.70)
  $wav = [Math]::Sin($t * 8.0) * $R * 0.013 * $t
  $x = $cx + [Math]::Cos($theta) * ($rad + $wav)
  $y = $cy + [Math]::Sin($theta) * ($rad + $wav)
  $thick = $R * (0.250 * [Math]::Pow(1.0 - $t, 0.92) + 0.022)
  # 255 flat for the first two-thirds, then a quick fade at the very tip only.
  $alpha = 255
  if ($t -gt 0.72) { $alpha = [int](255 * (1.0 - (($t - 0.72) / 0.28))) }
  # Shading along the tail: brighter where it leaves the head, darker toward the tip.
  # Stays well below the hot-core threshold so the tail takes full colour from the tint.
  $val = [int](205 - 60 * $t)
  Dot $x $y $thick $alpha $val
}

# Sparks, also solid.
for ($k = 0; $k -lt 6; $k++) {
  $t0 = 0.30 + (NextRand) * 0.52
  $theta = $headAngle + $t0 * $SWEEP + ((NextRand) * 0.22 - 0.11)
  $rad0 = $headDist + ($R * 0.64) * [Math]::Pow($t0, 0.70)
  $reach = $R * (0.10 + (NextRand) * 0.13)
  $len = 18
  for ($q = 0; $q -lt $len; $q++) {
    $u = $q / [double]$len
    $rr = $rad0 + $u * $reach
    $th = $theta + $u * 0.26
    $x = $cx + [Math]::Cos($th) * $rr
    $y = $cy + [Math]::Sin($th) * $rr
    Dot $x $y ($R * 0.030 * (1.0 - $u) + 1) ([int](255 * (1.0 - $u * 0.75))) 185
  }
}

# Head - solid disc, brighter than the tail but still short of the core threshold, so it
# takes strong colour rather than bleaching.
Dot $hx $hy ($R * 0.235) 255 215
$g.Dispose()

# ---------------------------------------------------------------------------------
# 2. Dark silhouette, thresholded, for the rim.
# ---------------------------------------------------------------------------------
$rectN = New-Object System.Drawing.Rectangle -ArgumentList ([int]0),([int]0),([int]$N),([int]$N)
$sd = $shape.LockBits($rectN, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$nBytes = $sd.Stride * $N
$sBuf = New-Object byte[] $nBytes
[System.Runtime.InteropServices.Marshal]::Copy($sd.Scan0, $sBuf, 0, $nBytes)
$shape.UnlockBits($sd)

$dark = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$dd = $dark.LockBits($rectN, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$dBuf = New-Object byte[] $nBytes
for ($i = 0; $i -lt $nBytes; $i += 4) {
  if ($sBuf[$i+3] -ge 110) {
    $dBuf[$i] = 18; $dBuf[$i+1] = 16; $dBuf[$i+2] = 14; $dBuf[$i+3] = 255
  }
}
[System.Runtime.InteropServices.Marshal]::Copy($dBuf, 0, $dd.Scan0, $nBytes)
$dark.UnlockBits($dd)

# ---------------------------------------------------------------------------------
# 3. Composite: rim (stamped offsets) -> shape -> hot core.
# ---------------------------------------------------------------------------------
$comp = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cg = [System.Drawing.Graphics]::FromImage($comp)
$cg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$cg.Clear([System.Drawing.Color]::FromArgb(0,255,255,255))

$RIM = $N * 0.020      # rim thickness in working pixels
$STAMPS = 20
for ($k = 0; $k -lt $STAMPS; $k++) {
  $a = ($k / [double]$STAMPS) * 2 * [Math]::PI
  $ox = [Math]::Cos($a) * $RIM
  $oy = [Math]::Sin($a) * $RIM
  $cg.DrawImage($dark, [single]$ox, [single]$oy)
}
$cg.DrawImage($shape, 0, 0)

# Hot near-white core in the head, like RWoM's bright centres.
$cg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
for ($i = 40; $i -ge 1; $i--) {
  $t = $i / 40.0
  $rad = $R * 0.165 * $t
  $al = [int](26 * (1.0 - $t) + 5)
  $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($al,255,255,255))
  $cg.FillEllipse($brush, [single]($hx-$rad), [single]($hy-$rad), [single]($rad*2), [single]($rad*2))
  $brush.Dispose()
}
$cg.Dispose()
$shape.Dispose(); $dark.Dispose()

# ---------------------------------------------------------------------------------
# 4. Recentre by alpha centroid, downsample to 256.
# ---------------------------------------------------------------------------------
$cd = $comp.LockBits($rectN, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$cBuf = New-Object byte[] $nBytes
[System.Runtime.InteropServices.Marshal]::Copy($cd.Scan0, $cBuf, 0, $nBytes)
$comp.UnlockBits($cd)
[double]$mx=0; [double]$my=0; [double]$mw=0
for ($y=0; $y -lt $N; $y+=2) {
  $row = $y*$cd.Stride
  for ($x=0; $x -lt $N; $x+=2) {
    $a = $cBuf[$row+$x*4+3]
    if ($a -gt 8) { $mx += $x*$a; $my += $y*$a; $mw += $a }
  }
}
$shiftX = 0.0; $shiftY = 0.0
if ($mw -gt 0) { $shiftX = ($N/2.0) - ($mx/$mw); $shiftY = ($N/2.0) - ($my/$mw) }

$outBmp = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$fg = [System.Drawing.Graphics]::FromImage($outBmp)
$fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$fg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$fg.Clear([System.Drawing.Color]::FromArgb(0,255,255,255))
$destRect = New-Object System.Drawing.Rectangle -ArgumentList `
  ([int][Math]::Round($shiftX / $SS)), ([int][Math]::Round($shiftY / $SS)), ([int]$SIZE), ([int]$SIZE)
$fg.DrawImage($comp, $destRect)
$fg.Dispose()
$comp.Dispose()

$outBmp.Save($DESTPATH, [System.Drawing.Imaging.ImageFormat]::Png)
$outBmp.Dispose()

$fi = Get-Item $DESTPATH
Write-Output ("written: {0}  {1}x{1}  ({2} KB)" -f $fi.Name, $SIZE, [int]($fi.Length/1KB))
