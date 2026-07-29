# =====================================================================================
#  The Ancient Dragonborn's spectral greataxe.
#
#  Palette is taken from GenerateDragonAspect.ps1 deliberately - the axe and the armour are
#  the same conjuration and must read as one object. Blue at the head running to ember down
#  the haft, the same direction $VERSION "B" gives the armour (blue shoulders, bronze waist).
#
#  Written as ONE sprite, 256x256, pointing up-left the way RimWorld equipment art does.
#  Not stuffed, not quality-tinted: the ThingDef is standalone, so what is drawn here is
#  exactly what appears in the pawn's hands.
#
#  PowerShell traps this is written around, all previously paid for in this project:
#    - variable names are CASE-INSENSITIVE: $OUT and $out are ONE variable
#    - the comma operator binds TIGHTER than arithmetic, so every element of a numeric
#      array literal is parenthesised
#    - Set-Content double-encodes UTF-8; this file writes only PNG bytes, never text
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$DEST_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\Things\Item\Equipment"
$PREVIEW_DIR = "C:\Users\User\AppData\Local\Temp\claude\C--Games-Rimworld-RimWorld-RimWorldFolder-DovahkiinClaudePluged\8fd789e0-037c-4f64-847d-50fcce95451a\scratchpad"
$SIZE = 256
$SS   = 4                      # supersample factor
$N    = $SIZE * $SS

if (-not (Test-Path $DEST_DIR)) { New-Item -ItemType Directory -Path $DEST_DIR -Force | Out-Null }

# --- palette, lifted from the armour generator ---
$C_BLUE_HOT  = @(132,186,246)
$C_BLUE_LIT  = @( 58,124,216)
$C_BLUE_MID  = @( 36, 80,150)
$C_GOLD      = @(228,152, 44)
$C_MID       = @(168,104, 28)
$C_HOT       = @(255,206,120)
$C_EMBER     = @(240,118, 28)

function RGB($r,$g,$b,$a=255) {
  $rr=[int][Math]::Max(0,[Math]::Min(255,$r)); $gg=[int][Math]::Max(0,[Math]::Min(255,$g))
  $bb=[int][Math]::Max(0,[Math]::Min(255,$b)); $aa=[int][Math]::Max(0,[Math]::Min(255,$a))
  return [System.Drawing.Color]::FromArgb($aa,$rr,$gg,$bb)
}
function Lerp3($a, $b, [double]$t) {
  if ($t -lt 0) { $t = 0 }
  if ($t -gt 1) { $t = 1 }
  return @(
    ([int]($a[0] + ($b[0]-$a[0])*$t)),
    ([int]($a[1] + ($b[1]-$a[1])*$t)),
    ([int]($a[2] + ($b[2]-$a[2])*$t))
  )
}

$bmp = New-Object System.Drawing.Bitmap $N, $N, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear((RGB 0 0 0 0))
$g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

# AXIS: bottom-LEFT (butt) to top-RIGHT (head), matching Medieval Overhaul's convention.
#
# This was mirrored until measured. Counting opaque pixels per quadrant: their halberd is
# topLeft 105 / topRight 5583 / botLeft 3252 / botRight 105, and their greataxe the same shape -
# both run bottom-left to top-right with the head at TOP-RIGHT. Mine ran top-left to
# bottom-right, i.e. the other diagonal.
#
# That matters because we now borrow their halberd's Melee Animation tweak data verbatim, and
# OffX/OffY/Rotation/BladeStart/BladeEnd are all expressed in THEIR texture's frame. Copying
# those onto a mirrored sprite would have the pawn holding this by the blade.
$BUTT_X = 0.26; $BUTT_Y = 0.86
$HEAD_X = 0.70; $HEAD_Y = 0.20

function PT([double]$fx, [double]$fy) {
  return (New-Object System.Drawing.PointF ([single]($fx*$N)), ([single]($fy*$N)))
}

