# Generates all 15 shout button icons from the single B3 master.
#
# THREE LEVERS distinguish one shout from another, as the user set out:
#   1. body colour   - the comet itself
#   2. core colour   - the bright circle in the middle of the head, tintable SEPARATELY
#   3. opacity       - for the shouts that should read as faint or translucent
#
# The core being independently tintable is what makes Slow Time work: a pale grey-white comet
# with a BLUE core. Before this the core always blew out to white.
#
# Re-runnable: same master + same table always yields the same 15 files. To retune a shout,
# edit one row here and re-run - nothing else in the mod changes.
Add-Type -AssemblyName System.Drawing

$MASTER = "C:\Users\User\AppData\Local\Temp\claude\C--Games-Rimworld-RimWorld-RimWorldFolder-DovahkiinClaudePluged\fa5ecdd8-cfc2-4e0c-b98b-1560cbcd8092\scratchpad\fireball3_master.png"
$OUTDIR = "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Textures\UI\Abilities"

if (-not (Test-Path $OUTDIR)) { New-Item -ItemType Directory -Force -Path $OUTDIR | Out-Null }

# name, bodyR,bodyG,bodyB, coreR,coreG,coreB, opacity
$TABLE = @(
  @("UnrelentingForce",  95,165,240,  255,255,255, 1.00),  # deeper blue than Frost, per playtest
  @("FireBreath",       255,140, 40,  255,255,255, 1.00),
  @("FrostBreath",      150,215,255,  255,255,255, 1.00),
  @("WhirlwindSprint",  225,228,235,  255,255,255, 1.00),
  # Tip stays the cold blue-grey it already was; the head becomes blue lilac (see $HEAD_GRADIENT).
  @("MarkedForDeath",   150,160,175,  255,255,255, 1.00),
  @("ClearSkies",       170,215,245,  255,255,255, 1.00),
  # Slow Time: Unrelenting Force's OLD pale grey-white body, with a BLUE core.
  @("SlowTime",         235,235,240,   70,145,235, 1.00),
  @("BecomeEthereal",   200,225,245,  255,255,255, 0.72),  # translucent by design
  @("DrainVitality",    150, 50,200,  255,255,255, 1.00),
  @("Dismay",           220, 40, 40,  255,255,255, 1.00),
  @("Cyclone",          185,190,200,  255,255,255, 0.60),  # faintest of the family
  # Not built yet - icons generated anyway, they cost nothing and will be waiting.
  # Tip is THUNDER BLUE - the bright flash, with storm-cloud grey at the head.
  @("StormCall",        105,180,255,  255,255,255, 1.00),
  # Tip colour: BRIGHT CLEAR PURPLE, not the old crimson. With the dark-purple head below this
  # makes the whole comet one hue running dark to bright, which also matches the shout's purple
  # bolt in play. Kept distinctly lighter than Drain Vitality's (150,50,200) so the two purple
  # shouts still read apart on the command bar.
  @("SoulTear",         178,102,250,  255,255,255, 1.00),
  # Tip is Fire Breath's EXACT orange, head is Unrelenting Force's EXACT blue - the shout is
  # both at once, so it borrows both rather than inventing a third colour.
  @("DragonAspect",     255,140, 40,  255,255,255, 1.00),
  # Tip is clear light azure, head deep azure. Core lightened to match, since a grey core in an
  # all-azure comet read as a smudge rather than a highlight.
  @("Dragonrend",       135,205,250,  235,248,255, 1.00),
  # CALL OF VALOR. The user's spec: bright white at the head running down to grey at the tip,
  # with the head's core circle in the summon's own blue.
  #
  # The core is C_AZURE (120,196,255) from Tools/ValorPalette.ps1 - the SAME file both his armour
  # and his greatsword read, so the icon quotes the hero rather than a blue that merely looks
  # like his. That palette exists precisely because the sword and the armour once carried two
  # near-identical copies of the same colours and drifted apart; a third copy here would restart
  # the problem.
  #
  # The tip grey is faintly COOL (150,155,165) rather than neutral. A pure grey tip on a
  # white-to-grey comet reads as an uncoloured placeholder; a cool cast ties it to his spectral
  # palette and, at full opacity, keeps it clear of Cyclone's (185,190,200) at 0.60 - the only
  # other grey in the family.
  @("CallOfValor",      150,155,165,  120,196,255, 1.00)
)

