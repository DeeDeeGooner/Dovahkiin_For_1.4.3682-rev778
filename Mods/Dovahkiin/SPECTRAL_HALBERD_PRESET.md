# The spectral halberd — exact working preset

**Hand this whole file to the session that is struggling.** Every number here was read off disk,
not remembered. If a value here disagrees with what that session has, this file is right.

Committed at `da7498a` on branch `main`. Three files and one code block define the weapon
completely — nothing else touches it.

---

## 1. What it is meant to be

The Ancient Dragonborn's weapon. **Our own art** carrying the armour's blue-to-orange gradient,
but it **behaves, is held and animates exactly as Medieval Overhaul's halberd**
(`DankPyon_MeleeWeapon_Halberd`).

That split is deliberate and was the user's design call:

- **Art is ours** → full gradient control, identical for every player, no dependency.
- **Behaviour is borrowed** → stats, hold angle and Melee Animation data copied from their
  halberd, so it hits and swings like the weapon a Medieval Overhaul player already knows.

**There is exactly ONE axe def.** An earlier `MayRequire` variant that reused their texture was
deleted. Do not reintroduce it. If a session is fighting a fallback path or a runtime def lookup,
that is the deleted design and it should be removed.

---

## 2. Files — the complete set

| file | role |
|---|---|
| `Tools/GenerateAncientAxe.ps1` | generates the texture. Run it to rebuild the art. |
| `Textures/Things/Item/Equipment/DovahkiinAncientAxe.png` | the output, 256×256 |
| `Defs/ThingDefs_Misc/AncientDragonborn_Dovahkiin.xml` | the `ThingDef` |
| `WeaponTweakData/Dovahkiin_AncientDragonbornAxe_erzou.dovahkiin.json` | Melee Animation data |
| `Source/Dovahkiin/Thing_DragonAspectOverlay.cs` | draws it on the summon |

---

## 3. ThingDef — `Dovahkiin_AncientDragonbornAxe`

```xml
<label>spectral halberd</label>
<thingClass>ThingWithComps</thingClass>
<category>Item</category>
<equipmentType>Primary</equipmentType>
<altitudeLayer>Item</altitudeLayer>
<drawerType>MapMeshOnly</drawerType>
<tickerType>Never</tickerType>
<useHitPoints>false</useHitPoints>
<destroyOnDrop>true</destroyOnDrop>
<tradeability>None</tradeability>
<soundInteract>Standard_Pickup</soundInteract>
<comps>
  <li><compClass>CompEquippable</compClass></li>
</comps>
<graphicData>
  <texPath>Things/Item/Equipment/DovahkiinAncientAxe</texPath>
  <graphicClass>Graphic_Single</graphicClass>
  <drawSize>(1.5,1.5)</drawSize>
</graphicData>
<uiIconScale>1.2</uiIconScale>
<equippedAngleOffset>45</equippedAngleOffset>
<statBases><Mass>2.75</Mass><MarketValue>0</MarketValue></statBases>
<weaponClasses><li>Melee</li></weaponClasses>
<tools>
  <li><label>shaft</label><capacities><li>Blunt</li><li>Poke</li></capacities>
      <power>13</power><cooldownTime>2</cooldownTime></li>
  <li><label>blade</label><labelUsedInLogging>false</labelUsedInLogging>
      <capacities><li>Cut</li></capacities>
      <power>27</power><cooldownTime>2.9</cooldownTime><armorPenetration>0.3</armorPenetration></li>
</tools>
```

### Four of these are load-bearing and each one broke something

- **`CompEquippable` is REQUIRED.** Without it RimWorld logs *"is equipment but has no
  CompEquippable"* at load and `Pawn_EquipmentTracker.Notify_EquipmentAdded` throws a
  `NullReferenceException` the moment anything holds it. **That is what made the summon fail to
  appear at all in the first playtest.** There is no `CompProperties_Equippable` type — vanilla's
  `BaseWeapon` declares it as a plain comp carrying `compClass`, exactly as shown.