# ---------------------------------------------------------------------------------
# HAFT. Drawn as a tapering quad rather than a pen stroke so it can carry a gradient
# along its length - a pen would give one flat colour and the axe would not match the
# armour's ramp.
# ---------------------------------------------------------------------------------
$HAFT_W_BUTT = 0.024
$HAFT_W_HEAD = 0.036
$dx = $HEAD_X - $BUTT_X; $dy = $HEAD_Y - $BUTT_Y
$len = [Math]::Sqrt($dx*$dx + $dy*$dy)
$ux = $dx/$len; $uy = $dy/$len
$px = -$uy; $py = $ux            # unit normal

# DARK OUTLINE FIRST, under everything.
#
# This is what the art was missing. RimWorld weapon sprites are drawn with a dark keyline -
# Medieval Overhaul's greataxe has one - and without it a coloured shape on lit ground reads as
# soft and washed out however good the silhouette is. Comparing the two side by side, the
# outline was the whole difference, not the geometry.
#
# Drawn as a single wider quad along the haft, then the gradient bands paint over its middle
# and leave the margin showing as a keyline.
$C_LINE = @(14, 18, 28)
$OUTLINE = 0.010
$haftOutline = @(
  (PT ($BUTT_X + $px*($HAFT_W_BUTT+$OUTLINE) - $ux*$OUTLINE) ($BUTT_Y + $py*($HAFT_W_BUTT+$OUTLINE) - $uy*$OUTLINE)),
  (PT ($HEAD_X + $px*($HAFT_W_HEAD+$OUTLINE) + $ux*$OUTLINE) ($HEAD_Y + $py*($HAFT_W_HEAD+$OUTLINE) + $uy*$OUTLINE)),
  (PT ($HEAD_X - $px*($HAFT_W_HEAD+$OUTLINE) + $ux*$OUTLINE) ($HEAD_Y - $py*($HAFT_W_HEAD+$OUTLINE) + $uy*$OUTLINE)),
  (PT ($BUTT_X - $px*($HAFT_W_BUTT+$OUTLINE) - $ux*$OUTLINE) ($BUTT_Y - $py*($HAFT_W_BUTT+$OUTLINE) - $uy*$OUTLINE))
)
$brLine = New-Object System.Drawing.SolidBrush (RGB $C_LINE[0] $C_LINE[1] $C_LINE[2] 240)
$g.FillPolygon($brLine, [System.Drawing.PointF[]]$haftOutline)
$brLine.Dispose()

$BANDS = 26
for ($i = 0; $i -lt $BANDS; $i++) {
  $t0 = $i / [double]$BANDS
  $t1 = ($i+1) / [double]$BANDS
  # ember at the butt running to blue at the head - the armour's own direction
  $col = Lerp3 $C_EMBER $C_BLUE_LIT (($t0+$t1)*0.5)
  $w0 = $HAFT_W_BUTT + ($HAFT_W_HEAD-$HAFT_W_BUTT)*$t0
  $w1 = $HAFT_W_BUTT + ($HAFT_W_HEAD-$HAFT_W_BUTT)*$t1
  $ax = $BUTT_X + $dx*$t0; $ay = $BUTT_Y + $dy*$t0
  $bx = $BUTT_X + $dx*$t1; $by = $BUTT_Y + $dy*$t1
  $quad = @(
    (PT ($ax + $px*$w0) ($ay + $py*$w0)),
    (PT ($bx + $px*$w1) ($by + $py*$w1)),
    (PT ($bx - $px*$w1) ($by - $py*$w1)),
    (PT ($ax - $px*$w0) ($ay - $py*$w0))
  )
  $br = New-Object System.Drawing.SolidBrush (RGB $col[0] $col[1] $col[2] 232)
  $g.FillPolygon($br, [System.Drawing.PointF[]]$quad)
  $br.Dispose()
}

