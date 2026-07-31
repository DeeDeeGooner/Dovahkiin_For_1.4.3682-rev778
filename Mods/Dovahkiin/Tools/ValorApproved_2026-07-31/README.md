# CALL OF VALOR — THE APPROVED LOOK, 2026-07-31

**The user's words: "this one right here is the best of every version we had until now."**
Frozen here so that later changes cannot lose it.

> **CHECKPOINT MOVED.** This folder was first taken at the pauldrons-and-chest stage and has
> since been **overwritten at the user's instruction** to become the checkpoint *including the
> abdominal half*. Overwriting is safe rather than destructive: **the earlier checkpoint is
> still recoverable in full from git at commit `008e674`**, contents and manifest alike. That
> is the reason a snapshot may be overwritten at all — the history is the real archive, this
> folder is the convenient one.

This folder is a **snapshot, not a source**. Nothing in the mod loads it — `Tools/` is not
scanned for textures. It exists so that any future version of the champion can be compared
against the one that was actually approved, byte for byte, instead of from memory.

## What is in it

| | |
|---|---|
| 36 PNGs | the full valor overlay set — 5 body types × 2 levels × 3 rotations, plus 3 helm rotations and the (unused) aura textures |
| `ValorGreatsword_A.png` | variant A, the katana/kissaki-tipped greatsword |
| `SHA256.txt` | a hash per file. **This is the actual check** |

## What this version is

- Dragon Aspect's **geometry**, in the **spectral white-blue palette** — the user's reversal of
  the earlier "normal armour in shape" brief, after the normal-plate version was built, rendered
  and judged dull.
- **No aura.** No ring, no crescents.
- **Pauldrons** instead of the three swept shoulder fins — three overlapping curved lames per
  shoulder, sweeping from outboard-below, over the joint, and down inboard across the chest.
- A **muscled cuirass** — pectoral domes drawn with `PathGradientBrush`, under-pec creases with
  lit lips, a sternum groove. North gets shoulder blades and a spine instead.
- The **abdominal half**: three rows of segments, each narrower than the one above and sweeping
  up at its outer end; the sternum groove extended into one continuous linea alba; an iliac line
  per side. North gets one erector-spinae mass per side rather than rows.
- **No crest shards.** Suppressed deliberately: they run down the same chest the cuirass occupies
  and win outright, so the pectorals stopped existing. There is no alpha at which both read.

## How to reproduce it exactly

```
$env:DOVAH_DEST    = "<some scratch folder>"
$env:DOVAH_PALETTE = "valor"
& "Mods\Dovahkiin\Tools\GenerateDragonAspect.ps1"
```

`DOVAH_DEST` is **not optional**. Without it the script writes into
`Textures\Things\Pawn\DragonAspect` and overwrites Dragon Aspect's own signed-off art.

To preview him, with the greatsword and no aura:

```
$env:DOVAH_OVERLAY_DIR  = "<that same scratch folder>"
$env:DOVAH_AXE_OVERRIDE = "<...>\valor_gs_A.png"
$env:DOVAH_NO_AURA      = "1"
& "Mods\Dovahkiin\Tools\PreviewAncientDragonborn.ps1"
```

## How to check a later version against this one

Hash the regenerated files and compare against `SHA256.txt`. Any difference is a real change to
approved art, and it should be a deliberate one:

```powershell
$snap = "Mods\Dovahkiin\Tools\ValorApproved_2026-07-31"
$new  = "<the folder DOVAH_DEST wrote to>"
Get-Content (Join-Path $snap "SHA256.txt") | ForEach-Object {
  $parts = $_ -split '\s+', 2
  if ($parts.Count -lt 2) { return }
  $candidate = Join-Path $new $parts[1].Trim()
  if (-not (Test-Path $candidate)) { "MISSING  " + $parts[1]; return }
  if ((Get-FileHash $candidate -Algorithm SHA256).Hash -ne $parts[0]) { "CHANGED  " + $parts[1] }
}
```

Silence means identical.

## The one thing not frozen here

The **portal cast effect** is not part of this snapshot — it is a separate effect with its own
generator (`GenerateValorPortal.ps1`) and it was approved separately. Its sprites are still
preview-only.
