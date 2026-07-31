# =====================================================================================
#  CALL OF VALOR'S PALETTE - the single source. Dot-source it; do not copy it.
#
#      . "$PSScriptRoot\ValorPalette.ps1"
#
#  WHY THIS FILE EXISTS
#  --------------------
#  The champion's armour and his greatsword were authored in separate scripts and ended up
#  with palettes that were *nearly* identical - (196,232,255) against (206,232,252),
#  (120,168,216) against (120,162,200). Close enough that nobody could see the difference,
#  and far enough that they were two palettes rather than one. A lookalike ramp drifts the
#  first time either side is retuned, and the drift is invisible until the two pieces are
#  side by side on a pawn.
#
#  So the colours live here and both generators read them. Retune once, and the weapon
#  follows the wearer by construction.
#
#  ROLES, because the sword and the armour use different names for the same jobs:
#
#      C_HOT       the hot edge, pure light   -> the weapon's luminous RIM
#      C_GOLD      the lit face               -> the bright end of the blade's gradient
#      C_MID       pale steel-blue            -> the dim end of it
#      C_DEEP      deep cool shadow           -> shading under plate; too dark for a
#                                                translucent blade body
#      C_AZURE     the aura's second colour   -> the weapon's outer BLOOM
#
#  A colour named for the ARMOUR's use of it can be given to the weapon only where the
#  role matches. Matching by role rather than by eye is the whole point of the file.
#
#  These are the exact values the DOVAH_PALETTE=valor block carried when it was proven
#  inert - 36 of 36 shipped textures byte-identical. Any change here MUST be re-proven
#  against Tools/ValorApproved_2026-07-31/SHA256.txt before it is believed.
# =====================================================================================

$C_DEEP       = @( 34,  58,  84)   # deep cool shadow, where bronze had its darkest body
$C_MID        = @(120, 162, 200)   # pale steel-blue
$C_GOLD       = @(206, 232, 252)   # the lit face
$C_HOT        = @(255, 255, 255)   # hot edge - pure light
$C_EMBER      = @(214, 240, 255)   # rim light. A ghost's rim is cold, not amber.
$C_ORANGE     = @(168, 214, 250)   # crest
$C_OCORE      = @(255, 255, 255)   # crest hot centre
$C_AZURE      = @(120, 196, 255)   # aura's second colour
$C_BLEND_MID  = @(232, 246, 255)   # the midtone the crescent blend passes through
$C_BLUE_LIT   = @(150, 196, 236)
$C_BLUE_MID   = @( 92, 140, 186)
$C_BLUE_DEEP  = @( 28,  52,  82)
$C_BLUE_HOT   = @(226, 246, 255)
