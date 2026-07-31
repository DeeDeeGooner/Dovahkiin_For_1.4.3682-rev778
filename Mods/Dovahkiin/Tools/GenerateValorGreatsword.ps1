# =====================================================================================
#  CALL OF VALOR - Hun Kaal Zoor. The hero's SPECTRAL greatsword.
#
#  Built from scratch 2026-07-30 after the user rejected the previous attempts with the
#  decisive note: "you still draw it in a way that behaves like a material weapon, lighting
#  effect white bands, shading, etc". That was exactly right and it is the whole brief.
#
#  ---------------------------------------------------------------------------------
#  A GHOST IS EMISSIVE, NOT LIT. THIS IS THE POINT OF THE WHOLE FILE.
#  ---------------------------------------------------------------------------------
#  Every earlier version rendered a solid object being lit from somewhere: a hard two-tone
#  bevel down the blade, a specular on the pommel, a dark keyline, shaded faces. All of that
#  says "this is a metal thing catching light", which is precisely what a spirit weapon is
#  not. Deleted, not adjusted - it was the wrong model, not the wrong numbers.
#
#  What replaces it:
#    - the body is TRANSLUCENT. You can see the ground through it. Nothing else says ghost
#      as directly, and it is the one thing none of the earlier attempts did.
#    - the EDGES glow, rather than faces being lit. A rim of near-white light round the
#      silhouette, brightest where the shape is thinnest.
#    - an outer BLOOM, several wide low-alpha passes, so it bleeds into the air around it.
#    - the engraving GLOWS instead of being a dark inlay. On the reference the knotwork is
#      cut dark into steel; on a spirit blade it is the light showing through.
#    - NO specular, NO bevel split, NO cast shading, NO dark keyline.
#
#  The dark keyline is the notable casualty. SPEC-adjacent lore in this project says a
#  keyline is not optional because a coloured shape on lit ground reads washed out - and that
#  is true FOR A SOLID OBJECT. A ghost is allowed to be soft; what it is not allowed to be is
#  illegible, so variant B keeps a faint dark separation and the sheet shows both at 48px.
#
#  ---------------------------------------------------------------------------------
#  SILHOUETTE: traced from the user's reference, a Nordic-carved greatsword
#  ---------------------------------------------------------------------------------
#  Its distinctive features, in order up the weapon:
#    squared pommel, a wrapped lower grip, a HORNED lower guard, a second wrapped section,
#    a larger HORNED upper guard, then a blade that STEPS rather than tapering smoothly -
#    a broad flared base, a step in, a long shaft, a second flare near the tip, then the
#    point. The steps and the upswept horns are what make it read as ancient rather than as
#    a generic sword; a smooth taper is what made the last attempt look like anybody's sword.
#
#  ---------------------------------------------------------------------------------
#  BEHAVIOUR - unchanged, and the reason this is its own weapon and not a tint of theirs
#  ---------------------------------------------------------------------------------
#  Our own standalone ThingDef, borrowing only DankPyon_MeleeWeapon_Greatsword's behaviour:
#    drawSize (1.25,1.25) - the Melee Animation tweak's ScaleX/ScaleY are 1.25 and MUST match
#    Mass 3, handle Poke 9 at 2s, edge Cut 31.25 at 3.15s with 0.25 AP
#    tweak: OffX 0.5461391, OffY -0.0112994611, Rotation 45.0,
#           BladeStart 0.305084735, BladeEnd 1.382298, MeleeWeaponType 6 (two-handed sword)
#    hold: no equippedAngleOffset - they use VFECore weaponDraftedDrawOffsets,
#          south/east -45, north 115, west 45. Authored angles, the right starting point.
#  WRITING IT STANDALONE DROPS CompEquippable, which lives on vanilla's BaseWeapon. Without
#  it RimWorld logs "is equipment but has no CompEquippable" and the summon fails outright -
#  that is what killed the Ancient Dragonborn's first playtest. Add it by hand.
#
#  ORIENTATION: bottom-left to top-right, tip at TOP-RIGHT, as all three of their two-handers
#  are. Tweak values live in their texture's frame, so a mirrored sprite is gripped by the blade.
#
#  PowerShell traps honoured: no single-letter variables, every numeric array element
#  parenthesised, [double] on Math::Max, no C-style casts.
# =====================================================================================
Add-Type -AssemblyName System.Drawing

