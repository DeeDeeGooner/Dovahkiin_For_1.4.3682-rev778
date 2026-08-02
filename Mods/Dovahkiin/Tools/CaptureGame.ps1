# GIVE THE SESSION EYES ON THE RUNNING GAME.
#
# The user asked whether a plugin exists that would let me actually SEE what is happening in
# play. Searched the connector registry: there is nothing for screen capture or game vision. But
# the capability is real and it does not need one - Windows will hand over the contents of a
# window to anyone who asks, and this project already renders and reads PNGs everywhere.
#
# WHY THIS IS WORTH HAVING HERE SPECIFICALLY. Almost every defect in this mod has been VISUAL and
# reported in words: "the armour stayed standing over her", "the sword tilts in the wrong
# direction", "he appears as soon as I click". Each of those cost at least one round of me
# guessing at what the words meant - the sword took three. A screenshot collapses that: the user
# says "look", and the thing is either wrong on screen or it is not.
#
# IT CAPTURES THE RIMWORLD WINDOW ONLY, BY DESIGN. It finds the window by process and captures
# its rectangle. If RimWorld is not running it says so and captures NOTHING - it will not fall
# back to grabbing the whole desktop, because the desktop is the user's private business and a
# tool that quietly photographs it is not one they asked for.
#
# Usage:
#   $env:DOVAH_PREVIEW = "<folder>"     # optional; defaults beside this script
#   & Tools\CaptureGame.ps1
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$signature = @'
[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")]
public static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")]
public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@
if (-not ("DovahWin" -as [type])) {
  # NO -UsingNamespace here. Add-Type already emits `using System.Runtime.InteropServices;` for a
  # -MemberDefinition block, and adding it again is a DUPLICATE USING - which this PowerShell
  # treats as warning-as-error and refuses to compile. The message points at the Add-Type line,
  # not at the redundant parameter.
  Add-Type -MemberDefinition $signature -Name "DovahWin" -Namespace ""
}

$OUTDIR = if ($env:DOVAH_PREVIEW) { $env:DOVAH_PREVIEW } else { $PSScriptRoot }
if (-not (Test-Path $OUTDIR)) { New-Item -ItemType Directory -Force $OUTDIR | Out-Null }
$OUTFILE = Join-Path $OUTDIR "game_capture.png"

# RimWorld's process is RimWorldWin64. Match on the process rather than a window TITLE: titles
# are localised and change with the loaded save, and a title match would silently find nothing
# on a non-English install.
$proc = Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $proc) {
  Write-Output "RIMWORLD IS NOT RUNNING (no RimWorldWin64 process with a window)."
  Write-Output "Nothing captured - this tool never falls back to the whole desktop."
  exit 2
}

$handle = $proc.MainWindowHandle
if ([DovahWin]::IsIconic($handle)) {
  # 9 = SW_RESTORE. A minimised window has no pixels to read; restoring it is the only way to
  # get an image, and it is a visible action so it is announced rather than done silently.
  Write-Output "RimWorld was minimised - restoring it so there is something to capture."
  [void][DovahWin]::ShowWindow($handle, 9)
  Start-Sleep -Milliseconds 700
}
[void][DovahWin]::SetForegroundWindow($handle)
Start-Sleep -Milliseconds 400

$rect = New-Object DovahWin+RECT
if (-not [DovahWin]::GetWindowRect($handle, [ref]$rect)) {
  Write-Output "Could not read RimWorld's window rectangle."
  exit 3
}
$wide = $rect.Right - $rect.Left
$high = $rect.Bottom - $rect.Top
if ($wide -le 0 -or $high -le 0) {
  Write-Output "RimWorld's window has no size ($wide x $high)."
  exit 4
}

$bmp = New-Object System.Drawing.Bitmap $wide, $high, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $wide, $high))
$gfx.Dispose()
$bmp.Save($OUTFILE, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output ("CAPTURED {0}x{1} -> {2}" -f $wide, $high, $OUTFILE)
