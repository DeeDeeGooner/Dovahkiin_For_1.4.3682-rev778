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

---

# Call of Valor's greatsword — `Dovahkiin_ValorGreatsword`

Same instruction from the user, applied to a blade: *"medieval mod should recognise it as one
it's weapon (using their same position, orientation when held by a pawn, and same animation too
since melee animation is in the modlist, also the weapon it should behave like is the greatsword
in the blades section)."* Reference weapon: **`DankPyon_MeleeWeapon_Greatsword`**.

## What is copied verbatim, and what is NOT

| field | ours | theirs | |
|---|---|---|---|
| `MeleeWeaponType` | **6** | 6 | **copied** — 6 is two-handed sword. This is the field that picks the animation set |
| `Rotation` | **45.0** | 45.0 | **copied** |
| `ScaleX` / `ScaleY` | **1.25** | 1.25 | **copied** — matches both defs' `drawSize` |
| `OffY` | -0.0113 | -0.0113 | **copied** — it is essentially zero, and our sprite is symmetric about its own axis by construction |
| `OffX` | **0.363739** | 0.5461391 | **DERIVED** |
| `BladeStart` | **0.268436** | 0.3050847 | **DERIVED** |
| `BladeEnd` | **1.157929** | 1.382298 | **DERIVED** |

**The last three could not be copied, and copying them is the trap.** They are distances in
WORLD UNITS measured from the pawn's hand, so they encode where *their* grip and *their* blade
sit in *their* sprite. Our sword is a different shape — it has **two crossguards**, so its hilt
occupies far more of the weapon's length than theirs does.

## The formula, and how it was validated

Derived, then checked by reproducing **their** published numbers from **their** sprite:

```
weaponLength = (pixels along the weapon axis) x drawSize / 256
OffX         = (0.5 - handFraction)       x weaponLength
BladeStart   = (bladeFraction - handFraction) x weaponLength
BladeEnd     = (1.0 - handFraction)       x weaponLength
```

Their greatsword measures **340.8 px** along its axis, hand at fraction **0.1718**, blade from
**0.3551**. That gives `OffX 0.5461` against their published **0.5461391**, and `BladeStart
0.3050` against their **0.30508475** — exact to five decimals. A formula that reproduces the
reference file is a formula worth trusting on ours.

## Our measurements

Profiled along the weapon axis at **alpha > 128**, 24 buckets. The solid threshold matters: our
blade carries a spectral **bloom**, and at alpha > 8 that halo counts as weapon and inflates
every width (20,672 px of ink against 10,443 for theirs — nearly double, almost all of it glow).

| | ours | theirs |
|---|---|---|
| length along axis | 325.3 px | 340.8 px |
| lower grip | 0.02–0.10 | — |
| **lower crossguard** | **0.15–0.23** | — |
| middle grip | ~0.27 | 0.10–0.19 |
| **main crossguard** | **0.31–0.40** | **0.23–0.27** |
| blade | 0.44 → 1.00 | 0.36 → 1.00 |

**Hand placed at 0.271 — the middle grip, directly below the main crossguard**, which is the
same *role* their hand occupies at 0.1718 (the grip directly below their guard). Matching their
0.1718 numerically instead would have landed our hand **on our lower crossguard**, because our
hilt is longer. That is the difference between the same position and the same number, and the
user asked for the first.

**This is the one value the game must settle.** Only a playtest shows whether it reads as gripped
or as floating. If it needs to move, `handFraction` is the single knob — recompute all three
fields from it with the formula above. Matching their 0.1718 exactly gives `OffX 0.5210`,
`BladeStart 0.4098`, `BladeEnd 1.3152`, if the numeric match ever turns out to look better.

## Same diagonal, so the angles transfer at all

Quadrant ink at **alpha > 8**, threshold stated because a table without one cannot be re-checked:

| sprite | topLeft | topRight | botLeft | botRight | reads as |
|---|---|---|---|---|---|
| their greatsword | 206 | **4100** | 5931 | 206 | bottom-left → top-right |
| **our greatsword** | 861 | **8016** | 10554 | 1241 | bottom-left → top-right |

Same diagonal, blade at the same end — so no mirroring was needed here, unlike the axe. **Re-run
this if the sword art is ever redrawn.**