$WRITE_TEXTURE = $false

$DEST_DIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\Things\Item\Equipment"
$OUT_DIR = $env:DOVAH_PREVIEW
if (-not $OUT_DIR) { $OUT_DIR = $PSScriptRoot }

$SIZE   = 256
$SS     = 4
$CANVAS = $SIZE * $SS

# --- spectral palette. Light, not pigment. -----------------------------------------
$C_RIM    = @(255, 255, 255)   # the luminous edge
$C_BODY   = @(196, 232, 255)   # the translucent interior, bright end
$C_BODY_D = @(120, 168, 216)   # the translucent interior, dim end
$C_BLOOM  = @(168, 216, 255)   # the halo bled into the air
$C_DARK   = @( 26,  40,  62)   # ONLY used by variant B, as a faint separation

$BODY_ALPHA  = 104             # how solid the interior is. Lower = ghostlier.
$RIM_ALPHA   = 236
$GLYPH_ALPHA = 150

# --- geometry: the full diagonal ---------------------------------------------------
$BUTT_X = 0.060; $BUTT_Y = 0.952
$TIP_X  = 0.952; $TIP_Y  = 0.060

# THE PROFILE, traced from the reference. (along, half-width), both fractions of length.
# A STEPPED blade, not a smooth taper - repeated points at the same 'along' are the steps.
$PROFILE = @(
  @( (0.000), (0.0230) ),   # pommel cap
  @( (0.035), (0.0270) ),
  @( (0.045), (0.0175) ),   # step in to the lower grip
  @( (0.150), (0.0175) ),
  @( (0.160), (0.0620) ),   # LOWER GUARD, horns spread
  @( (0.196), (0.0620) ),
  @( (0.206), (0.0180) ),   # step back in to the middle grip
  @( (0.300), (0.0180) ),
  @( (0.312), (0.0790) ),   # UPPER GUARD, wider
  @( (0.352), (0.0790) ),
  @( (0.364), (0.0300) ),   # blade root
  @( (0.396), (0.0560) ),   # flare out - the broad base
  @( (0.470), (0.0520) ),
  @( (0.492), (0.0380) ),   # step in
  @( (0.760), (0.0350) ),   # the long shaft, barely tapering
  @( (0.792), (0.0470) ),   # second flare, near the tip
  @( (0.842), (0.0420) ),
  @( (0.868), (0.0330) )    # THE YOKOTE - where the symmetric blade ends and the tip begins
)

# ------------------------------------------------------------------------------------
# THE TIP IS ASYMMETRIC - a katana's kissaki, or a glaive's head.
#
# $PROFILE cannot express this: it stores ONE half-width per station and mirrors it, which
# can only ever produce a symmetric point. So the blade is symmetric up to the yokote and
# the tip is built explicitly, one side at a time.
#
# Which side is which: the sprite runs bottom-left to top-right, so -perp is the upper-left
# face. On a blade pointing up-right that upper-left line is the SPINE, and +perp is the
# EDGE. The point therefore sits offset toward -perp, with the edge sweeping up to meet the
# spine - which is what makes it read as a single-edged blade rather than a chisel.
#
# Two shapes offered because they are genuinely different weapons:
#   KATANA - a short kissaki, a restrained slant, the point close to the spine line
#   GLAIVE - a longer, broader forward sweep with more belly to the edge
# ------------------------------------------------------------------------------------
$TIP_KATANA = @{
  Edge  = @( @((0.900), ( 0.0268)), @((0.949), ( 0.0170)), @((0.985), ( 0.0062)) )
  Point =    @((1.000), (-0.0070))
  Spine = @( @((0.962), (-0.0224)), @((0.906), (-0.0300)) )
}
$TIP_GLAIVE = @{
  Edge  = @( @((0.898), ( 0.0400)), @((0.938), ( 0.0396)), @((0.975), ( 0.0250)) )
  Point =    @((1.000), (-0.0090))
  Spine = @( @((0.958), (-0.0230)), @((0.902), (-0.0306)) )
}

