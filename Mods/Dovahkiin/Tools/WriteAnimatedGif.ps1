# =====================================================================================
#  Write-AnimatedGif - an animated GIF writer for .NET, by splicing.
#
#  WHY THIS FILE EXISTS AT ALL
#  ---------------------------
#  There is no animated-GIF encoder in .NET. For judging a MOVING effect - a portal
#  opening, an aura breathing, a wave travelling - a strip of stills is close to useless,
#  because the thing being judged is the motion. This project has needed that twice and
#  rebuilt the writer both times: the save notebook claimed a working copy lived in
#  Tools/ and it never did. It does now. Dot-source it:
#
#      . "$PSScriptRoot\WriteAnimatedGif.ps1"
#      Write-AnimatedGif -Frames $frameList -Path "out.gif" -DelayHundredths 5
#
#  HOW THE SPLICE WORKS
#  --------------------
#  .NET *can* write a single-frame GIF. So: save each frame to a MemoryStream as its own
#  GIF, cut out the two parts worth keeping - that frame's colour table and its LZW image
#  data - and assemble one multi-frame file by hand around them.
#
#  The one detail that is not optional: each frame's palette is written as a LOCAL colour
#  table, and the global colour table flag in the logical screen descriptor is CLEARED.
#  The encoder picks a palette per frame; forcing them all through one shared global table
#  shears the colours as the animation runs.
#
#  Returns the number of frames written.
# =====================================================================================

function Write-AnimatedGif {
  param(
    [Parameter(Mandatory = $true)] [System.Drawing.Bitmap[]] $Frames,
    [Parameter(Mandatory = $true)] [string] $Path,
    [int] $DelayHundredths = 5,   # 5 = 20fps, 7 = ~14fps
    [int] $LoopCount = 0          # 0 = forever
  )

  if ($Frames.Count -lt 1) { throw "Write-AnimatedGif: no frames" }

  $canvasW = $Frames[0].Width
  $canvasH = $Frames[0].Height

  $outStream = New-Object System.IO.FileStream $Path, ([System.IO.FileMode]::Create)
  try {
    # --- header + logical screen descriptor. GCT flag cleared; see the header note. ------
    $outStream.Write([byte[]]@(0x47, 0x49, 0x46, 0x38, 0x39, 0x61), 0, 6)   # "GIF89a"
    $outStream.Write((Le16 $canvasW), 0, 2)
    $outStream.Write((Le16 $canvasH), 0, 2)
    # 0x70: colour resolution 8 bits, no global colour table, unsorted.
    $outStream.Write([byte[]]@(0x70, 0x00, 0x00), 0, 3)

    # --- Netscape 2.0 application extension: the only way to say "loop" ------------------
    $netscape = New-Object 'byte[]' 0
    $netscape += [byte[]]@(0x21, 0xFF, 0x0B)
    $netscape += [System.Text.Encoding]::ASCII.GetBytes("NETSCAPE2.0")
    $netscape += [byte[]]@(0x03, 0x01)
    $netscape += (Le16 $LoopCount)
    $netscape += [byte]0x00
    $outStream.Write($netscape, 0, $netscape.Length)

    $written = 0
    foreach ($frame in $Frames) {
      $parsed = Split-SingleFrameGif $frame
      if ($null -eq $parsed) { continue }

      # --- graphic control extension. Disposal method 1 = leave the frame in place, which
      #     is correct here because every frame is fully opaque and covers the canvas.
      $gce = New-Object 'byte[]' 0
      $gce += [byte[]]@(0x21, 0xF9, 0x04, 0x04)
      $gce += (Le16 $DelayHundredths)
      $gce += [byte[]]@(0x00, 0x00)
      $outStream.Write($gce, 0, $gce.Length)

      # --- image descriptor, with the LOCAL colour table flag SET ------------------------
      $entries = [int]($parsed.Palette.Length / 3)
      $sizeBits = 0
      while ((1 -shl ($sizeBits + 1)) -lt $entries) { $sizeBits++ }
      $descriptor = New-Object 'byte[]' 0
      $descriptor += [byte]0x2C
      $descriptor += (Le16 0)
      $descriptor += (Le16 0)
      $descriptor += (Le16 $parsed.Width)
      $descriptor += (Le16 $parsed.Height)
      $descriptor += [byte](0x80 -bor $sizeBits)
      $outStream.Write($descriptor, 0, $descriptor.Length)
      $outStream.Write($parsed.Palette, 0, $parsed.Palette.Length)
      $outStream.Write($parsed.Data, 0, $parsed.Data.Length)
      $written++
    }

    $outStream.WriteByte(0x3B)   # trailer
  }
  finally {
    $outStream.Close()
    $outStream.Dispose()
  }
  return $written
}

