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

Copied from Medieval Overhaul's own greataxe entry, because that is the closest analogue to our
axe that is already tuned:

| field | meaning |
|---|---|
| `MeleeWeaponType` | **2** — what Medieval Overhaul's `Greataxe` uses, i.e. a two-handed axe. |
| `Rotation` | corrects the texture's own diagonal. Our art points up-left at roughly 45°. |
| `OffX` / `OffY` | grip offset in the pawn's hand. |
| `BladeStart` / `BladeEnd` | fraction along the weapon that is *edge*, used for the hit spark and for execution animations. Our blade sits at the far end from the grip. |

**`Rotation`, `BladeStart` and `BladeEnd` are estimates and want one visual check.** They were
derived from our art's own geometry rather than measured against a swinging pawn, so the swing
may need the numbers nudged. Everything else is copied from a known-good entry.
