// Implements: SPEC.md 4.6 (a dragon's shout is no longer the Dovahkiin's) and 4.6a (the
// breath's geometry depends on the movement state).
//
// ============================================================================================
// WHY THIS IS NOT A TOOL, A VERB, OR A SHOUT WAVE
// ============================================================================================
// THE USER, 2026-08-05, AND IT IS THE GOVERNING CONSTRAINT: "I dont want to risk the dovahs to
// breath multiple times during a grounded period... compared to the bite and shake, their
// breaths aren't just a random melee or ranged roll, it's an ACTIVE SKILL. Their breathing
// should be used exactly as in the patterns (which are rolled: 1 or 2 or 3 or 4)."
//
// So there is deliberately NO Verb and NO <tools> entry for the breath. If it were either,
// RimWorld's own combat AI would pick it up and fire it whenever it felt like it - which is
// precisely the "several breaths in one grounded period" being ruled out. The ONLY way to start
// a breath is a code call to Spawn(), which will come from the attack-pattern executor.
//
// It is also NOT built out of Thing_ShoutWave. Decided 2026-08-04 and recorded in the notebook:
// an emitter firing ~15 overlapping ShoutWaves was proposed and rejected, because the
// performance worry is legitimate (15 Things each drawing per frame, with RocketMan installed)
// AND soar's circle-plus-cosmetic-cone cannot be expressed as repeated cone waves at all - the
// hack would be written now and thrown away the moment soar arrived.
//
// ============================================================================================
// GRADUAL DAMAGE IS THE WHOLE POINT (SPEC.md 4.6)
// ============================================================================================
// "+100% damage but delivered GRADUALLY over time of exposure." Thing_ShoutWave applies its
// payload the instant its front arrives, which is right for a shout and wrong for a breath.
// Here the breath LINGERS and pulses: each pulse applies one share of the total to whoever is
// standing in it AT THAT MOMENT. Walk out after one pulse and you take one share; stand in it
// and you take all of them. Exposure IS the damage model, not a flavour word.
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Dovahkiin
{
    /// <summary>
    /// Which shape the breath takes. SPEC.md 4.6a - this depends on the dragon's movement state,
    /// and that is the point rather than a complication: a ground-swept cone from a creature
    /// thirty feet up makes no sense, and the two states get genuinely different threat shapes.
    /// </summary>
    public enum DragonBreathShape
    {
        /// <summary>GROUNDED. A damaging cone along the ground, from the mouth outward.</summary>
        Cone,

        /// <summary>
        /// SOAR. Damage in a CIRCLE at the impact point, plus a purely VISUAL cone stretching
        /// from the dragon down to reach it. The cone does not damage - only the circle does.
        /// </summary>
        Pool
    }

    public class Thing_DragonBreath : Thing
    {
        // ---- geometry ----
        private IntVec3 origin;          // the mouth
        private IntVec3 impact;          // aimed cell; for Pool this is the circle's centre
        private DragonBreathShape shape = DragonBreathShape.Cone;
        private float range = 16f;
        private float coneAngle = 38f;
        private float poolRadius = 3.5f;

        // ---- lifetime and pulses ----
        private int durationTicks = 180;
        private int pulseIntervalTicks = 20;
        private int age;
        private int pulsesFired;
        private int totalPulses = 1;

        // ---- payload ----
        private Pawn instigator;
        private DamageDef damageDef;
        private float totalDamage;
        private int damageInstances = 4;
        private float armorPenetration;
        private bool igniteGround;
        private float snowDepth;
        private HediffDef appliedHediff;
        private float appliedHediffSeverity = 1f;

        // ---- look ----
        private FleckDef fleckDef;
        private float fleckScale = 1.4f;
        private Color tint = new Color(1f, 0.6f, 0.25f);

        /// <summary>The roar, played once as the breath begins. Null is silent, not an error.</summary>
        private SoundDef breathSound;

        /// <summary>
        /// THE FILL. A flat translucent wash covering the whole shape, under the flecks.
        ///
        /// The user, 2026-08-05: "there should be a whole layer of color filling the whole
        /// circle, a transparent darker orange" - and the same for the cone. Particles alone,
        /// however many, always read as scattered; what makes a breath look like a VOLUME is a
        /// continuous body of colour with the particles on top of it.
        ///
        /// Alpha is animated over the breath's life, so this is deliberately NOT run through
        /// SolidColorMaterials' cache: that dictionary is keyed by COLOUR, and a colour that
        /// changes every frame would grow it without bound. One material per breath, mutated in
        /// place (free - no allocation), destroyed with the Thing.
        /// </summary>
        private Color fillColor = new Color(0.72f, 0.26f, 0.05f, 0.34f);
        private bool fillColorSet;

        /// <summary>
        /// The LIGHT end of the fill gradient - at the mouth, at the start of the breath.
        /// The user, 2026-08-05: "it should be a clearer orange than the particles in the
        /// begining". Deliberately paler than the fleck tint (1.00, 0.62, 0.24).
        /// </summary>
        private Color fillBright = new Color(1f, 0.76f, 0.40f);

        /// <summary>
        /// The hue the NEAR end of the breath is biased toward - "the yellowness of the fire".
        /// Strongest at the mouth, gone by the far end, so a jet is white-hot where it leaves him
        /// and settles to its own colour by the time it lands.
        /// </summary>
        private Color fillYellow = new Color(1f, 0.93f, 0.35f);

        /// <summary>How far toward <see cref="fillYellow"/> the mouth goes. 0 disables the ramp.</summary>
        private float yellowStrength = 0.75f;

        /// <summary>
        /// SOAR ONLY: how much less transparent the damaging circle is than the reaching cone.
        /// A multiplier on its alpha. See DovahkiinTuningDef.dragonBreathSoarCircleOpacity for
        /// why this is an OPACITY lever and not a brightness one.
        /// </summary>
        private float soarCircleOpacity = 1.4f;

        [Unsaved(false)] private Mesh[] fillBands;
        [Unsaved(false)] private Mesh[] reachBands;
        [Unsaved(false)] private Material[] fillMats;
        [Unsaved(false)] private Material[] reachMats;

        /// <summary>
        /// How many distance bands the fill is cut into. The gradient has to be built from
        /// separate meshes because ONE Graphics.DrawMesh call takes ONE material colour - and
        /// mutating a shared material between queued draws does not work, since DrawMesh
        /// references the material rather than copying it, so every band would render with
        /// whichever colour was set last.
        ///
        /// Eight is smooth enough at play distance and is still eight draw calls against the
        /// ~190 a per-cell fill would cost.
        /// </summary>
        private const int GradientBands = 8;

        /// <summary>The damaging cells, computed once on spawn. Never recomputed per tick.</summary>
        private List<IntVec3> damagingCells;

        /// <summary>
        /// The same cells as a set, for O(1) membership. Pulse walks the MAP'S PAWNS and asks
        /// "are you in me", rather than walking a few hundred cells asking "is anyone here" -
        /// there are tens of pawns and hundreds of cells, so the test is the cheap way round.
        /// </summary>
        private HashSet<IntVec3> damagingSet;

        /// <summary>
        /// Cosmetic-only cells - Pool's reaching cone. Drawn, never struck. Kept separate from
        /// damagingCells so "what is drawn" and "what is hit" can never drift into each other,
        /// which is the whole reason soar's shape needed its own class.
        /// </summary>
        private List<IntVec3> cosmeticCells;

        /// <summary>
        /// Start a breath. THE ONLY ENTRY POINT - there is no verb and no tool, by design; see
        /// the header. The attack-pattern executor is what will call this.
        /// </summary>
        public static Thing_DragonBreath Spawn(Pawn dragon, IntVec3 target, DragonBreathShape shape,
            float range, float coneAngle, float poolRadius, int durationTicks, int pulseIntervalTicks)
        {
            if (dragon == null || !dragon.Spawned || dragon.Map == null)
            {
                return null;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Dovahkiin_DragonBreath");
            if (def == null)
            {
                // GetNamedSilentFail returns null with no message. Without this line a def that
                // failed to load would present as "the breath does nothing", which is
                // indistinguishable from the pattern never calling it.
                Log.WarningOnce("[Dovahkiin] Dovahkiin_DragonBreath ThingDef missing - "
                    + "dragons cannot breathe.", 0x5A1D03);
                return null;
            }

            Thing_DragonBreath breath = (Thing_DragonBreath)ThingMaker.MakeThing(def);
            breath.instigator = dragon;
            breath.origin = dragon.Position;
            breath.impact = target;
            breath.shape = shape;
            breath.range = Mathf.Max(1f, range);
            breath.coneAngle = Mathf.Clamp(coneAngle, 1f, 360f);
            breath.poolRadius = Mathf.Max(0.5f, poolRadius);
            breath.durationTicks = Mathf.Max(1, durationTicks);
            breath.pulseIntervalTicks = Mathf.Max(1, pulseIntervalTicks);
            // Derived, never given its own field. Two numbers that must agree and are stored
            // apart is this project's most repeated bug - see the landing stun.
            breath.totalPulses = Mathf.Max(1, breath.durationTicks / breath.pulseIntervalTicks);

            GenSpawn.Spawn(breath, dragon.Position, dragon.Map);
            return breath;
        }

        /// <summary>
        /// What the breath does to whoever stands in it. Set immediately after Spawn, before the
        /// first tick. Split from Spawn so the geometry and the payload can be read separately -
        /// the pattern chooses the shape, the dragon's kind chooses the damage.
        /// </summary>
        public void SetPayload(DamageDef damageDef, float totalDamage, int damageInstances,
            float armorPenetration, bool igniteGround, float snowDepth,
            HediffDef appliedHediff, float appliedHediffSeverity)
        {
            this.damageDef = damageDef;
            this.totalDamage = totalDamage;
            this.damageInstances = Mathf.Max(1, damageInstances);
            this.armorPenetration = armorPenetration;
            this.igniteGround = igniteGround;
            this.snowDepth = snowDepth;
            this.appliedHediff = appliedHediff;
            this.appliedHediffSeverity = appliedHediffSeverity;
        }

        public void SetLook(FleckDef fleckDef, float fleckScale, Color tint)
        {
            this.fleckDef = fleckDef;
            this.fleckScale = fleckScale;
            this.tint = tint;
        }

        /// <summary>
        /// The roar. Played ONCE, on the breath's first tick - see the note there for why not in
        /// SpawnSetup.
        /// </summary>
        public void SetSound(SoundDef sound)
        {
            breathSound = sound;
        }

        /// <summary>
        /// The flat wash filling the shape. Leave it unset and it is DERIVED from the fleck tint -
        /// darker and translucent - so a frost breath gets a cold fill without anyone having to
        /// remember to set one. Same reasoning as deriving the pulse count: a second colour that
        /// must agree with the first is a colour that will one day disagree.
        /// </summary>
        public void SetFill(Color fill)
        {
            fillColor = fill;
            fillColorSet = true;
        }

        /// <summary>
        /// The whole fill gradient in one call: base colour, the pale end at the mouth, the hue
        /// the near end is biased toward, and how strongly. All four live in DovahkiinTuningDef
        /// so they can be retuned without a rebuild.
        /// </summary>
        public void SetFillGradient(Color fill, Color bright, Color yellow, float strength,
            float soarCircleOpacity)
        {
            SetFill(fill);
            fillBright = bright;
            fillYellow = yellow;
            yellowStrength = Mathf.Clamp01(strength);
            this.soarCircleOpacity = Mathf.Max(0f, soarCircleOpacity);
        }

        private Color FillColorFor()
        {
            if (fillColorSet)
            {
                return fillColor;
            }
            Color derived = tint * 0.72f;
            derived.a = 0.34f;
            return derived;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            BuildCells();
            // Built once here, alongside the cells they are made from - never per frame.
            // ONE distance scale for both sets: for a cone that is simply its range, and for a
            // soaring breath it runs from the mouth out past the far edge of the circle, which is
            // what puts the reaching cone in the light bands and the circle in the dark ones.
            float maxDist = shape == DragonBreathShape.Cone
                ? range
                : origin.DistanceTo(impact) + poolRadius;
            fillBands = BuildBandedMeshes(damagingCells, origin, maxDist);
            reachBands = BuildBandedMeshes(cosmeticCells, origin, maxDist);
        }

        /// <summary>
        /// Work out the cells once. Both shapes are built here so the difference between them is
        /// visible in one place rather than spread through the tick.
        /// </summary>
        private void BuildCells()
        {
            damagingCells = new List<IntVec3>();
            cosmeticCells = new List<IntVec3>();
            damagingSet = new HashSet<IntVec3>();
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 originV = origin.ToVector3Shifted();
            Vector3 facing = impact.ToVector3Shifted() - originV;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f)
            {
                facing = Vector3.forward;
            }
            facing.Normalize();

            if (shape == DragonBreathShape.Cone)
            {
                // GROUNDED: a cone swept along the ground from the mouth. Same geometry the
                // shout wave uses, so the two read as the same family of effect - but held open
                // for the whole duration instead of travelling.
                float half = coneAngle * 0.5f;
                foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, range, true))
                {
                    if (!c.InBounds(map) || c == origin)
                    {
                        continue;
                    }
                    Vector3 to = c.ToVector3Shifted() - originV;
                    to.y = 0f;
                    if (to.sqrMagnitude < 0.01f)
                    {
                        continue;
                    }
                    if (Vector3.Angle(facing, to.normalized) > half)
                    {
                        continue;
                    }
                    // ⚠⚠ FIRE DOES NOT GO THROUGH A WALL. The user, 2026-08-13: "obstacles like
                    // remparts and walls blocks the breathing from going beyond them."
                    //
                    // Without this the cone was pure geometry - a wedge of cells that never asked
                    // whether he could SEE any of them, so a jet cooked a room through its own
                    // curtain wall.
                    //
                    // ⚠ USE THE ENGINE'S OWN LINE OF SIGHT; DO NOT WRITE ONE. `GenSight.LineOfSight`
                    // walks the Bresenham line and stops at anything that fails `CanBeSeenOverFast`
                    // - which IS the distinction the user described. A WALL blocks completely,
                    // while a TREE only shadows the cell or two directly behind it, because the
                    // line to cells beside it is still clear. That falls out of per-cell LOS for
                    // free; hand-rolling "how many cells does a tree shadow" would be inventing an
                    // approximation of something vanilla already computes exactly.
                    //
                    // skipFirstCell: his own cell can never obstruct his own breath, and he is
                    // very often standing right against the wall he is breathing over.
                    if (!GenSight.LineOfSight(origin, c, map, true))
                    {
                        continue;
                    }
                    damagingCells.Add(c);
                    damagingSet.Add(c);
                }
                return;
            }

            // SOAR: the damage is a CIRCLE where the breath lands. The cone from the dragon down
            // to it is drawn and nothing more - it is what makes the circle read as coming from
            // him rather than appearing out of nowhere.
            foreach (IntVec3 c in GenRadial.RadialCellsAround(impact, poolRadius, true))
            {
                if (c.InBounds(map))
                {
                    damagingCells.Add(c);
                    damagingSet.Add(c);
                }
            }
            float reach = origin.DistanceTo(impact);
            if (reach > 1f)
            {
                float halfVisual = Mathf.Clamp(coneAngle * 0.5f, 4f, 40f);
                foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, reach, true))
                {
                    if (!c.InBounds(map) || c == origin || damagingSet.Contains(c))
                    {
                        continue;
                    }
                    Vector3 to = c.ToVector3Shifted() - originV;
                    to.y = 0f;
                    if (to.sqrMagnitude < 0.01f || Vector3.Angle(facing, to.normalized) > halfVisual)
                    {
                        continue;
                    }
                    cosmeticCells.Add(c);
                }
            }
        }

        public override void Tick()
        {
            age++;
            if (damagingCells == null || age > durationTicks)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            // The instigator dying mid-breath does not cancel it - fire already in the air still
            // burns. But a despawned map would.
            if (Map == null)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            // THE ROAR, on the first tick rather than in SpawnSetup.
            //
            // ⚠ ORDERING, AND IT WOULD HAVE BEEN A SILENT FAILURE: SpawnSetup runs INSIDE
            // GenSpawn.Spawn, which happens inside our own Spawn() - so it fires BEFORE the
            // caller has had a chance to call SetSound. Playing it there would mean breathSound
            // is always null and the breath is always silent, with nothing to see in the log.
            // The first tick is after every setter, whatever order the caller uses them in.
            //
            // Positional, not on-camera: this def is context MapOnly with a distRange, so it is
            // heard from where the breath is. The notebook's rule is the reverse case - a sound
            // authored onCamera MUST use PlayOneShotOnCamera or it is inaudible off its own tile.
            if (age == 1 && breathSound != null)
            {
                breathSound.PlayOneShot(new TargetInfo(Position, Map));
            }

            EmitFlecks();

            if (age % pulseIntervalTicks == 0 && pulsesFired < totalPulses)
            {
                pulsesFired++;
                Pulse();
            }
        }

        /// <summary>
        /// One share of the total, to whoever is standing in it RIGHT NOW. This is what "delivered
        /// gradually over time of exposure" means mechanically: the breath does not hit a list of
        /// victims chosen when it started, it hits whoever is in it each time it pulses.
        /// </summary>
        private void Pulse()
        {
            Map map = Map;
            float perPulse = totalDamage / totalPulses;

            // ⚠ WALK THE PAWNS, NOT THE CELLS. A 24-range 38-degree cone is several hundred
            // cells; a map has tens of pawns. Asking each pawn "am I in the breath" against a
            // HashSet is O(pawns); asking each cell "is anyone here" is O(cells) and allocates a
            // thing list per cell. Same answer, an order of magnitude cheaper, and this runs on
            // a tick path with RocketMan installed.
            List<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                Pawn p = pawns[i];
                // NO alreadyHit SET, DELIBERATELY - unlike the shout wave. Being hit again on
                // the next pulse is the entire mechanic; deduplicating would turn the breath
                // back into a single instant hit wearing a duration.
                if (p == null || p == instigator || p.Dead || !damagingSet.Contains(p.Position))
                {
                    continue;
                }
                {
                    if (appliedHediff != null && p.health != null)
                    {
                        Hediff prior = p.health.hediffSet.GetFirstHediffOfDef(appliedHediff);
                        if (prior != null)
                        {
                            prior.Severity = Mathf.Max(prior.Severity, appliedHediffSeverity);
                        }
                        else
                        {
                            p.health.AddHediff(appliedHediff).Severity = appliedHediffSeverity;
                        }
                    }

                    if (damageDef == null || perPulse <= 0f)
                    {
                        continue;
                    }
                    float per = perPulse / damageInstances;
                    for (int d = 0; d < damageInstances && !p.Destroyed && !p.Dead; d++)
                    {
                        // Spread across core and head rather than crushing toes - the same shared
                        // rule the Dovahkiin's breaths use.
                        BodyPartRecord part = DovahkiinDamageUtility.SelectSpreadTarget(p);
                        p.TakeDamage(new DamageInfo(
                            damageDef, per, armorPenetration, -1f, instigator, part));
                    }
                }
            }

            ScorchSample(map);
        }

        /// <summary>
        /// Fire and snow, on a SAMPLE of the cells rather than all of them.
        ///
        /// Two reasons, and the second is the one that matters. Walking every cell of a large
        /// cone on every pulse is the per-tick cost this class exists to avoid. And at a 0.25
        /// ignition chance per cell per pulse, nine pulses would light very nearly the ENTIRE
        /// cone - a dragon's breath should scorch ground, not reliably set fire to every square
        /// it touches. Sampling gives a scatter of burning patches, which is what it should look
        /// like anyway.
        /// </summary>
        private void ScorchSample(Map map)
        {
            if (!igniteGround && snowDepth <= 0f)
            {
                return;
            }
            int sample = Mathf.Clamp(damagingCells.Count / 6, 1, 24);
            for (int i = 0; i < sample; i++)
            {
                // Ignition and snow go through the SHARED helper the Dovahkiin's own shouts use.
                // Rewriting them here would leave burned cells and snowy patches subtly different
                // from hers - exactly what the notebook warned about when this class was designed.
                DovahkiinStrike.ScorchCell(damagingCells[Rand.Range(0, damagingCells.Count)],
                    map, igniteGround, snowDepth);
            }
        }

        /// <summary>
        /// The look. Flecks rather than drawn quads: the mod's shouts already read this way, and
        /// a Thing that emits flecks needs no DrawAt override, no drawOffscreen, and nothing on
        /// the render path at all - which matters with RocketMan installed.
        /// </summary>
        private void EmitFlecks()
        {
            if (fleckDef == null || Map == null || age % FleckIntervalTicks != 0)
            {
                return;
            }
            // Density falls off over the breath's life so it visibly gutters out rather than
            // stopping dead on the last tick.
            float life = Mathf.Clamp01(0.35f + (1f - ((float)age / durationTicks)));

            // TWO LAYERS, and the dim one is what makes it read as a MASS rather than confetti.
            // The user, 2026-08-05: "the breath looks esthetically incomplete/holed, there should
            // be another layer of vfx or effect that makes it whole."
            //
            // They are right, and the cause was MY OWN performance fix: a fixed 14 flecks
            // scattered over a cone of several hundred cells is necessarily sparse, and the
            // bigger the cone the sparser it gets. The answer is NOT simply more flecks - it is
            // BIGGER, OVERLAPPING, DIMMER ones underneath. A wide soft wash fills the volume; the
            // bright sparse layer on top gives it detail. Same trick as the aura's underglow
            // bands, which the notebook already records as the thing that stopped it reading flat.
            int core = SampleCount(damagingCells, CoreDensity, 8, 34);
            int wash = SampleCount(damagingCells, WashDensity, 5, 20);
            EmitSample(damagingCells, Mathf.RoundToInt(wash * life), fleckScale * 2.1f, 0.30f);
            EmitSample(damagingCells, Mathf.RoundToInt(core * life), fleckScale, 1f);

            int reach = SampleCount(cosmeticCells, WashDensity, 3, 14);
            EmitSample(cosmeticCells, Mathf.RoundToInt(reach * life), fleckScale * 1.5f, 0.22f);
            EmitSample(cosmeticCells, Mathf.RoundToInt(reach * 0.6f * life), fleckScale * 0.7f, 0.5f);
        }

        /// <summary>
        /// How many flecks for a region of this size. Grows as the SQUARE ROOT of the cell count,
        /// so a big breath is denser than a small one without the cost growing with its area -
        /// a flat count made large cones look moth-eaten, and a linear count would put the
        /// per-tick cost straight back.
        /// </summary>
        private static int SampleCount(List<IntVec3> cells, float density, int min, int max)
        {
            if (cells == null || cells.Count == 0)
            {
                return 0;
            }
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(cells.Count) * density), min, max);
        }

        // ⚠ THE PERFORMANCE SHAPE OF THIS CLASS, AND IT IS DELIBERATE.
        //
        // The user, 2026-08-05: "reusing the same wave mechanics used by the dragonborn would be
        // a great performance risk in the case of how dragon's breaths behave (overtime)". They
        // are right, and the first draft of this method had exactly that fault - it walked EVERY
        // cell of the cone EVERY tick rolling Rand.Chance, which on a 24-range cone is several
        // hundred cells sixty times a second, on a tick path, with RocketMan installed.
        //
        // So: emit on an INTERVAL, and emit a FIXED COUNT of flecks at randomly chosen cells
        // rather than testing every cell. Cost is now O(flecks) and independent of how big the
        // breath is - a huge cone costs exactly what a small one does. It also looks the same,
        // because what the eye reads is the density of flecks, not which cells were considered.
        private const int FleckIntervalTicks = 3;
        private const float CoreDensity = 1.5f;
        private const float WashDensity = 0.85f;

        private void EmitSample(List<IntVec3> cells, int count, float scale, float alpha)
        {
            if (cells == null || cells.Count == 0 || count <= 0)
            {
                return;
            }
            Color c = tint;
            c.a *= alpha;
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = cells[Rand.Range(0, cells.Count)];
                // ⚠ SCALE WITH DISTANCE FROM THE MOUTH. A cone WIDENS, so the same fleck size
                // that packs the throat leaves gaps at the far end - which is most of what read
                // as "holed", since most of a cone's cells are out at the wide end. Growing the
                // fleck with its distance keeps the coverage even all the way down, and it is
                // also what a jet of flame does as it spreads.
                float spread = 1f;
                if (range > 1f)
                {
                    spread = Mathf.Lerp(0.75f, 1.8f,
                        Mathf.Clamp01(origin.DistanceTo(cell) / range));
                }
                Vector3 pos = cell.ToVector3Shifted()
                    + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.45f, 0.45f));
                FleckCreationData data = FleckMaker.GetDataStatic(pos, Map, fleckDef, scale * spread);
                data.instanceColor = c;
                data.rotationRate = Rand.Range(-30f, 30f);
                Map.flecks.CreateFleck(data);
            }
        }

        // ============================================================================
        // THE FILL LAYER
        // ============================================================================
        // ONE MESH covering every cell, drawn in ONE call per frame. The obvious alternative -
        // a quad per cell - is ~190 draw calls a frame for a full cone, sixty times a second,
        // with RocketMan installed. A combined mesh costs the same whether the breath covers
        // four cells or four hundred, which is the same rule the fleck sampling follows.
        //
        // This is the project's established route for a Thing that draws its own geometry
        // (Thing_DragonAspectOverlay and Thing_ValorPortal both do it) - NOT a pawn render patch,
        // and nothing on the pawn render path.

        /// <summary>Drawn under pawns, so a victim is visibly standing IN the fire, not behind it.</summary>
        private static readonly float FillAltitude = AltitudeLayer.MoteLow.AltitudeFor();

        /// <summary>
        /// The fill colour at a point along the breath, at a moment in its life.
        ///
        /// THE USER'S SPEC, 2026-08-05, and both halves matter:
        ///   SPATIAL - "the tip of the cone starts darker, going clearer the closer it is to the
        ///             dragon", and for soar "the cone is clearer at start compared to the
        ///             circle". Both are the SAME rule once measured from the mouth: light near,
        ///             dark far. The soaring cone lies between him and the circle, so banding by
        ///             distance gives that for free rather than needing a second rule.
        ///   TEMPORAL - "starts as a gradient, and then is grading down to darker again."
        ///
        /// At <paramref name="spatialT"/> 0 / time 0 this is the pale end; at the far tip at time
        /// 0 it is the base colour; by time 1 everything has settled to the dark floor.
        ///
        /// ⚠ THE FLOOR IS THE USER'S CONSTRAINT, NOT A TASTE CALL: "the darkest color used should
        /// be at most a 20% darker of the color used rightnow." So the darkest value this can
        /// ever return is base x 0.8, and nothing may push past it.
        /// </summary>
        private Color FillColorAt(float spatialT, float timeT, float yellowScale)
        {
            Color baseFill = FillColorFor();
            Color dark = baseFill * DarkFloor;
            dark.a = baseFill.a;

            Color bright = fillBright;
            bright.a = baseFill.a;

            // 1. LIGHTNESS - pale at the mouth, base colour at the far end.
            Color c = Color.Lerp(bright, baseFill, Mathf.Clamp01(spatialT));

            // 2. YELLOWNESS - strongest at the mouth, gone by the far end. Applied BEFORE the
            //    temporal ramp on purpose, so the fade to dark washes it out along with
            //    everything else and the floor below still binds.
            float yellowT = yellowStrength * yellowScale * (1f - Mathf.Clamp01(spatialT));
            if (yellowT > 0f)
            {
                Color yellow = fillYellow;
                yellow.a = baseFill.a;
                c = Color.Lerp(c, yellow, Mathf.Clamp01(yellowT));
            }

            // 3. TEMPORAL - everything settles to the floor as the breath burns out.
            c = Color.Lerp(c, dark, Mathf.Clamp01(timeT));
            c.a = baseFill.a;
            return c;
        }

        /// <summary>base x 0.8 - the user's "at most 20% darker than the colour used right now".</summary>
        private const float DarkFloor = 0.8f;

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // NO base call. Thing.DrawAt would try to draw a graphic this def does not have.
            if (fillBands == null && reachBands == null)
            {
                return;
            }
            // Fade in fast, hold, then gutter out - so it does not pop on or vanish on the last
            // tick. This is OPACITY only; the colour gradient is a separate axis.
            float t01 = Mathf.Clamp01((float)age / durationTicks);
            float envelope = Mathf.Min(Mathf.Clamp01(age / 8f), Mathf.Clamp01((1f - t01) * 3.2f));
            if (envelope <= 0.01f)
            {
                return;
            }

            // ⚠ WHO GETS THE YELLOW, AND WHY IT IS AN EXCLUSION RATHER THAN A RAMP.
            //
            // The user, 2026-08-06: "Soar: yellower cone, UNTOUCHED yellowness for the circle."
            // The circle sits at the far end of the distance ramp, so it would already take only
            // a little yellow - but "a little" is not "untouched". A soaring breath's damaging
            // circle is therefore excluded outright (scale 0), while a grounded cone - which IS
            // the fillBands - gets the full ramp.
            bool grounded = shape == DragonBreathShape.Cone;
            float bodyYellow = grounded ? 1f : 0f;

            // ⚠ AND THE CIRCLE MUST STAND OUT FROM THE CONE - VIA OPACITY, NOT BRIGHTNESS.
            //
            // The user asked on 2026-08-06 for the circle to lead ("it should be looking that way
            // not the other (my initial approach was wrong)"), reversing their own request of the
            // day before that the cone be the lighter one. That reversal STANDS - the circle is
            // where the damage lands, so it should be what the eye goes to.
            //
            // What changed is the MECHANISM. An RGB brightness multiplier was tried first and
            // rejected in play - "now it feels like the cone and circle has the same color" - and
            // the reason is measurable: red was already saturated at 1.0 across every circle
            // band, so multiplying could only lift green and blue, walking the circle's hue
            // TOWARD the cone's yellow-orange. It made them converge. Opacity has no such ceiling
            // and changes weight without touching hue.
            float bodyAlpha = grounded ? 1f : soarCircleOpacity;
            DrawBands(fillBands, ref fillMats, t01, envelope, bodyAlpha, bodyYellow);

            // The reaching cone stays the fainter of the two - it is cosmetic and must never read
            // as a second damaging area. It is also the part asked to be yellower, and being
            // nearest the mouth it takes the strongest end of that ramp for free.
            DrawBands(reachBands, ref reachMats, t01, envelope, 0.6f, 1f);
        }

        private void DrawBands(Mesh[] bands, ref Material[] mats, float timeT, float envelope,
            float alphaScale, float yellowScale)
        {
            if (bands == null)
            {
                return;
            }
            if (mats == null)
            {
                mats = new Material[bands.Length];
            }
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i] == null)
                {
                    continue;
                }
                // Band centre, so the first band is not pinned to pure white and the last not to
                // pure floor - the ramp reads smoother across only eight steps.
                float spatialT = (i + 0.5f) / bands.Length;
                Color c = FillColorAt(spatialT, timeT, yellowScale);
                // Clamped: alphaScale may be above 1 (the soaring circle is deliberately less
                // transparent than everything else), and an alpha over 1 is not simply ignored.
                c.a = Mathf.Clamp01(c.a * envelope * alphaScale);

                if (mats[i] == null)
                {
                    // Transparent, NOT MoteGlow: MoteGlow is additive and would BRIGHTEN the
                    // ground, and this is a tint, not a glow. One material PER BAND, because
                    // Graphics.DrawMesh references the material rather than copying it - a single
                    // shared material mutated between calls renders every band the same colour.
                    mats[i] = SolidColorMaterials.NewSolidColorMaterial(c, ShaderDatabase.Transparent);
                    if (mats[i] == null)
                    {
                        continue;
                    }
                }
                mats[i].color = c;
                // Vertices already carry world positions, so no transform is needed here.
                Graphics.DrawMesh(bands[i], Vector3.zero, Quaternion.identity, mats[i], 0);
            }
        }

        /// <summary>
        /// One mesh, two triangles per cell, built in WORLD coordinates so it can be drawn with
        /// no transform. A few hundred quads is nothing for the GPU; the point is that it is ONE
        /// draw call rather than one per cell.
        /// </summary>
        /// <summary>
        /// Cut the cells into distance bands from the mouth and build one mesh per band, so each
        /// can be drawn in its own colour. maxDist is shared between the damaging and cosmetic
        /// sets on purpose: it is what makes a soaring breath's reaching cone come out lighter
        /// than the circle it feeds, with no second rule.
        /// </summary>
        private static Mesh[] BuildBandedMeshes(List<IntVec3> cells, IntVec3 from, float maxDist)
        {
            if (cells == null || cells.Count == 0)
            {
                return null;
            }
            if (maxDist < 1f)
            {
                maxDist = 1f;
            }
            List<IntVec3>[] buckets = new List<IntVec3>[GradientBands];
            for (int i = 0; i < GradientBands; i++)
            {
                buckets[i] = new List<IntVec3>();
            }
            for (int i = 0; i < cells.Count; i++)
            {
                float d = from.DistanceTo(cells[i]) / maxDist;
                int band = Mathf.Clamp((int)(d * GradientBands), 0, GradientBands - 1);
                buckets[band].Add(cells[i]);
            }
            Mesh[] meshes = new Mesh[GradientBands];
            for (int i = 0; i < GradientBands; i++)
            {
                meshes[i] = BuildFillMesh(buckets[i]);
            }
            return meshes;
        }

        private static Mesh BuildFillMesh(List<IntVec3> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return null;
            }
            Vector3[] verts = new Vector3[cells.Count * 4];
            Vector2[] uvs = new Vector2[cells.Count * 4];
            int[] tris = new int[cells.Count * 6];
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                int v = i * 4;
                verts[v] = new Vector3(cell.x, FillAltitude, cell.z);
                verts[v + 1] = new Vector3(cell.x, FillAltitude, cell.z + 1);
                verts[v + 2] = new Vector3(cell.x + 1, FillAltitude, cell.z + 1);
                verts[v + 3] = new Vector3(cell.x + 1, FillAltitude, cell.z);
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);
                int t = i * 6;
                tris[t] = v;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 2;
                tris[t + 3] = v;
                tris[t + 4] = v + 2;
                tris[t + 5] = v + 3;
            }
            Mesh m = new Mesh();
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            return m;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // ⚠ MESHES AND MATERIALS ARE UNITY OBJECTS AND ARE NOT GARBAGE COLLECTED. One breath
            // leaking three of them is invisible; a fight full of breaths is a slow GPU memory
            // leak. The notebook already records this for the Thu'um bar's cached textures.
            //
            // Note the name collision: inside a Thing, an unqualified Destroy() is
            // Thing.Destroy(DestroyMode). These must be fully qualified.
            DisposeAll(ref fillBands, ref fillMats);
            DisposeAll(ref reachBands, ref reachMats);
            base.Destroy(mode);
        }

        private static void DisposeAll(ref Mesh[] meshes, ref Material[] mats)
        {
            if (meshes != null)
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] != null) { UnityEngine.Object.Destroy(meshes[i]); }
                }
                meshes = null;
            }
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) { UnityEngine.Object.Destroy(mats[i]); }
                }
                mats = null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref origin, "dbOrigin");
            Scribe_Values.Look(ref impact, "dbImpact");
            Scribe_Values.Look(ref shape, "dbShape", DragonBreathShape.Cone);
            Scribe_Values.Look(ref range, "dbRange", 16f);
            Scribe_Values.Look(ref coneAngle, "dbConeAngle", 38f);
            Scribe_Values.Look(ref poolRadius, "dbPoolRadius", 3.5f);
            Scribe_Values.Look(ref durationTicks, "dbDuration", 180);
            Scribe_Values.Look(ref pulseIntervalTicks, "dbPulseInterval", 20);
            Scribe_Values.Look(ref age, "dbAge", 0);
            Scribe_Values.Look(ref pulsesFired, "dbPulsesFired", 0);
            Scribe_Values.Look(ref totalPulses, "dbTotalPulses", 1);
            Scribe_References.Look(ref instigator, "dbInstigator");
            Scribe_Defs.Look(ref damageDef, "dbDamageDef");
            Scribe_Values.Look(ref totalDamage, "dbTotalDamage", 0f);
            Scribe_Values.Look(ref damageInstances, "dbDamageInstances", 4);
            Scribe_Values.Look(ref armorPenetration, "dbArmorPen", 0f);
            Scribe_Values.Look(ref igniteGround, "dbIgniteGround", false);
            Scribe_Values.Look(ref snowDepth, "dbSnowDepth", 0f);
            Scribe_Defs.Look(ref appliedHediff, "dbHediff");
            Scribe_Values.Look(ref appliedHediffSeverity, "dbHediffSeverity", 1f);
            Scribe_Defs.Look(ref fleckDef, "dbFleck");
            Scribe_Defs.Look(ref breathSound, "dbSound");
            Scribe_Values.Look(ref fleckScale, "dbFleckScale", 1.4f);
            Scribe_Values.Look(ref tint, "dbTint", Color.white);

            // The cell lists are NOT saved - they are derived from origin/impact/shape and cost
            // one pass to rebuild. Saving them would be several hundred IntVec3 per breath for
            // something reproducible, and SpawnSetup runs on load anyway.
        }
    }
}