# ---------------------------------------------------------------------------------
# HEAD. Two mirrored bits - a greataxe, not a hatchet. Angular via AddPolygon: the
# armour's chest crest taught that even a slight curve tension turns a facet into a
# petal, and the read here is "forged", not "organic".
# ---------------------------------------------------------------------------------
# ONE dominant blade plus a small counterweight spike, NOT two mirrored bits.
#
# Two mirrored bits were tried first and read as a pair of blobs stuck either side of a
# stick - at 48px, which is the size that actually matters in play, a double-bit head is
# two small shapes competing instead of one silhouette. A single large blade with a spike
# opposite is the classic greataxe read and survives being shrunk.
#
# $o runs OUT from the haft, $up runs ALONG it. The blade must be TALLER than it is wide:
# the long cutting edge parallel to the haft is what says "axe". Outward reach alone says
# nothing, which is what the first two attempts got wrong.
function DrawBit([double]$side, [double]$o, [double]$up, $raw, [int]$edgeAlpha, [double]$penPx) {
  $cx = $HEAD_X + $ux*0.055
  $cy = $HEAD_Y + $uy*0.055
  $oo = $o * $side

  $pts = @()
  foreach ($q in $raw) {
    $ro = $q[0]; $ra = $q[1]
    $pts += (PT ($cx + $px*$oo*$ro + $ux*$up*$ra) ($cy + $py*$oo*$ro + $uy*$up*$ra))
  }
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddPolygon([System.Drawing.PointF[]]$pts)

  # Dark keyline UNDER the fill - see the note by the haft outline. Stroked before filling, so
  # the fill covers the inner half and the outer half survives as the outline.
  $penLine = New-Object System.Drawing.Pen (RGB 14 18 28 245), ([single](6.0*$SS))
  $penLine.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $penLine.MiterLimit = [single]6.0
  $g.DrawPath($penLine, $path); $penLine.Dispose()

  $rect = $path.GetBounds()
  if ($rect.Width -lt 1) { $rect.Width = 1 }
  if ($rect.Height -lt 1) { $rect.Height = 1 }
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, `
    (RGB $C_BLUE_MID[0] $C_BLUE_MID[1] $C_BLUE_MID[2] 226), `
    (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] 242), ([single]45.0)
  $g.FillPath($brush, $path); $brush.Dispose()

  # hot edge, MITRED - a round join blunts exactly the corners doing the work. Kept thin:
  # a fat outline on a small shape swallows the facets and turns the blade back into a blob.
  $pen = New-Object System.Drawing.Pen (RGB $C_BLUE_HOT[0] $C_BLUE_HOT[1] $C_BLUE_HOT[2] $edgeAlpha), ([single]($penPx*$SS))
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  $g.DrawPath($pen, $path); $pen.Dispose()
  $path.Dispose()
}

# A BEARDED GREATAXE HEAD, not a wedge.
#
# Two rules carried over from the shoulder fins and the chest crest, both learned the hard way:
#
#  1. THE ALONG-HAFT EXTENT MUST GROW MONOTONICALLY with distance from the haft - 0.42 at the
#     socket, 0.78 mid, 1.00 at the edge. Put the tallest points at mid-span instead and the
#     shape is a lozenge, which with an outline renders as a hexagon. That happened twice.
#
#  2. THE BEARD NEEDS A CONCAVE NOTCH. Points 6 and 7 hook the lower horn down and then pull
#     BACK toward the haft. Without that notch the bottom of the blade is just a longer
#     triangle - it is the concavity that says "axe" rather than "spearhead", exactly as the
#     shoulder fins only read as fins once their trailing edge scooped inward.
$BLADE = @(
  @( (0.10),  (0.42) ),
  @( (0.50),  (0.78) ),
  @( (0.92),  (1.00) ),
  @( (1.10),  (0.25) ),
  @( (1.02), (-0.55) ),
  @( (0.78), (-1.05) ),
  @( (0.42), (-0.70) ),
  @( (0.12), (-0.40) )
)
# The counterweight spike opposite, small: it balances the silhouette and any detail on it is
# lost by 48px, which is the size that actually matters in play.
$SPIKE = @(
  @( (0.10),  (0.34) ),
  @( (0.95),  (0.55) ),
  @( (1.16),  (0.00) ),
  @( (0.95), (-0.55) ),
  @( (0.10), (-0.34) )
)
DrawBit  1.0 0.205 0.165 $BLADE 250 2.2
DrawBit -1.0 0.085 0.062 $SPIKE 235 1.8