# ---------------------------------------------------------------------------------------------
# HEAD-TO-TIP GRADIENT (optional, per shout)
# ---------------------------------------------------------------------------------------------
# name -> head colour. Where present, the body colour is blended ALONG THE COMET: this colour at
# the head, the table's body colour by the tip. The blend curve is deliberately the same one the
# Thu'um bar uses - smoothstepped across the middle 40% - so each colour still owns roughly half
# the shape rather than smearing into mud through the centre. That is what "50/50 blend" means
# here, and it is why the numbers 0.30/0.70 appear in both places.
#
# "Along the comet" is measured as distance from the HEAD, and the head is found automatically as
# the centroid of the brightest pixels - the hot core the master already draws there. No
# hard-coded coordinates, so it survives the master being redrawn.
$HEAD_GRADIENT = @{
  # deep dark purple -> bright clear purple
  "SoulTear"       = @( 46,  10,  78)
  # blue lilac -> the cold blue-grey it already was
  "MarkedForDeath" = @(140, 150, 225)
  # Unrelenting Force's EXACT blue (95,165,240) -> Fire Breath's EXACT orange (255,140,40).
  # Dragon Aspect is both shouts at once, so it borrows their colours rather than inventing one.
  "DragonAspect"   = @( 95, 165, 240)
  # storm-cloud dark grey -> thunder blue
  "StormCall"      = @( 52,  56,  64)
  # deep azure -> clear light azure
  "Dragonrend"     = @( 18,  68, 148)
  # BRIGHT WHITE head -> grey tip. Pure white, not off-white: he is the only shout in the family
  # whose head is meant to read as light itself rather than as a colour, which is what separates
  # a hero of Sovngarde from the rest of the bar at a glance.
  "CallOfValor"    = @(255, 255, 255)
}

$src = [System.Drawing.Bitmap]::FromFile($MASTER)
$W = $src.Width; $H = $src.Height
$rect = New-Object System.Drawing.Rectangle -ArgumentList ([int]0),([int]0),([int]$W),([int]$H)
$sd = $src.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$n = $sd.Stride * $H
$sBuf = New-Object byte[] $n
[System.Runtime.InteropServices.Marshal]::Copy($sd.Scan0, $sBuf, 0, $n)
$src.UnlockBits($sd)
$src.Dispose()

$RIM_MAX  = 0.22   # below this luminance a pixel is the dark rim
$CORE_MIN = 0.86   # above this it is the hot core

# --- locate the comet's HEAD, for the head-to-tip gradient ------------------------------------
# The head is where the master's hot core sits, so it is simply the centroid of the brightest
# pixels. Found rather than hard-coded, so redrawing the master cannot silently misplace it.
[double]$hx = 0; [double]$hy = 0; [double]$hw = 0
for ($y = 0; $y -lt $H; $y++) {
  $row = $y * $sd.Stride
  for ($x = 0; $x -lt $W; $x++) {
    $i = $row + $x*4
    if ($sBuf[$i+3] -lt 40) { continue }
    $l = (0.299*$sBuf[$i+2] + 0.587*$sBuf[$i+1] + 0.114*$sBuf[$i]) / 255.0
    if ($l -gt $CORE_MIN) { $hx += $x; $hy += $y; $hw++ }
  }
}
if ($hw -gt 0) { $hx = $hx / $hw; $hy = $hy / $hw } else { $hx = $W/2; $hy = $H/2 }

# Longest distance from the head to any visible pixel - the far tip of the tail. Normalising by
# this makes the gradient span the whole shape whatever size the master is.
[double]$maxDist = 1
for ($y = 0; $y -lt $H; $y++) {
  $row = $y * $sd.Stride
  for ($x = 0; $x -lt $W; $x++) {
    if ($sBuf[$row + $x*4 + 3] -lt 40) { continue }
    $dx = $x - $hx; $dy = $y - $hy
    $d = [Math]::Sqrt($dx*$dx + $dy*$dy)
    if ($d -gt $maxDist) { $maxDist = $d }
  }
}
Write-Output ("head at {0:N0},{1:N0}   tail reach {2:N0}px" -f $hx, $hy, $maxDist)

