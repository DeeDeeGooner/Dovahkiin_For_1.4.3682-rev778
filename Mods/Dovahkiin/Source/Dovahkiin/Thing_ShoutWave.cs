// Implements: SPEC.md 4.3 - "a shockwave/cone VFX in the facing direction".
//
// Playtest feedback: spawning every fleck at once read as "an instant cone of dust just
// manifested". A shout should be seen TRAVELLING outward from the mouth. This emits the cone as
// an expanding front over real time, fading from a bright leading edge to nothing behind it.
//
// A Thing rather than a MapComponent so it only ticks while a shout is actually in flight -
// CLAUDE.md forbids per-tick work that could be avoided, and RocketMan is installed.
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class Thing_ShoutWave : Thing
    {
        private IntVec3 origin;
        private IntVec3 towards;
        private float range = 8f;
        private float coneAngle = 50f;
        private Color headColor = new Color(0.45f, 0.75f, 1f);
        private int lifespanTicks = 48;
        private float fleckScale = 1.7f;
        private FleckDef fleckDef;

        private int age;
        private int lastStruckBand = -1;

        /// <summary>
        /// How fast the front travels, in cells per second. The PawnFlyer used for knockback is
        /// tuned to the same figure so a thrown pawn rides the blast instead of outrunning it.
        /// </summary>
        public const float CellsPerSecond = 10f;

        /// <summary>Cells bucketed by their distance ring, computed once on spawn.</summary>
        private List<List<IntVec3>> rings;

        // --- Payload. The wave DELIVERS the shout; it is not decoration over an instant hit. ---
        private Pawn instigator;
        private DamageDef damageDef;
        private float damageAmount;
        private float knockbackCells;
        private int stunTicks;
        private bool ignitePawns;
        private bool igniteGround;
        private HediffDef appliedHediff;
        private float appliedHediffSeverity = 1f;
        private HashSet<Pawn> alreadyHit;

        /// <summary>
        /// Runs the front from the OUTER edge back to the caster instead of outward.
        ///
        /// An inward wave is cosmetic by construction: StrikeBand is skipped entirely for one,
        /// so it can never damage, stun or apply anything. That is deliberate - a returning
        /// wave passes back over ground the outgoing wave already hit, and hitting everyone a
        /// second time on the way home is not what "the shout comes back" should mean.
        /// </summary>
        private bool inward;

        /// <summary>Colour at the END of the wave's life. Lerped from headColor over progress.</summary>
        private Color endColor;
        private bool hasEndColor;

        /// <summary>Ticks to wait before moving. Lets a return wave be queued behind an outgoing one.</summary>
        private int startDelayTicks;

        public static Thing_ShoutWave Spawn(Pawn caster, IntVec3 targetCell, float range,
            float coneAngle, Color headColor, FleckDef fleckDef, float fleckScale,
            float cellsPerSecond = CellsPerSecond, float laneWidth = 0f,
            float vortexRadius = 0f, float vortexSpinPerTick = 22f, float alphaScale = 1f,
            bool inward = false, Color? endColor = null, int startDelayTicks = 0)
        {
            if (caster == null || caster.Map == null || DovahkiinDefOf.Dovahkiin_ShoutWave == null)
            {
                return null;
            }
            Thing_ShoutWave wave = (Thing_ShoutWave)ThingMaker.MakeThing(
                DovahkiinDefOf.Dovahkiin_ShoutWave);
            wave.origin = caster.Position;
            wave.towards = targetCell;
            wave.range = range;
            wave.coneAngle = coneAngle;
            wave.headColor = headColor;
            wave.fleckDef = fleckDef;
            wave.fleckScale = fleckScale;
            wave.instigator = caster;
            wave.alreadyHit = new HashSet<Pawn>();
            // Derived from range so every shout's front moves at the same speed - a long shout
            // takes longer to arrive, which is what makes reach legible.
            wave.cellsPerSecond = Mathf.Max(1f, cellsPerSecond);
            wave.laneWidth = laneWidth;
            wave.vortexRadius = vortexRadius;
            wave.vortexSpinPerTick = vortexSpinPerTick;
            wave.alphaScale = Mathf.Clamp(alphaScale, 0.05f, 1f);
            wave.inward = inward;
            wave.endColor = endColor.HasValue ? endColor.Value : headColor;
            wave.hasEndColor = endColor.HasValue;
            wave.startDelayTicks = Mathf.Max(0, startDelayTicks);
            wave.lifespanTicks = Mathf.Max(8, Mathf.RoundToInt(range / wave.cellsPerSecond * 60f));
            GenSpawn.Spawn(wave, caster.Position, caster.Map);
            return wave;
        }

        /// <summary>
        /// What the front does when it reaches someone. Set immediately after Spawn.
        ///
        /// This exists because playtest found targets being flung *before* the wave reached
        /// them: the comp was applying damage on cast while the visual took ~1s to arrive.
        /// Effects now travel with the front, so cause and effect line up.
        /// </summary>
        /// <summary>
        /// How many separate damage instances to split the total into. More instances means
        /// more body parts hit - Frost Breath encases a whole body rather than taking one
        /// finger off, which is what a wall of ice should do.
        /// </summary>
        private int damageInstances = 1;

        /// <summary>Snow laid in the wake of the front, per cell. Frost Breath's ice trail.</summary>
        private float snowDepth;

        /// <summary>Aim each hit by the shared priority rule rather than letting RimWorld roll.</summary>
        private bool spreadDamage;

        /// <summary>
        /// Fraction of the hits repeated on already-damaged parts, immediately. Fire only.
        /// </summary>
        private float reburnFraction;

        /// <summary>
        /// A second hediff applied alongside the first. Frost Breath uses it for ice-encasing:
        /// StunHandler proved unreliable in playtest (a pawn walked out of a level-3 freeze), so
        /// the freeze is a hediff that zeroes the Moving capacity instead. It is guaranteed,
        /// visible in the health tab, and does not depend on stun internals.
        /// </summary>
        private HediffDef secondaryHediff;
        private float secondaryHediffSeverity = 1f;

        /// <summary>Overrides the default wave speed. Whirlwind Sprint's trail outruns the dash.</summary>
        private float cellsPerSecond = CellsPerSecond;

        /// <summary>
        /// When above zero, the wave is a constant-width LANE rather than a widening cone.
        /// Used by Whirlwind Sprint, which is a dash down a corridor, not a blast.
        /// </summary>
        private float laneWidth;

        /// <summary>
        /// When above zero, the wave is a VORTEX - a compact rotating column of this radius that
        /// travels along the line toward the target. Cyclone: a tornado crossing the ground, not
        /// a cone and not a spreading spiral.
        ///
        /// The first attempt at Cyclone was a spiral ARM sweeping outward, which playtest
        /// correctly called nothing like the shout. The difference that matters: a vortex is
        /// LOCAL - a small disc that moves - where a cone or spiral is a front that expands.
        /// Bands are therefore indexed by distance ALONG the travel line, not from the origin.
        /// </summary>
        private float vortexRadius;

        /// <summary>Degrees the visible column spins per tick. Cosmetic only.</summary>
        private float vortexSpinPerTick = 22f;

        /// <summary>Centre of each band, needed to spin the column. Vortex mode only.</summary>
        private List<Vector3> ringCenters;

        /// <summary>
        /// Multiplier on the wave's opacity. Below 1 makes a fainter, more transparent front -
        /// Cyclone is meant to be barely visible, less so even than Whirlwind Sprint's trail.
        /// </summary>
        private float alphaScale = 1f;

        /// <summary>
        /// A mental state inflicted on whoever the front reaches. Dismay's fear.
        /// Applied with the other CONTROL effects, before any damage - see StrikeBand.
        /// </summary>
        private MentalStateDef mentalState;
        private float mentalStateChance;

        /// <summary>
        /// Armour penetration for the payload's damage.
        ///
        /// ZERO IS A REAL CHOICE AND IT BIT US: Soul Tear shipped with 0 and a heavily armoured
        /// modded raider shrugged the whole shout off, because Dovahkiin_SoulWither is
        /// Blunt-parented and therefore fully reduced by blunt armour. A breath weapon may
        /// reasonably be stopped by plate; the mod's most powerful shout may not.
        /// </summary>
        private float armorPenetration;

        /// <summary>
        /// Stop the wave dead once it reaches ANY pawn. Soul Tear: a single-target bolt that
        /// halts at the first body it meets, exactly as it does in TES5 - never a cone, never a
        /// line that carries on through the rank behind.
        /// </summary>
        private bool stopOnFirstPawn;
        private bool hasStruckSomeone;

        /// <summary>Shout level for Soul Tear's puppet roll. Zero means "not a Soul Tear".</summary>
        private int soulTearLevel;

        /// <summary>How many bands of fading trail follow the front. Higher is a longer tail.</summary>
        private int trailBands = 2;

        public void SetPayload(DamageDef damageDef, float damageAmount, float knockbackCells,
            int stunTicks, bool ignitePawns, bool igniteGround,
            HediffDef appliedHediff = null, float appliedHediffSeverity = 1f,
            int damageInstances = 1, float snowDepth = 0f,
            HediffDef secondaryHediff = null, float secondaryHediffSeverity = 1f,
            bool spreadDamage = false, float reburnFraction = 0f,
            MentalStateDef mentalState = null, float mentalStateChance = 0f,
            float armorPenetration = 0f)
        {
            this.mentalState = mentalState;
            this.mentalStateChance = mentalStateChance;
            this.armorPenetration = armorPenetration;
            this.secondaryHediff = secondaryHediff;
            this.secondaryHediffSeverity = secondaryHediffSeverity;
            this.spreadDamage = spreadDamage;
            this.reburnFraction = reburnFraction;
            this.damageDef = damageDef;
            this.damageAmount = damageAmount;
            this.knockbackCells = knockbackCells;
            this.stunTicks = stunTicks;
            this.ignitePawns = ignitePawns;
            this.igniteGround = igniteGround;
            this.appliedHediff = appliedHediff;
            this.appliedHediffSeverity = appliedHediffSeverity;
            this.damageInstances = Mathf.Max(1, damageInstances);
            this.snowDepth = snowDepth;
        }

        /// <summary>Apply the payload to everyone standing in the band the front just reached.</summary>
        private void StrikeBand(int band)
        {
            if (alreadyHit == null || band < 0 || band >= rings.Count)
            {
                return;
            }
            List<IntVec3> cells = rings[band];
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 c = cells[i];

                // Ignition and the ice laid in the wake of the front. Moved to DovahkiinStrike on
                // 2026-08-05 with NO change of behaviour - the constants and the order are the
                // same - so a dragon's breath can scorch identically instead of growing its own
                // copy that drifts the first time either is tuned.
                DovahkiinStrike.ScorchCell(c, Map, igniteGround, snowDepth);

                List<Thing> things = c.GetThingList(Map);
                // Reverse: effects can despawn a pawn (the flyer removes it from the cell).
                for (int t = things.Count - 1; t >= 0; t--)
                {
                    Pawn p = things[t] as Pawn;
                    if (p == null || p == instigator || alreadyHit.Contains(p))
                    {
                        continue;
                    }
                    alreadyHit.Add(p);

                    // ORDER MATTERS. Control effects are applied BEFORE damage.
                    //
                    // Previously damage ran first, and a heavy breath could down the victim
                    // outright - at which point stunning an already-collapsed pawn is invisible
                    // and meaningless. Playtest read that as "the stun doesn't work". Freeze
                    // first, then hurt.
                    if (stunTicks > 0 && p.stances != null && p.stances.stunner != null)
                    {
                        p.stances.stunner.StunFor(stunTicks, instigator, false, false);
                    }
                    // Dismay's fear. A mental state is a CONTROL effect, so it belongs here with
                    // the stun and before any damage - a downed pawn cannot flee, and starting a
                    // mental state on one that is already collapsed does nothing visible.
                    if (mentalState != null && Rand.Chance(mentalStateChance)
                        && p.mindState != null && p.mindState.mentalStateHandler != null
                        && !p.Downed && !p.Dead)
                    {
                        p.mindState.mentalStateHandler.TryStartMentalState(mentalState);
                    }
                    if (secondaryHediff != null && p.health != null)
                    {
                        Hediff prior2 = p.health.hediffSet.GetFirstHediffOfDef(secondaryHediff);
                        if (prior2 != null)
                        {
                            prior2.Severity = Mathf.Max(prior2.Severity, secondaryHediffSeverity);
                        }
                        else
                        {
                            p.health.AddHediff(secondaryHediff).Severity = secondaryHediffSeverity;
                        }
                    }

                    if (damageDef != null && damageAmount > 0f)
                    {
                        // Split into several hits so the damage lands across multiple body
                        // parts. One large instance concentrates on a single limb, which is
                        // wrong for a breath weapon engulfing a whole target.
                        float per = damageAmount / damageInstances;
                        for (int d = 0; d < damageInstances && !p.Destroyed; d++)
                        {
                            // spreadDamage aims each hit with the shared priority rule - core
                            // and head over extremities - instead of letting RimWorld roll.
                            // Unrelenting Force uses it so its bruising lands where a shove
                            // would, and does not simply crush the victim's feet.
                            BodyPartRecord part = spreadDamage
                                ? DovahkiinDamageUtility.SelectSpreadTarget(p)
                                : null;
                            p.TakeDamage(new DamageInfo(
                                damageDef, per, armorPenetration, -1f, instigator, part));
                        }

                        // Fire burns the same wound deeper rather than finding fresh skin.
                        // This is what makes flame more lethal than frost at comparable totals:
                        // concentrated damage destroys parts, spread damage only hurts.
                        if (reburnFraction > 0f)
                        {
                            int extra = Mathf.Max(1,
                                Mathf.RoundToInt(damageInstances * reburnFraction));
                            for (int d = 0; d < extra && !p.Destroyed; d++)
                            {
                                BodyPartRecord deep = DovahkiinDamageUtility.SelectDeepenTarget(p);
                                p.TakeDamage(new DamageInfo(
                                    damageDef, per, armorPenetration, -1f, instigator, deep));
                            }
                        }
                    }
                    if (ignitePawns && !p.Destroyed)
                    {
                        p.TryAttachFire(0.6f);
                    }
                    if (appliedHediff != null && !p.Destroyed && p.health != null)
                    {
                        // Stack severity rather than adding a second copy, so repeated hits
                        // deepen the effect instead of cluttering the health tab.
                        Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(appliedHediff);
                        if (existing != null)
                        {
                            existing.Severity += appliedHediffSeverity;
                        }
                        else
                        {
                            Hediff added = p.health.AddHediff(appliedHediff);
                            added.Severity = appliedHediffSeverity;
                            // Drain Vitality gives the life it takes back to the caster, so the
                            // hediff has to know who cast it. No other hediff needs this, hence
                            // a type check rather than a field on every payload.
                            Hediff_VitalityDrained drain = added as Hediff_VitalityDrained;
                            if (drain != null)
                            {
                                drain.drainedBy = instigator;
                            }
                        }
                    }
                    if (knockbackCells > 0f && !p.Destroyed && p.Spawned)
                    {
                        ShoutTargeting.Knockback(p, origin, knockbackCells);
                    }
                    else if (knockbackCells <= 0f && !p.Destroyed && p.jobs != null)
                    {
                        p.jobs.StopAll();
                    }

                    // Soul Tear's roll happens where the bolt LANDS, not on cast, so the puppet
                    // rises at the moment of impact rather than a second before the effect
                    // arrives. Same reasoning as every other payload in this class.
                    if (soulTearLevel > 0)
                    {
                        SoulTearUtility.Resolve(instigator, p, soulTearLevel);
                    }

                    hasStruckSomeone = true;
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            BuildRings();
        }

        /// <summary>
        /// Pre-bucket the cone's cells by integer distance so each tick only has to emit one
        /// band. Doing the geometry once keeps the per-tick cost to a list index.
        /// </summary>
        private void BuildRings()
        {
            int bands = Mathf.Max(1, Mathf.CeilToInt(range));
            rings = new List<List<IntVec3>>(bands + 1);
            for (int i = 0; i <= bands; i++)
            {
                rings.Add(new List<IntVec3>());
            }

            Vector3 originV = origin.ToVector3Shifted();
            Vector3 facing = towards.ToVector3Shifted() - originV;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f)
            {
                // The target IS the caster's own cell, which is the normal case for a self-cast
                // ring (coneAngle 360, where facing is meaningless anyway). This used to return
                // early and leave the wave with no cells at all - an invisible shout. Fall back
                // to an arbitrary facing rather than emitting nothing.
                facing = Vector3.forward;
            }
            facing.Normalize();
            // coneAngle 360 gives half = 180, and Vector3.Angle never exceeds 180, so every cell
            // passes the test and the front expands as a full circle. That is how Slow Time and
            // Clear Skies get a ring with no special-case geometry.
            float half = coneAngle * 0.5f;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, range, true))
            {
                if (!c.InBounds(Map) || c == origin)
                {
                    continue;
                }
                Vector3 to = c.ToVector3Shifted() - originV;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                if (vortexRadius > 0f)
                {
                    // Vortex mode: a disc of vortexRadius centred on a point that slides along
                    // the travel line. Banding by ALONG-distance rather than radial distance is
                    // what makes the column travel as one body instead of expanding.
                    float along = Vector3.Dot(to, facing);
                    if (along < 0f)
                    {
                        continue; // behind the caster
                    }
                    if ((to - facing * along).magnitude > vortexRadius)
                    {
                        continue;
                    }
                    int vband = Mathf.Clamp(Mathf.RoundToInt(along), 0, bands);
                    rings[vband].Add(c);
                    continue; // banded already; skip the shared radial banding below
                }
                else if (laneWidth > 0f)
                {
                    // Lane mode: constant width along the line, not a widening cone. Whirlwind
                    // Sprint is a dash down a corridor - playtest correctly called the cone
                    // shape wrong for it, since the far end fanned out.
                    float along = Vector3.Dot(to, facing);
                    if (along < 0f)
                    {
                        continue; // behind the caster
                    }
                    float perpendicular = (to - facing * along).magnitude;
                    if (perpendicular > laneWidth * 0.5f)
                    {
                        continue;
                    }
                }
                else
                {
                    // coneAngle 0 means a single-target shout: draw a narrow lane anyway so it
                    // still reads as something leaving the mouth.
                    float allowed = coneAngle <= 0f ? 12f : half;
                    if (Vector3.Angle(facing, to.normalized) > allowed)
                    {
                        continue;
                    }
                }
                int band = Mathf.Clamp(Mathf.RoundToInt(to.magnitude), 0, bands);
                rings[band].Add(c);
            }

            // Vortex only: remember where each band's column sits, so Tick can spin the visible
            // arc around it. Computed once here, never per tick.
            if (vortexRadius > 0f)
            {
                ringCenters = new List<Vector3>(bands + 1);
                for (int b = 0; b <= bands; b++)
                {
                    ringCenters.Add(originV + facing * b);
                }
            }
        }

        /// <summary>
        /// Soul Tear only: a single-target bolt that halts at the first body it meets, as it
        /// does in TES5. Set immediately after Spawn, alongside SetPayload.
        /// </summary>
        public void SetSoulTear(int level, bool stopAtFirstPawn, int trail)
        {
            soulTearLevel = level;
            stopOnFirstPawn = stopAtFirstPawn;
            trailBands = Mathf.Clamp(trail, 1, 12);
        }

        public override void Tick()
        {
            // Queued behind an outgoing wave: sit still and draw nothing until our turn.
            if (startDelayTicks > 0)
            {
                startDelayTicks--;
                return;
            }
            age++;
            if (rings == null || age > lifespanTicks)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            // Stopped dead by the first body it reached. The trail is left to fade naturally on
            // the tick after impact rather than vanishing mid-air, which would read as the bolt
            // being deleted rather than landing.
            if (stopOnFirstPawn && hasStruckSomeone)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            float progress = (float)age / lifespanTicks;
            int bands = rings.Count - 1;

            // The bright leading edge, plus a short fading tail behind it. An inward wave runs
            // the same front backwards, from the outer edge home to the caster.
            int head = Mathf.RoundToInt((inward ? 1f - progress : progress) * bands);

            // Deliver the hit exactly where the front is now. Every band between the last tick
            // and this one is struck, so nobody is skipped when the front moves more than one
            // cell per tick.
            //
            // Skipped entirely when inward: a returning wave is cosmetic by construction. It
            // travels back over ground the outgoing wave already covered, and striking everyone
            // a second time on the way home is not what a returning wave should mean.
            if (!inward)
            {
                for (int b = lastStruckBand + 1; b <= head; b++)
                {
                    StrikeBand(b);
                }
                lastStruckBand = Mathf.Max(lastStruckBand, head);
            }
            for (int back = 0; back <= trailBands; back++)
            {
                int band = head - back;
                if (band < 0 || band > bands)
                {
                    continue;
                }
                // Alpha falls off behind the front, so the wave visibly passes and dissipates.
                // Falloff is scaled to the trail LENGTH, not a fixed 0.33 per band. Hard-coding
                // it meant anything longer than three bands faded to nothing before it was
                // drawn, so a longer trail was silently invisible.
                float alpha = Mathf.Clamp01(1f - (back / (float)(trailBands + 1)))
                    * Mathf.Clamp01(1.15f - progress) * alphaScale;
                if (alpha <= 0.02f)
                {
                    continue;
                }
                // Grade towards endColor over the wave's life when one was given - this is how
                // Dragon Aspect's returning ring turns from ember to azure as it comes home.
                Color c = hasEndColor ? Color.Lerp(headColor, endColor, progress) : headColor;
                c.a = alpha;

                // A vortex is drawn as a FUNNEL, not as scattered cell dust - see DrawFunnel.
                // Filling every cell of the disc reads as a dust cloud no matter how it is
                // tinted, which is exactly what playtest reported: "there is no vortex".
                if (vortexRadius > 0f)
                {
                    if (back == 0)
                    {
                        DrawFunnel(band, c);
                    }
                    continue;
                }

                List<IntVec3> cells = rings[band];
                FleckDef def = fleckDef ?? FleckDefOf.DustPuffThick;

                for (int i = 0; i < cells.Count; i++)
                {
                    if (!Rand.Chance(0.7f))
                    {
                        continue;
                    }
                    // instanceColor carries the alpha, which is what produces the fade from a
                    // bright front to nothing. This only works because our fleck defs omit
                    // renderInstanced - instanced flecks are batched and ignore it.
                    FleckCreationData data = FleckMaker.GetDataStatic(
                        cells[i].ToVector3Shifted(), Map, def,
                        fleckScale * (0.7f + alpha * 0.5f));
                    data.instanceColor = c;
                    Map.flecks.CreateFleck(data);
                }
            }
        }

        /// <summary>
        /// Draw the vortex as a funnel: a few particles ORBITING the column's centre at
        /// different radii and heights, each individually rotated, rather than dust sprayed
        /// across every cell of the disc.
        ///
        /// This is the whole difference between "a grey cloud moving" and "a tornado". Playtest
        /// on the first build reported no vortex at all, and the cause was not the colour or the
        /// radius - it was that a filled disc of particles has no structure to read as rotation.
        /// Orbits give it structure: the same particle count, arranged instead of scattered.
        ///
        /// RimWorld of Magic solves this with a single purpose-drawn spinning sprite
        /// (Mote_ManaVortex, texture UI/manavortex_trans). That is genuinely the better way and
        /// it is what a real fix looks like - but it needs ART we do not have, and Core's own
        /// textures are packed into Unity bundles so there is no swirl texture to borrow.
        /// Logged in ART_TODO.md. This is the best approximation available from vanilla dust.
        /// </summary>
        private void DrawFunnel(int band, Color tint)
        {
            if (ringCenters == null || band < 0 || band >= ringCenters.Count)
            {
                return;
            }
            Vector3 centre = ringCenters[band];
            FleckDef def = fleckDef ?? FleckDefOf.DustPuffThick;

            // Three orbits at different radii. The inner one is tight and fast, the outer one
            // wide and slower, which is what gives a funnel its taper.
            const int Orbits = 3;
            const int PerOrbit = 4;
            for (int o = 0; o < Orbits; o++)
            {
                float t = (o + 1f) / Orbits;               // 0..1, inner to outer
                float orbitRadius = vortexRadius * t;
                float spin = age * vortexSpinPerTick * (1.6f - t);  // inner spins faster
                float scale = fleckScale * (1.15f - 0.45f * t);     // inner particles larger

                for (int i = 0; i < PerOrbit; i++)
                {
                    float a = spin + (360f / PerOrbit) * i;
                    float rad = a * Mathf.Deg2Rad;
                    Vector3 pos = centre + new Vector3(
                        Mathf.Cos(rad) * orbitRadius, 0f, Mathf.Sin(rad) * orbitRadius);
                    if (!pos.ToIntVec3().InBounds(Map))
                    {
                        continue;
                    }
                    Color c = tint;
                    c.a *= 1f - 0.25f * t;  // outer edge fainter, so the core reads as solid
                    FleckCreationData data = FleckMaker.GetDataStatic(pos, Map, def, scale);
                    data.instanceColor = c;
                    // Rotate each particle with the orbit so the whole thing turns as one body
                    // instead of looking like a ring of static specks.
                    data.rotation = a;
                    data.rotationRate = vortexSpinPerTick * 2f;
                    Map.flecks.CreateFleck(data);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // A wave lasts under a second. If a save happens mid-flight, let it simply expire on
            // load rather than persisting half-drawn state.
            Scribe_Values.Look(ref age, "age", 0);
            Scribe_Values.Look(ref lifespanTicks, "lifespanTicks", 45);
            // A queued return wave has not started yet, so unlike age this genuinely has to
            // survive a save - otherwise it fires the instant the game reloads.
            Scribe_Values.Look(ref startDelayTicks, "startDelayTicks", 0);
            Scribe_Values.Look(ref inward, "inward", false);
        }
    }
}
