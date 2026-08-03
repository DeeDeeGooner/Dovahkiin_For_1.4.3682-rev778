# MakeFlightRotations.ps1 - all four flight facings from ONE top-down sprite.
#
# WHY ROTATION AND NOT FOUR DRAWINGS:
# RimWorld's GROUND animals are drawn from above-and-slightly-in-front, so their south view
# shows a face and their north view shows the back of the skull - two genuinely different
# pictures with the head at the top in both.
#
# A FLYING creature is seen from DIRECTLY OVERHEAD. From there you see its back no matter
# which way it is going; only the ORIENTATION changes. So the south view is the north view
# turned 180 degrees, and east/west are the same image turned 90.
#
# Applying the ground convention to a flying creature produces a dragon that faces the camera
# while flying - which is what one generated reference did, and it is unusable.
#
# Rotation, not mirroring, for east/west: mirroring flips the creature's handedness, so any
# asymmetric detail (a scar, a notched wing) would swap sides between the two.

Add-Type -AssemblyName System.Drawing

$SOURCE  = if ($env:DOVAH_FLIGHT) { $env:DOVAH_FLIGHT } else {
    "C:\Games\Rimworld\RimWorld\RimWorldFolder\Mods\Dovahkiin\Tools\DragonArt_2026-08-03\Alduin_flight_northview.png" }
$OUT_DIR = if ($env:DOVAH_DEST) { $env:DOVAH_DEST } else { Split-Path $SOURCE -Parent }
$PREFIX  = if ($env:DOVAH_PREFIX) { $env:DOVAH_PREFIX } else { "Alduin_flight" }

$northBmp = [System.Drawing.Bitmap]::FromFile($SOURCE)
if ($northBmp.Width -ne $northBmp.Height) {
    Write-Output ("WARNING: source is {0}x{1}, not square - a 90-degree turn will not fit its own frame" -f $northBmp.Width, $northBmp.Height)
}
Write-Output ("source: {0}  ({1} x {2})" -f (Split-Path $SOURCE -Leaf), $northBmp.Width, $northBmp.Height)

# North is the source as drawn - head toward the top, flying away from the camera - so it is
# only re-saved when the output folder differs from the source. Saving it over itself throws
# "A generic error occurred in GDI+": Bitmap.FromFile holds a lock on the file it read.
$northOut = Join-Path $OUT_DIR ("{0}_northview.png" -f $PREFIX)
if ([System.IO.Path]::GetFullPath($northOut) -ne [System.IO.Path]::GetFullPath($SOURCE)) {
    $northBmp.Save($northOut, [System.Drawing.Imaging.ImageFormat]::Png)
}

$ROTATIONS = @(
    @( "southview", [System.Drawing.RotateFlipType]::Rotate180FlipNone ),
    @( "eastview",  [System.Drawing.RotateFlipType]::Rotate90FlipNone  ),
    @( "westview",  [System.Drawing.RotateFlipType]::Rotate270FlipNone )
)
foreach ($rotation in $ROTATIONS) {
    $copy = New-Object System.Drawing.Bitmap $northBmp
    $copy.RotateFlip($rotation[1])
    $outPath = Join-Path $OUT_DIR ("{0}_{1}.png" -f $PREFIX, $rotation[0])
    $copy.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output ("  wrote {0}_{1}.png  ({2})" -f $PREFIX, $rotation[0], $rotation[1])
    $copy.Dispose()
}
$northBmp.Dispose()
Write-Output "DONE"