foreach ($row in $TABLE) {
  $name = $row[0]
  $br=[double]$row[1]; $bg=[double]$row[2]; $bb=[double]$row[3]
  $cr=[double]$row[4]; $cg=[double]$row[5]; $cb=[double]$row[6]
  $op=[double]$row[7]

  $res = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $od = $res.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $oBuf = New-Object byte[] $n

  # Optional head-to-tip gradient for this shout.
  $grad = $HEAD_GRADIENT[$name]
  $hasGrad = ($grad -ne $null)
  if ($hasGrad) { $gr=[double]$grad[0]; $gg0=[double]$grad[1]; $gb=[double]$grad[2] }

  # Nested x/y rather than a flat byte walk: the gradient needs each pixel's POSITION, which a
  # raw index does not give without recovering it anyway.
  for ($y = 0; $y -lt $H; $y++) {
   for ($x = 0; $x -lt $W; $x++) {
    $i = $y * $sd.Stride + $x * 4
    $a = $sBuf[$i+3]
    if ($a -eq 0) { continue }
    $l = (0.299*$sBuf[$i+2] + 0.587*$sBuf[$i+1] + 0.114*$sBuf[$i]) / 255.0

    # This pixel's body colour. Normally flat; with a gradient it runs from the head colour at
    # the comet's head to the table colour at the tail tip, using the SAME smoothstep across
    # 0.30..0.70 that the Thu'um bar uses, so each colour owns about half the shape.
    $pr = $br; $pg = $bg; $pb = $bb
    if ($hasGrad) {
      $dx = $x - $hx; $dy = $y - $hy
      $t = [Math]::Sqrt($dx*$dx + $dy*$dy) / $maxDist
      if ($t -gt 1.0) { $t = 1.0 }
      $f = ($t - 0.30) / 0.40
      if ($f -lt 0.0) { $f = 0.0 }
      if ($f -gt 1.0) { $f = 1.0 }
      $f = $f * $f * (3.0 - 2.0 * $f)
      $pr = $gr + ($br - $gr) * $f
      $pg = $gg0 + ($bg - $gg0) * $f
      $pb = $gb + ($bb - $gb) * $f
    }

    if ($l -lt $RIM_MAX) {
      # The rim: a very dark version of the body hue, never pure black - that is what keeps
      # it looking drawn rather than cut out.
      $oBuf[$i]   = [byte]($pb * $l * 0.45)
      $oBuf[$i+1] = [byte]($pg * $l * 0.45)
      $oBuf[$i+2] = [byte]($pr * $l * 0.45)
    }
    else {
      $k = 0.42 + 0.58 * $l
      $rr = $pr * $k; $gg = $pg * $k; $bl = $pb * $k
      if ($l -gt $CORE_MIN) {
        # Blend toward the CORE colour, not white. This is the third lever.
        $hot = ($l - $CORE_MIN) / (1.0 - $CORE_MIN)
        if ($hot -gt 1.0) { $hot = 1.0 }
        $rr = $rr + ($cr - $rr) * $hot
        $gg = $gg + ($cg - $gg) * $hot
        $bl = $bl + ($cb - $bl) * $hot
      }
      $oBuf[$i]   = [byte][Math]::Min(255, [Math]::Max(0, $bl))
      $oBuf[$i+1] = [byte][Math]::Min(255, [Math]::Max(0, $gg))
      $oBuf[$i+2] = [byte][Math]::Min(255, [Math]::Max(0, $rr))
    }
    $oBuf[$i+3] = [byte][Math]::Min(255, $a * $op)
   }
  }

  [System.Runtime.InteropServices.Marshal]::Copy($oBuf, 0, $od.Scan0, $n)
  $res.UnlockBits($od)
  $path = Join-Path $OUTDIR ("Dovahkiin_Shout_" + $name + ".png")
  $res.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $res.Dispose()
  Write-Output ("  {0,-22} -> {1}" -f $name, (Split-Path $path -Leaf))
}

Write-Output ""
Write-Output ("written {0} icons to {1}" -f $TABLE.Count, $OUTDIR)
