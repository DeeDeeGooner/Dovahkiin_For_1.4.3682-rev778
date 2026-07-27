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
  @("MarkedForDeath",   150,160,175,  255,255,255, 1.00),
  @("ClearSkies",       170,215,245,  255,255,255, 1.00),
  # Slow Time: Unrelenting Force's OLD pale grey-white body, with a BLUE core.
  @("SlowTime",         235,235,240,   70,145,235, 1.00),
  @("BecomeEthereal",   200,225,245,  255,255,255, 0.72),  # translucent by design
  @("DrainVitality",    150, 50,200,  255,255,255, 1.00),
  @("Dismay",           220, 40, 40,  255,255,255, 1.00),
  @("Cyclone",          185,190,200,  255,255,255, 0.60),  # faintest of the family
  # Not built yet - icons generated anyway, they cost nothing and will be waiting.
  @("StormCall",        140, 90,210,  255,255,255, 1.00),
  @("SoulTear",         190, 25, 35,  255,255,255, 1.00),
  @("DragonAspect",     215,165, 70,  255,255,255, 1.00),
  @("Dragonrend",       228,228,218,  150,160,172, 1.00)
)

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

foreach ($row in $TABLE) {
  $name = $row[0]
  $br=[double]$row[1]; $bg=[double]$row[2]; $bb=[double]$row[3]
  $cr=[double]$row[4]; $cg=[double]$row[5]; $cb=[double]$row[6]
  $op=[double]$row[7]

  $res = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $od = $res.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $oBuf = New-Object byte[] $n

  for ($i = 0; $i -lt $n; $i += 4) {
    $a = $sBuf[$i+3]
    if ($a -eq 0) { continue }
    $l = (0.299*$sBuf[$i+2] + 0.587*$sBuf[$i+1] + 0.114*$sBuf[$i]) / 255.0

    if ($l -lt $RIM_MAX) {
      # The rim: a very dark version of the body hue, never pure black - that is what keeps
      # it looking drawn rather than cut out.
      $oBuf[$i]   = [byte]($bb * $l * 0.45)
      $oBuf[$i+1] = [byte]($bg * $l * 0.45)
      $oBuf[$i+2] = [byte]($br * $l * 0.45)
    }
    else {
      $k = 0.42 + 0.58 * $l
      $rr = $br * $k; $gg = $bg * $k; $bl = $bb * $k
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

  [System.Runtime.InteropServices.Marshal]::Copy($oBuf, 0, $od.Scan0, $n)
  $res.UnlockBits($od)
  $path = Join-Path $OUTDIR ("Dovahkiin_Shout_" + $name + ".png")
  $res.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $res.Dispose()
  Write-Output ("  {0,-22} -> {1}" -f $name, (Split-Path $path -Leaf))
}

Write-Output ""
Write-Output ("written {0} icons to {1}" -f $TABLE.Count, $OUTDIR)