# The upswept HORNS on both guards - drawn separately, because they sweep back toward the
# blade and a symmetric profile cannot express that.
$HORNS = @(
  @( (0.160), (0.0620), (0.055), (1.9) ),   # along, from, length, sweep
  @( (0.312), (0.0790), (0.068), (2.1) )
)

function RGB($red, $green, $blue, $alpha = 255) {
  $rr = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$red))
  $gg = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$green))
  $bb = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$blue))
  $aa = [int][Math]::Max([double]0.0, [Math]::Min([double]255.0, [double]$alpha))
  return [System.Drawing.Color]::FromArgb($aa, $rr, $gg, $bb)
}
function Lerp3($from, $to, [double]$amount) {
  if ($amount -lt 0) { $amount = 0 }
  if ($amount -gt 1) { $amount = 1 }
  return @(
    ([int]($from[0] + ($to[0]-$from[0])*$amount)),
    ([int]($from[1] + ($to[1]-$from[1])*$amount)),
    ([int]($from[2] + ($to[2]-$from[2])*$amount))
  )
}

# $colder: swap the palette toward a saturated spirit-blue instead of near-neutral white.
# $bodyAlpha: how solid the interior is. Lower is ghostlier.
#
# A DARK SEPARATION WAS TRIED HERE AND REMOVED. The idea was a faint dark stroke outside the
# rim to hold the shape against lit ground, the way a keyline does for a solid weapon.
# Measured, it did nothing: drawn after the bloom and under the rim, only about a pixel of it
# survived, and the interior and just-outside alphas came back 170/140 against 170/143 for the
# version without it. Two ways to read that - it needed to be far heavier, or a ghost should
# not have a dark outline at all. The second is right, and the cool bloom already separates
# the weapon from warm ground by HUE, which is the spectral way to stay legible.
function BuildSword([bool]$colder, [int]$bodyAlpha, $tipSpec) {
  $bmp = New-Object System.Drawing.Bitmap $CANVAS, $CANVAS, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfx = [System.Drawing.Graphics]::FromImage($bmp)
  $gfx.Clear((RGB 0 0 0 0))
  $gfx.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

  $spanX = $TIP_X - $BUTT_X
  $spanY = $TIP_Y - $BUTT_Y
  $spanLen = [Math]::Sqrt(($spanX*$spanX) + ($spanY*$spanY))
  $uxx = $spanX/$spanLen; $uyy = $spanY/$spanLen
  $nxx = -$uyy;           $nyy = $uxx

  # the colder palette: a saturated spirit-blue rather than near-neutral white
  $rimCol   = if ($colder) { @(226, 246, 255) } else { $C_RIM }
  $bodyHi   = if ($colder) { @(140, 210, 255) } else { $C_BODY }
  $bodyLo   = if ($colder) { @( 64, 132, 206) } else { $C_BODY_D }
  $bloomCol = if ($colder) { @( 96, 178, 255) } else { $C_BLOOM }

  function WPT([double]$along, [double]$perp) {
    $fx = $BUTT_X + ($uxx * $along * $spanLen) + ($nxx * $perp * $spanLen)
    $fy = $BUTT_Y + ($uyy * $along * $spanLen) + ($nyy * $perp * $spanLen)
    return (New-Object System.Drawing.PointF ([single]($fx*$CANVAS)), ([single]($fy*$CANVAS)))
  }

  # One closed outline: up the EDGE side, round the asymmetric tip, back down the SPINE side.
  # The tip is inserted between the two passes rather than mirrored, which is the whole
  # reason it can be a kissaki instead of a symmetric point.
  $outline = @()
  foreach ($node in $PROFILE) { $outline += (WPT $node[0] $node[1]) }
  foreach ($node in $tipSpec.Edge)  { $outline += (WPT $node[0] $node[1]) }
  $outline += (WPT $tipSpec.Point[0] $tipSpec.Point[1])
  foreach ($node in $tipSpec.Spine) { $outline += (WPT $node[0] $node[1]) }
  for ($idx = $PROFILE.Count - 1; $idx -ge 0; $idx--) {
    $outline += (WPT $PROFILE[$idx][0] (-1.0 * $PROFILE[$idx][1]))
  }
  $body = New-Object System.Drawing.Drawing2D.GraphicsPath
  $body.AddPolygon([System.Drawing.PointF[]]$outline)

  # the horns, as separate paths so their sweep can be expressed
  $hornPaths = @()
  foreach ($spec in $HORNS) {
    foreach ($side in @((1.0), (-1.0))) {
      $atAlong = $spec[0]; $fromPerp = $spec[1]; $len = $spec[2]; $sweep = $spec[3]
      $hp = New-Object System.Drawing.Drawing2D.GraphicsPath
      $hp.AddPolygon([System.Drawing.PointF[]]@(
        (WPT ($atAlong - 0.012) ($side * $fromPerp * 0.80)),
        (WPT ($atAlong + ($len * 0.30)) ($side * ($fromPerp + ($len * 0.55)))),
        (WPT ($atAlong + ($len * $sweep * 0.42)) ($side * ($fromPerp + ($len * 0.30)))),
        (WPT ($atAlong + 0.014) ($side * $fromPerp * 0.72))
      ))
      $hornPaths += $hp
    }
  }

  $allPaths = @($body) + $hornPaths

  # ---- 1. OUTER BLOOM. Widest and faintest first. This is the weapon bleeding into the
  # air, and it is most of what sells "not solid". ----
  foreach ($spec in @(@((30.0), (16)), @((20.0), (24)), @((11.0), (34)))) {
    foreach ($pathRef in $allPaths) {
      $penGlow = New-Object System.Drawing.Pen (RGB $bloomCol[0] $bloomCol[1] $bloomCol[2] $spec[1]), ([single]($spec[0]*$SS))
      $penGlow.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
      $penGlow.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
      $penGlow.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
      $gfx.DrawPath($penGlow, $pathRef); $penGlow.Dispose()
    }
  }

  # ---- 2. optional faint DARK separation, variant B only. Sits outside the rim, so it
  # separates the weapon from lit ground without implying a lit surface. ----

  # ---- 3. TRANSLUCENT BODY, banded along the length so it carries the gradient. Drawn as
  # clipped bands over the whole silhouette: the ground shows through. ----
  $oldClip = $gfx.Clip
  $region = New-Object System.Drawing.Region $body
  foreach ($hp in $hornPaths) { $region.Union($hp) }
  $gfx.Clip = $region
  $BANDS = 64
  for ($band = 0; $band -lt $BANDS; $band++) {
    $tA = $band / [double]$BANDS
    $tB = ($band + 1) / [double]$BANDS
    $col = Lerp3 $bodyLo $bodyHi (($tA + $tB) * 0.5)
    $quad = @(
      (WPT $tA ( 0.30)), (WPT $tB ( 0.30)), (WPT $tB (-0.30)), (WPT $tA (-0.30))
    )
    $brush = New-Object System.Drawing.SolidBrush (RGB $col[0] $col[1] $col[2] $bodyAlpha)
    $gfx.FillPolygon($brush, [System.Drawing.PointF[]]$quad)
    $brush.Dispose()
  }

  # ---- 4. THE GLOWING ENGRAVING, inside the clip so it cannot escape the blade. On the
  # reference this knotwork is cut DARK into steel; on a spirit blade it is the light
  # showing through, so it is drawn bright. Sparse on purpose - it is gone by 48px and
  # dense detail there reads as noise. ----
  $penGlyph = New-Object System.Drawing.Pen (RGB 255 255 255 $GLYPH_ALPHA), ([single](2.2*$SS))
  $penGlyph.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
  foreach ($at in @(0.430, 0.545, 0.660, 0.812)) {
    # a small angular meander: out, along, back in - the Nordic step motif
    $gfx.DrawLines($penGlyph, [System.Drawing.PointF[]]@(
      (WPT ($at - 0.026) ( 0.0110)),
      (WPT ($at - 0.026) ( 0.0235)),
      (WPT ($at + 0.020) ( 0.0235)),
      (WPT ($at + 0.020) (-0.0235)),
      (WPT ($at - 0.026) (-0.0235)),
      (WPT ($at - 0.026) (-0.0110))
    ))
  }
  # a centreline of light running the blade's length
  $penSpine = New-Object System.Drawing.Pen (RGB 255 255 255 96), ([single](2.6*$SS))
  $penSpine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $penSpine.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $gfx.DrawLine($penSpine, (WPT 0.380 0.0), (WPT 0.862 0.0))
  $penSpine.Dispose()
  $penGlyph.Dispose()
  $gfx.Clip = $oldClip
  $region.Dispose()

  # ---- 5. THE LUMINOUS RIM, last so nothing covers it. This is the edge of the apparition,
  # and it replaces both the keyline and the specular. ----
  foreach ($pathRef in $allPaths) {
    $penRim = New-Object System.Drawing.Pen (RGB $rimCol[0] $rimCol[1] $rimCol[2] $RIM_ALPHA), ([single](2.6*$SS))
    $penRim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $gfx.DrawPath($penRim, $pathRef); $penRim.Dispose()
  }

  foreach ($hp in $hornPaths) { $hp.Dispose() }
  $body.Dispose()
  $gfx.Dispose()

  $final = New-Object System.Drawing.Bitmap $SIZE, $SIZE, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gfin = [System.Drawing.Graphics]::FromImage($final)
  $gfin.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $gfin.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $gfin.Clear((RGB 0 0 0 0))
  $gfin.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $SIZE, $SIZE))
  $gfin.Dispose(); $bmp.Dispose()
  return $final
}

