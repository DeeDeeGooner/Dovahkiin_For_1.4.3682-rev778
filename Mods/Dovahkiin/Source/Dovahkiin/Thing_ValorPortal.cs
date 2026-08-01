// ============================================================================================
// CALL OF VALOR'S PORTAL - the cast effect.
//
// The user's spec: bright white waves CIRCLING THE TARGET CELL, reading as an opening through
// which the hero steps. Every other shout in this mod sends a cone or a ring travelling out
// from the caster; this one does not travel at all.
//
// WHY THIS IS NOT A Thing_ShoutWave, settled by reading that class rather than by preference:
//   - its `origin` is hard-set to caster.Position in Spawn(), so it cannot sit on a target cell
//   - BuildRings buckets cells by DISTANCE from that origin
//   - Tick draws band `head = progress * bands` - a front marching outward
//   - `inward` reverses that march and does nothing else; there is no rotation anywhere in it
// A portal is the opposite shape: it does not travel, it SPINS, and it sits where it was aimed.
// Bending the wave class to cover both would put a rotation branch on the code path that every
// shout in the mod already runs through, to serve exactly one of them.
//
// So this is its own Thing, and it is the SAME route Thing_DragonAspectOverlay already uses for
// the aura: DrawerType.RealtimeOnly + an override of the virtual Thing.DrawAt, drawing rotated
// quads through Graphics.DrawMesh with a MaterialPropertyBlock for the tint.
// NO HARMONY PATCH, and nothing on the pawn render path - which is what keeps it clear of
// RocketMan, the single most likely thing to break a render patch in this modlist.
//
// The animation is a port of Tools/GenerateValorPortal.ps1, which the user has seen move. Where
// a number here looks arbitrary it came from that script, and the script says why it is what it
// is - most of them were arrived at by rejecting something that read wrongly.
// ============================================================================================
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// The portal Call of Valor steps out of. Purely cosmetic: it owns no game state, and
    /// destroying it at any moment loses the picture and nothing else. The summon is spawned by
    /// the shout, not by this - see ArrivalTick for the moment the two are meant to line up.
    ///
    /// [StaticConstructorOnStartup] because the class holds static Graphic fields. RimWorld warns
    /// about that on principle, since assets must load on the main thread; here it is a false
    /// positive, because the graphics are resolved LAZILY on first draw and never at def-load
    /// (GraphicDatabase is not safe to touch before content has loaded). The attribute marks the
    /// type as aware of the rule rather than forcing the fields to populate early. Without it
    /// the game logs a yellow line every startup, and CLAUDE.md does not allow unexplained ones.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Thing_ValorPortal : Thing
    {
        // --- saved state ---------------------------------------------------------------
        private int ticksLeft = -1;
        private int lifetimeTicks = 90;
        private float radiusCells = 1.10f;

        /// <summary>
        /// Accumulated rotation per orbit, in degrees.
        ///
        /// ACCUMULATED, not recomputed from the clock, and that is deliberate: the spin RATE
        /// changes over the effect's life (see SpinRateAt), so `rate * elapsed` would be wrong
        /// at every moment except the two ends. The generator makes the same point in the same
        /// words - "accumulate, do not recompute".
        ///
        /// Saved, so a portal caught mid-spin by a save/reload resumes where it was rather than
        /// snapping back to zero.
        /// </summary>
        private float spinInner;
        private float spinMiddle;
        private float spinOuter;

        /// <summary>
        /// Is a hero still due to step out of this portal?
        ///
        /// THE PAWN IS CREATED AT THE FLASH, NOT AT THE CAST. The first build spawned him
        /// immediately and opened the portal around him, and the user's playtest verdict was
        /// exactly right: *"he appears as soon as I click (rather than waiting for the brightest
        /// shine of the portal)"*. A portal you step out of has to have something happen before
        /// you are there.
        ///
        /// **What is stored across those 54 ticks is a FLAG AND A FACTION, never a live
        /// unspawned pawn.** That distinction is the whole reason this is safe: an unspawned
        /// pawn held in a field is precisely the orphaned-state hazard RISKS.md section 9 is
        /// about, and it would be written into the save. A bool cannot be orphaned - if this
        /// portal is destroyed early, or the map is lost, the worst case is that no hero
        /// arrives, which is a cosmetic failure and not a stranded colonist.
        /// </summary>
        private bool summonPending;
        private Pawn summonCaster;

        /// <summary>Ticks the hero should stay, or 0 for the tuning default. Set by the shout.</summary>
        private int summonLifetime;

        // --- the orbit table -----------------------------------------------------------
        //
        // NOT in DovahkiinTuningDef, and that is a judgement rather than an oversight.
        //
        // These are welded to the SPRITE: the arc is baked at ArcBakedRadius of its own frame,
        // and the quad size below inverts that constant to place it. Changing a radius here
        // without regenerating the art moves the arc off its own orbit, so these are art
        // geometry, not balance numbers. CLAUDE.md's rule is aimed at numbers the user should be
        // able to retune without a rebuild; a number that also requires re-running a generator
        // is not one of those. The genuinely tunable ones ARE exposed - see DovahkiinTuningDef:
        // valorPortalLifetimeTicks, valorPortalRadiusCells, valorPortalArriveAtFraction,
        // valorPortalGlowGain.
        //
        // Counts are 3/2/4 rather than equal so the composite does not repeat every 360/N
        // degrees - the Dragon Aspect aura hit exactly that and read as a stutter, not motion.
        // Adjacent orbits COUNTER-ROTATE: co-rotating rings read as one disc turning, opposed
        // ones read as a mechanism opening, which is the whole ask.
        private static readonly float[] OrbitRadius = { 0.45f, 0.73f, 1.02f };
        private static readonly int[] OrbitCount = { 3, 2, 4 };
        private static readonly float[] OrbitSpinDegPerSec = { 310f, -215f, 152f };
        private static readonly float[] OrbitAlpha = { 0.78f, 1.00f, 0.70f };
        private static readonly float[] OrbitPhase = { 0f, 40f, 18f };

        /// <summary>
        /// The arc sprite's own baked radius, as a fraction of its frame's half-width.
        ///
        /// This is the ONLY arithmetic connecting the art to the code: an arc baked at 0.70 of
        /// the half-frame, drawn on a quad of S world units, lands at world radius S * 0.70 / 2.
        /// Every quad size below inverts it. Named once, used everywhere, and it must match
        /// $R_ARC in GenerateValorPortal.ps1 - if that changes, this changes.
        /// </summary>
        private const float ArcBakedRadius = 0.70f;

        // Cool, not white, and it looks like a mistake until you see it over ground.
        //
        // A glow is ADDITIVE and RimWorld's ground is brown, roughly (122,106,84). Adding equal
        // R, G and B to that reaches full red long before full blue, so a glow authored pure
        // white comes out WARM GOLD at any brightness short of clipping. The first render of
        // this effect was gold on every frame that was not the flash - which is not "bright
        // white waves" by any reading. Biasing the tint cool puts the extra light into the
        // channel the ground has least of, so the sum lands neutral. The game's additive shader
        // does the same arithmetic over the same terrain as the preview did.
        private static readonly Color TintArc = new Color(206f / 255f, 234f / 255f, 255f / 255f);
        private static readonly Color TintCore = new Color(234f / 255f, 246f / 255f, 255f / 255f);

        private const string TexRoot = "Things/Effects/ValorPortal/";

        private static Graphic arcGraphic;
        private static Graphic coreGraphic;
        private static Graphic ringGraphic;

        /// <summary>
        /// The tick at which the hero should step through, so the summon and the flash agree.
        /// Exposed rather than duplicated: when the summon is built it should ask the portal,
        /// not carry its own copy of the fraction and drift out of step with it.
        /// </summary>
        public int ArrivalTick
        {
            get
            {
                float fraction = 0.60f;
                DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
                if (tuning != null && tuning.valorPortalArriveAtFraction > 0f)
                {
                    fraction = tuning.valorPortalArriveAtFraction;
                }
                return Mathf.RoundToInt(lifetimeTicks * fraction);
            }
        }

        /// <summary>Has the portal reached the moment the hero steps out?</summary>
        public bool HasArrived
        {
            get { return (lifetimeTicks - ticksLeft) >= ArrivalTick; }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                return;
            }
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning != null)
            {
                if (tuning.valorPortalLifetimeTicks > 0)
                {
                    lifetimeTicks = tuning.valorPortalLifetimeTicks;
                }
                if (tuning.valorPortalRadiusCells > 0f)
                {
                    radiusCells = tuning.valorPortalRadiusCells;
                }
            }
            ticksLeft = lifetimeTicks;
        }

        public override void Tick()
        {
            // DO NOT call base.Tick(). Verse.Thing.Tick, TickRare and TickLong are all six-byte
            // stubs containing a throw opcode - verified by reading their IL, and the cause of a
            // whole playtest round when Thing_DragonAspectOverlay politely called base.TickRare()
            // and its own cleanup below the call never ran.
            if (ticksLeft < 0)
            {
                ticksLeft = lifetimeTicks;
            }
            ticksLeft--;

            // THE HERO STEPS OUT AT THE FLASH. Checked before the expiry test below, so a
            // portal whose last tick is also its arrival tick still delivers him.
            if (summonPending && HasArrived)
            {
                summonPending = false;
                Map here = Map;
                IntVec3 cell = Position;
                Pawn caster = summonCaster;
                summonCaster = null;
                // Wrapped: a failure here must cost the hero and NOT leave a half-open portal
                // ticking forever, and it must never propagate into Thing.Tick.
                try
                {
                    CallOfValorUtility.SpawnHeroAt(caster, here, cell, summonLifetime);
                }
                catch (Exception ex)
                {
                    Log.Error("[Dovahkiin] Call of Valor: the hero failed to step through his "
                        + "portal. The portal will still close normally. " + ex);
                }
            }

            if (ticksLeft <= 0)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            float life = LifeFraction;
            float rate = SpinRateAt(life);
            const float SecondsPerTick = 1f / 60f;
            spinInner += OrbitSpinDegPerSec[0] * SecondsPerTick * rate;
            spinMiddle += OrbitSpinDegPerSec[1] * SecondsPerTick * rate;
            spinOuter += OrbitSpinDegPerSec[2] * SecondsPerTick * rate;
        }

        /// <summary>0 at the moment of casting, 1 at the moment it dies.</summary>
        private float LifeFraction
        {
            get
            {
                if (lifetimeTicks <= 0)
                {
                    return 1f;
                }
                return Mathf.Clamp01((lifetimeTicks - ticksLeft) / (float)lifetimeTicks);
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            EnsureGraphics();
            if (arcGraphic == null)
            {
                return;
            }

            float life = LifeFraction;
            float open = OpenAt(life);
            float bright = BrightAt(life);
            float core = CoreAt(life);

            float gain = 1.42f;
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning != null && tuning.valorPortalGlowGain > 0f)
            {
                gain = tuning.valorPortalGlowGain;
            }

            // Unlike Thing_DragonAspectOverlay, this Thing DOES belong at its own position - it
            // is a hole in the ground, not something riding a pawn. So drawLoc is used rather
            // than ignored.
            Vector3 basePos = drawLoc;

            // Additive, so drawing OVER the pawn adds light to him rather than hiding him -
            // which is what "stepping out of a bright opening" looks like, and what the preview
            // the user saw does (it composites the glow layer on top of the pawn).
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            // Core first, so the arcs read OVER it.
            basePos.y = altitude;
            DrawQuad(coreGraphic, basePos, 2f * radiusCells * open * 1.05f, 0f,
                TintCore, core * 0.9f, gain);

            // The anchoring hairline. Without it the arcs read as loose streaks rather than as
            // something with a rim.
            basePos.y = altitude + 0.002f;
            DrawQuad(ringGraphic, basePos, 2f * radiusCells * open / ArcBakedRadius, 0f,
                TintArc, bright * 0.55f, gain);

            basePos.y = altitude + 0.004f;
            for (int orbit = 0; orbit < OrbitRadius.Length; orbit++)
            {
                // Invert the sprite's own baked radius to get the quad size - see ArcBakedRadius.
                float quad = (2f * radiusCells * OrbitRadius[orbit] * open) / ArcBakedRadius;
                float spin = SpinOf(orbit);
                int count = OrbitCount[orbit];
                for (int arc = 0; arc < count; arc++)
                {
                    float angle = spin + OrbitPhase[orbit] + ((arc * 360f) / count);
                    DrawQuad(arcGraphic, basePos, quad, angle,
                        TintArc, bright * OrbitAlpha[orbit], gain);
                }
            }
        }

        private float SpinOf(int orbit)
        {
            if (orbit == 0)
            {
                return spinInner;
            }
            if (orbit == 1)
            {
                return spinMiddle;
            }
            return spinOuter;
        }

        // --- the animation curves, ported from GenerateValorPortal.ps1 -------------------

        /// <summary>
        /// How far open, as a multiple of the full radius. Eases OUT - a portal snaps open and
        /// settles, it does not arrive at its size linearly - then blows outward as it dies.
        /// </summary>
        private static float OpenAt(float time)
        {
            if (time < 0.40f)
            {
                float eased = 1f - Mathf.Pow(1f - (time / 0.40f), 3f);
                return 0.18f + (0.82f * eased);
            }
            if (time < 0.78f)
            {
                return 1f;
            }
            return 1f + (0.20f * ((time - 0.78f) / 0.22f));
        }

        /// <summary>Overall opacity of the arcs: fade in, hold, fade out.</summary>
        private static float BrightAt(float time)
        {
            if (time < 0.13f)
            {
                return Smooth(time / 0.13f);
            }
            if (time < 0.74f)
            {
                return 1f;
            }
            return Smooth((1f - time) / 0.26f);
        }

        /// <summary>
        /// The core, plus the flash at the moment he steps through.
        ///
        /// The 0.55 ceiling is not a round number picked for tidiness: at 0.72 the core washed
        /// out the inner orbit for the whole middle of the effect, so the innermost wave stopped
        /// existing exactly when the portal is most open. The core is the way THROUGH; the arcs
        /// are the effect.
        /// </summary>
        private static float CoreAt(float time)
        {
            float baseLevel;
            if (time < 0.50f)
            {
                baseLevel = Smooth((time - 0.08f) / 0.42f) * 0.55f;
            }
            else if (time < 0.72f)
            {
                baseLevel = 0.55f;
            }
            else
            {
                baseLevel = 0.55f * Smooth((1f - time) / 0.28f);
            }

            float arriveAt = 0.60f;
            DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
            if (tuning != null && tuning.valorPortalArriveAtFraction > 0f)
            {
                arriveAt = tuning.valorPortalArriveAtFraction;
            }
            // a short spike either side of the arrival
            float flashDist = Mathf.Abs(time - arriveAt) / 0.075f;
            if (flashDist < 1f)
            {
                baseLevel += (1f - (flashDist * flashDist)) * 0.95f;
            }
            return baseLevel;
        }

        /// <summary>Wind-up: the arcs turn slowly at first and spin up as the portal opens.</summary>
        private static float SpinRateAt(float time)
        {
            return 0.42f + (0.58f * Smooth(time / 0.42f));
        }

        private static float Smooth(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - (2f * clamped));
        }

        // --- drawing ---------------------------------------------------------------------

        /// <summary>
        /// One tinted, rotated quad. Same shape as Thing_DragonAspectOverlay.DrawQuad, minus the
        /// mirroring it needs and this does not - an arc has no handedness worth flipping.
        ///
        /// GLOW GAIN MULTIPLIES THE COLOUR, NOT THE ALPHA. With an additive shader those are not
        /// interchangeable: alpha decides how much of the sprite's own shape reaches the screen,
        /// while colour above 1.0 makes what does reach it brighter. Pushing alpha instead would
        /// fatten the arcs' gaussian tails until the three orbits merged into one solid ring,
        /// which is the exact thing "waves circling" is not.
        /// </summary>
        private static void DrawQuad(Graphic graphic, Vector3 pos, float size, float angleDeg,
            Color tint, float alpha, float gain)
        {
            if (graphic == null || alpha <= 0.004f || size <= 0.001f)
            {
                return;
            }
            Material mat = graphic.MatSingle;
            if (mat == null)
            {
                return;
            }
            Vector3 scale = new Vector3(size, 1f, size);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), scale);

            Color colour = new Color(tint.r * gain, tint.g * gain, tint.b * gain,
                Mathf.Clamp01(alpha));
            MaterialPropertyBlock block = PropBlock;
            block.Clear();
            block.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, block);
        }

        private static MaterialPropertyBlock propBlockInt;
        private static MaterialPropertyBlock PropBlock
        {
            get
            {
                if (propBlockInt == null)
                {
                    propBlockInt = new MaterialPropertyBlock();
                }
                return propBlockInt;
            }
        }

        /// <summary>
        /// Resolved on first draw, never at def-load: GraphicDatabase is not safe to touch
        /// before the game has loaded its content.
        ///
        /// ShaderDatabase.MoteGlow is the ADDITIVE one, and additive is the whole point - these
        /// sprites are light, not paint. Verified as a public static Shader field on
        /// Verse.ShaderDatabase by reflection over 1.4's own Assembly-CSharp, because a FleckDef's
        /// "shaderType MoteGlow" string and a C# member of that name are not the same thing.
        ///
        /// Graphic_Single, not Graphic_Multi: a portal has no facing. The drawSize passed here is
        /// irrelevant - DrawAt builds its own matrix and these graphics are only ever asked for
        /// their Material.
        /// </summary>
        private static void EnsureGraphics()
        {
            if (arcGraphic != null)
            {
                return;
            }
            Vector2 one = new Vector2(1f, 1f);
            arcGraphic = GraphicDatabase.Get<Graphic_Single>(
                TexRoot + "ValorPortalArc", ShaderDatabase.MoteGlow, one, Color.white);
            coreGraphic = GraphicDatabase.Get<Graphic_Single>(
                TexRoot + "ValorPortalCore", ShaderDatabase.MoteGlow, one, Color.white);
            ringGraphic = GraphicDatabase.Get<Graphic_Single>(
                TexRoot + "ValorPortalRing", ShaderDatabase.MoteGlow, one, Color.white);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks", 90);
            Scribe_Values.Look(ref radiusCells, "radiusCells", 1.10f);
            Scribe_Values.Look(ref spinInner, "spinInner", 0f);
            Scribe_Values.Look(ref spinMiddle, "spinMiddle", 0f);
            Scribe_Values.Look(ref spinOuter, "spinOuter", 0f);
            // A flag and a reference to an ALREADY-SPAWNED pawn (the caster). Never an
            // unspawned summon - see summonPending.
            Scribe_Values.Look(ref summonPending, "summonPending", false);
            Scribe_References.Look(ref summonCaster, "summonCaster");
            Scribe_Values.Look(ref summonLifetime, "summonLifetime", 0);
        }

        /// <summary>
        /// Open a portal on a cell. Returns it so a caller can ask ArrivalTick, and null rather
        /// than throwing if anything is missing - this is a cosmetic effect and it must never be
        /// able to take a summon down with it. The Ancient Dragonborn's first playtest failed
        /// worse than it needed to because one catch-all wrapped the load-bearing steps and the
        /// decorative ones together, so a missing weapon comp cost the entire ally.
        /// </summary>
        /// <summary>
        /// Open a portal that a hero will step out of at its flash.
        /// </summary>
        public static Thing_ValorPortal OpenAndSummon(Map map, IntVec3 cell, Pawn caster,
            int lifetimeOverride = 0)
        {
            Thing_ValorPortal portal = Open(map, cell);
            if (portal != null)
            {
                portal.summonPending = true;
                portal.summonCaster = caster;
                portal.summonLifetime = lifetimeOverride;
            }
            return portal;
        }

        public static Thing_ValorPortal Open(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid)
            {
                return null;
            }
            ThingDef def = DovahkiinDefOf.Dovahkiin_ValorPortal;
            if (def == null)
            {
                Log.Warning("[Dovahkiin] Dovahkiin_ValorPortal def is missing - no portal drawn.");
                return null;
            }
            try
            {
                Thing_ValorPortal portal = (Thing_ValorPortal)ThingMaker.MakeThing(def);
                GenSpawn.Spawn(portal, cell, map);
                return portal;
            }
            catch (Exception ex)
            {
                Log.Warning("[Dovahkiin] Could not open Call of Valor's portal: " + ex);
                return null;
            }
        }
    }
}
