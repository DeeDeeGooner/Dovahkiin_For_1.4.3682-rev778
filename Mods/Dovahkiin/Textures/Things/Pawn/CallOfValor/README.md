# Call of Valor's overlay art — READ BEFORE TOUCHING ANYTHING HERE

## These 36 files share Dragon Aspect's filenames ON PURPOSE, and that is a trap

Every file in this folder is named `DragonAspect_*` or `DragonAspectHelm_*`. **They are NOT
Dragon Aspect's art.** They are Call of Valor's champion, produced by the same generator with a
different palette, which is why the names came out the same.

The Dovahkiin's own signed-off art lives in the sibling folder:

```
Textures/Things/Pawn/DragonAspect/     <- the DOVAHKIIN. Bronze-into-blue.
Textures/Things/Pawn/CallOfValor/      <- the CHAMPION. Spectral white-blue. This folder.
```

**Copying a file from one to the other silently replaces one character's armour with the
other's, and nothing will error.** Only the folder tells them apart. If you are moving a file
between these two folders, stop — you almost certainly want to regenerate instead.

## How to regenerate this folder

```
$env:DOVAH_PALETTE = "valor"
$env:DOVAH_DEST    = "<a scratch folder, NEVER a Textures folder>"
& "Tools\GenerateDragonAspect.ps1"
```

**`DOVAH_DEST` is not optional.** `GenerateDragonAspect.ps1` defaults `$DEST` to
`Textures/Things/Pawn/DragonAspect` — the Dovahkiin's folder — so a run without it overwrites
her signed-off armour with whatever palette is active. That is a real thing this project has
had to guard against, not a hypothetical.

Then diff the result against `Tools/ValorApproved_2026-07-31/SHA256.txt` before believing a
change touched only what it meant to. That check has caught real mistakes more than once.

## Provenance — verified 2026-07-31, not assumed

- All 36 files here match their recorded hash in `Tools/ValorApproved_2026-07-31/SHA256.txt`.
- A fresh `DOVAH_PALETTE=valor` run reproduces the checkpoint **36 of 36 byte-identical**, so the
  generator and the approved snapshot agree.
- A fresh **default** run (no palette override) reproduces the Dovahkiin's shipping art **36 of
  36 byte-identical**, so the valor palette is provably inert on her armour. **Repeat that check
  if the palette block is ever edited.**

## What is NOT here

- **His greatsword** — `Textures/Things/Item/Equipment/ValorGreatsword.png`
- **His portal sprites** — `Textures/Things/Effects/ValorPortal/`
- **No aura.** Deliberate, and the user's rule: the aura is the Dovahkiin's signature and giving
  it to him was the mistake the whole design brief exists to correct. `DOVAH_NO_AURA` on the
  preview script; in game his overlay must simply not draw one.

## Nothing loads this folder yet

As of 2026-07-31 no C# and no def references these files. They are installed and safe, waiting
on `Thing_ValorPortal` and the summon itself. The path constant will need to be a variable
rather than Dragon Aspect's hardcoded one — that is the whole reason for the separate folder.