function QuadOf($bitmap) {
  $data = $bitmap.LockBits((New-Object System.Drawing.Rectangle 0,0,$bitmap.Width,$bitmap.Height),
          [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object 'byte[]' ($data.Stride * $bitmap.Height)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $stride = $data.Stride
  $bitmap.UnlockBits($data)
  $topLeft=0;$topRight=0;$botLeft=0;$botRight=0
  $half = [int]($bitmap.Width/2)
  for ($yy=0; $yy -lt $bitmap.Height; $yy++) {
    for ($xx=0; $xx -lt $bitmap.Width; $xx++) {
      if ($bytes[$yy*$stride + $xx*4 + 3] -gt 8) {
        if ($yy -lt $half) { if ($xx -lt $half) {$topLeft++} else {$topRight++} }
        else { if ($xx -lt $half) {$botLeft++} else {$botRight++} }
      }
    }
  }
  return @($topLeft,$topRight,$botLeft,$botRight)
}

$built = @()
$built += ,@((BuildSword $false $BODY_ALPHA $TIP_KATANA), "A. katana tip - short kissaki")
$built += ,@((BuildSword $false $BODY_ALPHA $TIP_GLAIVE), "B. glaive tip - broader forward sweep")
$built += ,@((BuildSword $true  $BODY_ALPHA $TIP_KATANA), "C. katana tip, spirit-blue")

$CELLW = 300
$PADX = 24
$sheetW = ($PADX * 4) + ($CELLW * 3)
$sheetH = 500
$sheet = New-Object System.Drawing.Bitmap $sheetW, $sheetH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gsheet = [System.Drawing.Graphics]::FromImage($sheet)
$gsheet.Clear((RGB 28 30 28 255))
$gsheet.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fontBig = New-Object System.Drawing.Font "Segoe UI", 15, ([System.Drawing.FontStyle]::Bold)
$fontMid = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Bold)
$fontSml = New-Object System.Drawing.Font "Segoe UI", 9.5
$brWhite = New-Object System.Drawing.SolidBrush (RGB 238 238 232 255)
$brGrey  = New-Object System.Drawing.SolidBrush (RGB 168 172 164 255)
$brGold  = New-Object System.Drawing.SolidBrush (RGB 226 178 92 255)
$gsheet.DrawString("CALL OF VALOR - built from scratch as a SPECTRE, not a lit object", $fontBig, $brWhite, [single]$PADX, [single]12)
$gsheet.DrawString("translucent body you can see the ground through, glowing edges and engraving, outer bloom. No bevel, no specular, no keyline.", $fontSml, $brGrey, [single]$PADX, [single]36)

function Ground($gfxRef, [double]$px, [double]$py, [double]$size, [int]$salt) {
  $tile = 12
  for ($gy = 0; $gy -lt $size; $gy += $tile) {
    for ($gx = 0; $gx -lt $size; $gx += $tile) {
      $hashv = [Math]::Sin((($gx+1)*12.9898) + (($gy+1)*78.233) + ($salt*37.719)) * 43758.5453
      $hashv = $hashv - [Math]::Floor($hashv)
      $delta = [int](($hashv - 0.5) * 42.0)
      $brush = New-Object System.Drawing.SolidBrush (RGB (122+$delta) (106+$delta) (84+[int]($delta*0.8)) 255)
      $gfxRef.FillRectangle($brush, [single]($px+$gx), [single]($py+$gy), [single]$tile, [single]$tile)
      $brush.Dispose()
    }
  }
}

$column = 0
$report = @()
foreach ($entry in $built) {
  $img = $entry[0]; $label = $entry[1]
  $px = $PADX + ($column * ($CELLW + $PADX))
  $py = 58
  Ground $gsheet $px $py $CELLW (11 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$px), ([int]$py), $CELLW, $CELLW))
  $brLabel = if ($column -eq 0) { $brGold } else { $brGrey }
  $gsheet.DrawString($label, $fontMid, $brLabel, [single]$px, [single]($py + $CELLW + 6))
  Ground $gsheet $px ($py + $CELLW + 30) 96 (40 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]$px), ([int]($py + $CELLW + 30)), 96, 96))
  Ground $gsheet ($px + 104) ($py + $CELLW + 30) 48 (50 + $column)
  $gsheet.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]($px + 104)), ([int]($py + $CELLW + 30)), 48, 48))
  $gsheet.DrawString("96px (in hand)      48px", $fontSml, $brGrey, [single]$px, [single]($py + $CELLW + 130))
  $quad = QuadOf $img
  $report += ("{0,-42} TL {1,5} TR {2,5} BL {3,5} BR {4,5}   tip-top-right: {5}" -f `
    $label, $quad[0], $quad[1], $quad[2], $quad[3],
    $(if ($quad[1] -gt $quad[0] -and $quad[1] -gt $quad[3]) { "YES" } else { "*** NO ***" }))
  $column++
}
$gsheet.Dispose()

$previewPath = Join-Path $OUT_DIR "valor_greatsword.png"
$sheet.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

for ($idx = 0; $idx -lt $built.Count; $idx++) {
  $cand = Join-Path $OUT_DIR ("valor_gs_" + ([char](65+$idx)) + ".png")
  $built[$idx][0].Save($cand, [System.Drawing.Imaging.ImageFormat]::Png)
}
if ($WRITE_TEXTURE) {
  $dest = Join-Path $DEST_DIR "DovahkiinValorGreatsword.png"
  $built[0][0].Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("WROTE SHIPPING TEXTURE " + $dest)
} else {
  Write-Output "preview only - nothing written into the mod (`$WRITE_TEXTURE is false)"
}
foreach ($entry in $built) { $entry[0].Dispose() }
foreach ($line in $report) { Write-Output $line }
Write-Output ("wrote " + $previewPath)
Write-Output "DONE"