- **`destroyOnDrop` needs `tradeability` `None`**, or it is a config error on its own.
- **`equippedAngleOffset` 45** comes from `DankPyon_Base_Sharp_Oversize`, the halberd's *parent* —
  not from the halberd def itself. This is the hold orientation.
- **`drawSize` (1.5,1.5)** is the halberd's; the tweak data's `Scale` 1.5 must match it.

`soundInteract` is `Standard_Pickup` because **`Interact_BladelikeWeapon` does not exist** in Core
— an unrecognised sound defName is an XML error at load.

---

## 4. Melee Animation tweak data — copied verbatim from their halberd

`WeaponTweakData/Dovahkiin_AncientDragonbornAxe_erzou.dovahkiin.json`

```json
{
  "TextureModID": "erzou.dovahkiin",
  "ItemDefName": "Dovahkiin_AncientDragonbornAxe",
  "ItemType": "ThingDef",
  "ItemTypeNamespace": "Verse",
  "OffX": 0.542588949,
  "OffY": -0.005071081,
  "Rotation": 45.0,
  "ScaleX": 1.5,
  "ScaleY": 1.5,
  "BladeStart": 0.851915538,
  "BladeEnd": 1.52634883,
  "MeleeWeaponType": 7
}
```

- **`MeleeWeaponType` 7 is polearm.** 2 is two-handed axe and was wrong — it is a halberd.
- Filename must be `<ItemDefName>_<packageId>.json`.
- **A mod may ship its own `WeaponTweakData/` folder** — `XenotypeSatyr` does, which is what
  proves the convention. Do **not** write into Melee Animation's folder; its next update wipes it.
- **This is not a dependency.** No `MayRequire`, no assembly reference. Absent Melee Animation the
  folder is simply never read.

---

## 5. THE ART — orientation is the thing that breaks

**Their weapons run bottom-left → top-right, head at TOP-RIGHT.** Measured, opaque pixels per
quadrant:

| sprite | topLeft | topRight | botLeft | botRight |
|---|---|---|---|---|
| their halberd | 105 | **5583** | 3252 | 105 |
| ours (correct) | 45 | **4040** | 2039 | 58 |

**Every value in section 4 is expressed in their texture's frame.** If our sprite runs the other
diagonal, the pawn grips the weapon *by the blade*. Ours was mirrored once already for this reason.

**If the art is ever redrawn, re-run that quadrant count.** It is the only thing that silently
invalidates the whole tweak file.

### Geometry constants, exactly as they are in `GenerateAncientAxe.ps1`

```
$SIZE = 256        $SS = 4          # 4× supersample, downscaled at the end

$BUTT_X = 0.12   $BUTT_Y = 0.93     # butt, bottom-left
$HEAD_X = 0.82   $HEAD_Y = 0.14     # head, top-right

$HAFT_W_BUTT = 0.015               # near-parallel: theirs is a CONSTANT 9.9px half-width
$HAFT_W_HEAD = 0.018               # for 60% of its length. Taper was what made ours a wedge.

$C_LINE = @(14, 18, 28)            # dark keyline
$OUTLINE = 0.010                   # haft keyline margin
$BANDS = 26                        # gradient bands along the haft

DrawBit  1.0 0.175 0.132 $BLADE 250 2.0     # blade:  side, out, along, points, alpha, edgePx
DrawBit -1.0 0.076 0.055 $SPIKE 235 1.7     # counterweight spike

$SPEAR_LEN  = 0.175                # spear point above the head
$SPEAR_HALF = 0.015
# base at HEAD - ux*0.020, tip at HEAD + ux*$SPEAR_LEN
```

Blade and spike outlines, as `(outward, along-haft)` pairs — outward is perpendicular to the
haft, along-haft is positive toward the head:

```
$BLADE = @(          $SPIKE = @(
  @( 0.10,  0.42 ),    @( 0.10,  0.34 ),
  @( 0.50,  0.78 ),    @( 0.95,  0.55 ),
  @( 0.92,  1.00 ),    @( 1.16,  0.00 ),
  @( 1.10,  0.25 ),    @( 0.95, -0.55 ),
  @( 1.02, -0.55 ),    @( 0.10, -0.34 )
  @( 0.78, -1.05 ),  )
  @( 0.42, -0.70 ),
  @( 0.12, -0.40 )
)
```