# --- a warm collar where head meets haft, so the two colours meet at something hot ---
$collar = New-Object System.Drawing.Pen (RGB $C_HOT[0] $C_HOT[1] $C_HOT[2] 220), ([single](5.0*$SS))
$collar.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$collar.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($collar, (PT ($HEAD_X + $ux*0.085) ($HEAD_Y + $uy*0.085)), `
                     (PT ($HEAD_X + $ux*0.140) ($HEAD_Y + $uy*0.140)))
$collar.Dispose()

# --- butt cap ---
$capPen = New-Object System.Drawing.Pen (RGB $C_GOLD[0] $C_GOLD[1] $C_GOLD[2] 235), ([single](6.0*$SS))
$capPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$capPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($capPen, (PT ($BUTT_X - $ux*0.012) ($BUTT_Y - $uy*0.012)), `
                     (PT ($BUTT_X + $ux*0.030) ($BUTT_Y + $uy*0.030)))
$capPen.Dispose()

$g.Dispose()

# downsample to 256
$final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gf = [System.Drawing.Graphics]::FromImage($final)
$gf.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gf.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$gf.Clear((RGB 0 0 0 0))
$gf.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0,0,$SIZE,$SIZE))
$gf.Dispose(); $bmp.Dispose()

$outPath = Join-Path $DEST_DIR "DovahkiinAncientAxe.png"
$final.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output ("wrote " + $outPath)

# --- preview, over lit ground, at item scale and in-hand scale ---
$sheet = New-Object System.Drawing.Bitmap 620, 330, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gs = [System.Drawing.Graphics]::FromImage($sheet)
$gs.Clear((RGB 38 40 36 255))
$gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontB = New-Object System.Drawing.Font "Segoe UI", 14, ([System.Drawing.FontStyle]::Bold)
$fontS = New-Object System.Drawing.Font "Segoe UI", 10
$brW = New-Object System.Drawing.SolidBrush (RGB 235 235 230 255)
$brG = New-Object System.Drawing.SolidBrush (RGB 165 168 160 255)
$gs.DrawString("Spectral greataxe", $fontB, $brW, [single]18, [single]12)

$tile = 12
foreach ($cell in @(@(18,46,256,"full size 256px"), @(300,46,96,"as it appears in-hand, 96px"), @(430,46,48,"colony zoom, 48px"))) {
  $cx = $cell[0]; $cy = $cell[1]; $sz = $cell[2]
  for ($gy=0; $gy -lt $sz; $gy+=$tile) {
    for ($gx=0; $gx -lt $sz; $gx+=$tile) {
      $hv = [Math]::Sin(($gx+1)*12.9898 + ($gy+1)*78.233 + 7*37.719)*43758.5453
      $hv = $hv - [Math]::Floor($hv)
      $dv = [int](($hv-0.5)*42.0)
      $bru = New-Object System.Drawing.SolidBrush (RGB (122+$dv) (106+$dv) (84+[int]($dv*0.8)) 255)
      $gs.FillRectangle($bru, ($cx+$gx), ($cy+$gy), $tile, $tile)
      $bru.Dispose()
    }
  }
  $gs.DrawImage($final, (New-Object System.Drawing.Rectangle $cx, $cy, $sz, $sz))
  $gs.DrawString($cell[3], $fontS, $brG, [single]$cx, [single]($cy+$sz+4))
}
$gs.Dispose()
$prev = Join-Path $PREVIEW_DIR "ancient_axe.png"
$sheet.Save($prev, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose(); $final.Dispose()
Write-Output ("wrote preview " + $prev)
Write-Output "DONE"