function Le16([int]$value) {
  return [byte[]]@([byte]($value -band 0xFF), [byte](($value -shr 8) -band 0xFF))
}

# -------------------------------------------------------------------------------------
#  Encode one bitmap as a standalone GIF, then hand back the only two pieces worth
#  keeping: its colour table, and its LZW data re-emitted verbatim (length-prefixed
#  sub-blocks and their 0x00 terminator included, so nothing has to be re-chunked).
# -------------------------------------------------------------------------------------
function Split-SingleFrameGif([System.Drawing.Bitmap]$frame) {
  $memory = New-Object System.IO.MemoryStream
  $frame.Save($memory, [System.Drawing.Imaging.ImageFormat]::Gif)
  $raw = $memory.ToArray()
  $memory.Dispose()
  if ($raw.Length -lt 14) { return $null }

  $packed = [int]$raw[10]
  $cursor = 13
  $palette = $null
  if (($packed -band 0x80) -ne 0) {
    $tableBytes = 3 * (1 -shl (($packed -band 0x07) + 1))
    $palette = $raw[13..(13 + $tableBytes - 1)]
    $cursor = 13 + $tableBytes
  }

  # walk the block stream to the image descriptor, skipping any extensions the encoder
  # happened to emit
  while ($cursor -lt $raw.Length) {
    $marker = $raw[$cursor]
    if ($marker -eq 0x2C) { break }
    if ($marker -eq 0x3B) { return $null }
    if ($marker -eq 0x21) {
      $cursor += 2                       # introducer + label
      while ($cursor -lt $raw.Length -and $raw[$cursor] -ne 0) {
        $cursor += 1 + $raw[$cursor]     # sub-block length prefix + its payload
      }
      $cursor++                          # the 0x00 that ends the sub-block chain
      continue
    }
    return $null
  }
  if ($cursor -ge $raw.Length) { return $null }

  # [int] ON BOTH HALVES IS LOAD-BEARING. PowerShell's -shl PRESERVES THE LEFT OPERAND'S
  # TYPE, so `$raw[$cursor + 6] -shl 8` on a byte[] element returns a BYTE - the high bits
  # are shifted straight out and the answer is 0, with no error and no warning. Written
  # without these casts, every frame's descriptor got width 0 and height 0, and the file
  # was rejected wholesale by GDI+ with "the parameter is not valid" - a message pointing
  # nowhere near the arithmetic. Cast anything read out of a byte[] before shifting it.
  $imgW = [int]$raw[$cursor + 5] + ([int]$raw[$cursor + 6] -shl 8)
  $imgH = [int]$raw[$cursor + 7] + ([int]$raw[$cursor + 8] -shl 8)
  $imgPacked = [int]$raw[$cursor + 9]
  $cursor += 10
  if (($imgPacked -band 0x80) -ne 0) {
    # a local table on the source frame wins over the global one - it is this image's own
    $localBytes = 3 * (1 -shl (($imgPacked -band 0x07) + 1))
    $palette = $raw[$cursor..($cursor + $localBytes - 1)]
    $cursor += $localBytes
  }
  if ($null -eq $palette) { return $null }

  # LZW minimum code size, then the sub-blocks, then the 0x00 terminator - copied whole
  $dataStart = $cursor
  $cursor++
  while ($cursor -lt $raw.Length -and $raw[$cursor] -ne 0) {
    $cursor += 1 + $raw[$cursor]
  }
  $dataEnd = $cursor          # index of the terminating 0x00
  if ($dataEnd -ge $raw.Length) { return $null }

  return @{
    Palette = [byte[]]$palette
    Data    = [byte[]]$raw[$dataStart..$dataEnd]
    Width   = $imgW
    Height  = $imgH
  }
}