### Five art rules, each learned by getting it wrong

1. **Along-haft extent must grow MONOTONICALLY with outward distance** — 0.42 → 0.78 → 1.00.
   Putting the widest points at mid-span makes a lozenge, and a lozenge with an outline renders
   as a hexagon. That happened twice.
2. **The beard needs a concave notch** (points 6–7 hooking back toward the haft). Without it the
   blade is just a longer triangle.
3. **A dark keyline is not optional.** RimWorld weapon sprites have one; without it a coloured
   shape on lit ground reads washed out however good the silhouette is.
4. **Scale the keyline with the shape.** A fixed 6px outline swallowed every facet once the head
   shrank to halberd proportions. It is now `max(2·SS, out · N · 0.055)`.
5. **A halberd needs its spear point.** Long and narrow, or it reads as a bead.

### Proportions, as fractions of weapon length

| | their halberd | ours |
|---|---|---|
| haft half-width | 0.029 | **0.0292** |
| head half-width | 0.137 | **0.1192** |

Measure along the **weapon axis**, not the bounding-box diagonal — the latter gives nonsense for a
sprite running the other way.

---

## 6. How it is drawn on the summon

**RimWorld will not draw it.** `PawnRenderer.DrawEquipment` gates on `CarryWeaponOpenly()`, which
is **false for an undrafted pawn** — and the summon is autonomous and never drafted, so his axe
would appear only mid-swing. `Thing_DragonAspectOverlay` draws it instead, which also keeps it off
the pawn render path entirely.

From `Thing_DragonAspectOverlay.DrawAt`, with `RefBodyWidth = 1.5f`:

```csharp
// North:  axePos.x -= 0.34f * scale / RefBodyWidth;  axeAngle = 205f;  y offset -0.006f
// West:   axePos.x -= 0.30f * scale / RefBodyWidth;  axeAngle = 200f;  y offset +0.006f
// else:   axePos.x += 0.34f * scale / RefBodyWidth;  axeAngle = 145f;  y offset +0.006f
axePos.z -= 0.06f * scale / RefBodyWidth;
DrawQuad(axeGraphic, axePos, axeDrawSize * scale / RefBodyWidth, axeAngle, false, Color.white, 1f);
```

`axeGraphic` and `axeDrawSize` are read **from the equipped ThingDef's own `graphicData`**, not
hardcoded — so the drawn axe and the carried one cannot diverge. A hardcoded draw size gave two
very different apparent sizes for two textures that fill their frames differently.

These offsets and angles are **eyeballed, not measured**, and are the one part of the weapon still
unverified in game. If the axe sits wrongly in his hand, this block is where to change it — not
the tweak data, not the def.

---

## 7. Two things NOT to do

- **Do not tint Medieval Overhaul's texture to get the gradient.** `CutoutComplex` tints through a
  mask; their greataxe mask marks 70% in RED, leaves the haft unmarked, and its GREEN channel is
  entirely empty so `colorTwo` does nothing. Plain `Cutout` multiplies everything but **multiply
  cannot brighten**, so a dark haft goes stained-dark, never luminous. Editing their PNG is
  redistributing their art.
- **Do not add a `MayRequire` variant or a `DefOf` entry for a conditional def.** A `[DefOf]` field
  for a `MayRequire` def logs a red error on every startup for anyone without that mod.

---

## 8. Rebuild and verify

```
powershell -File "Mods\Dovahkiin\Tools\GenerateAncientAxe.ps1"
dotnet build "Mods\Dovahkiin\Source\Dovahkiin\Dovahkiin.csproj" -c Release --nologo -v minimal
```

Then, in order: **read the dev-mode log for config errors** — a clean build and "every def parses"
both pass a def that names a non-existent type or field, because parsing proves well-formedness,
not validity. RimWorld's own config check is the pass that catches it, and it says so in plain
English at load.
