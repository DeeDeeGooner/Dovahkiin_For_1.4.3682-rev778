# WeaponTweakData — Melee Animation compatibility

Melee Animation (`co.uk.epicguru.meleeanimation`) needs a small JSON per melee weapon to know
where the grip and blade are, so it can animate a swing instead of falling back to a default
and logging:

```
[MeleeAnim] erzou.dovahkiin 'Dovahkiin' has 1 missing weapon tweak data.
```

**A mod may ship its own tweak data — this folder is read by Melee Animation directly.** That
was checked rather than assumed: `XenotypeSatyr` ships a `WeaponTweakData` folder of its own,
which is what proves the convention. Editing Melee Animation's own folder would have worked and
would have been wiped by its next update.

**Naming is load-bearing:** `<ItemDefName>_<packageId>.json`.

**This is not a dependency.** No `MayRequire`, no assembly reference, nothing in our defs points
at Melee Animation. If that mod is absent these files are simply never read, which satisfies
`CLAUDE.md`'s rule that only Harmony and HugsLib may be hard requirements.

## The fields

**Every value is `DankPyon_MeleeWeapon_Halberd`'s, copied verbatim** — at the user's request the
spectral halberd must be held, oriented and animated exactly as Medieval Overhaul's halberd is.
Nothing here is estimated.

| field | value | why |
|---|---|---|
| `MeleeWeaponType` | **7** | the halberd's type — a polearm, not the axe type 2 |
| `Rotation` | 45 | matches `equippedAngleOffset` 45 on `DankPyon_Base_Sharp_Oversize` |
| `ScaleX` / `ScaleY` | 1.5 | the halberd is an oversize weapon; our def's `drawSize` matches |
| `OffX` / `OffY` | theirs | grip offset in the pawn's hand |
| `BladeStart` / `BladeEnd` | theirs | the edge span, used for hit sparks and executions |

## Why the art had to be mirrored first

These values are expressed in **their texture's frame**, so they only transfer if our sprite runs
along the same diagonal with the head at the same end. Measured by counting opaque pixels per
quadrant:

| sprite | topLeft | topRight | botLeft | botRight | reads as |
|---|---|---|---|---|---|
| their halberd | 105 | **5583** | 3252 | 105 | bottom-left → top-right |
| their greataxe | 1429 | **8418** | 3932 | 192 | bottom-left → top-right |
| ours, before | **5724** | 203 | 122 | 2141 | top-left → bottom-right |

Ours ran the **opposite** diagonal. Copying their numbers onto it would have had the pawn
gripping the weapon by its blade. `GenerateAncientAxe.ps1` now draws butt at (0.26, 0.86) and
head at (0.70, 0.20), and the same check confirms it reads bottom-left → top-right.

**Re-run that check if the art is ever redrawn** — it is the one thing that silently invalidates
every value in this file.
