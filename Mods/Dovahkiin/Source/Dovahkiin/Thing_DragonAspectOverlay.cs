// Implements: SPEC.md 4.4d - the Dragon Aspect spectral overlay.
//
// SPEC.md 4.4d is explicit that failing to deliver this overlay is a stop-and-report, not a
// silent downgrade to a stat buff, so this file is the reason the shout is shippable at all.
//
// ============================================================================================
// WHY A FOLLOWER THING AND NOT A RENDER PATCH
// ============================================================================================
// Three routes were checked against the real 1.4 assembly before any of this was written:
//
//   1. RimWorld.PawnOverlayDrawer DOES exist in 1.4 - it is how firefoam and wounds paint onto
//      a pawn's own body mesh, and it would have been ideal. But PawnRenderer only ever calls
//      the two instances it owns, from the PRIVATE RenderPawnInternal. Adding a third means
//      patching pawn rendering, which is the single thing RocketMan is most likely to break.
//      CLAUDE.md and SPEC.md 4.4d both say to avoid exactly that.
//
//   2. Invisible apparel needs 15 textures, not 3 - ApparelGraphicRecordGetter.TryGetGraphicApparel
//      resolves body-layer apparel per BodyTypeDef. It is also a real item: it shows in the Gear
//      tab, can be taken off, and drops on death.
//
//   3. A follower Thing needs no patch at all. Thing.DrawAt is virtual, DrawerType.RealtimeOnly
//      makes it draw every frame, and reading pawn.Drawer.DrawPos and pawn.Rotation tracks
//      movement and facing exactly. This is that.
//
// Nothing here patches anything. If RocketMan changes how pawns are rendered, this keeps working.
// ============================================================================================
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Draws the Dragon Aspect armour, helm and aura on top of one pawn, following them until
    /// the hediff goes. Owns no game state: if this Thing were destroyed the shout would still
    /// work, it would simply be invisible - which is what makes it safe to destroy on a whim.
    /// </summary>
    /// <summary>
    /// [StaticConstructorOnStartup] silences a startup warning, and changes nothing else.
    ///
    /// RimWorld warns about any type holding a static Graphic field, because assets must load
    /// on the main thread. Here that is a false positive - the graphics are deliberately
    /// resolved LAZILY on first draw, never at def-load, because GraphicDatabase is not safe to
    /// touch before content has loaded. The attribute marks the type as aware of the rule; it
    /// does not force the fields to populate early.
    ///
    /// Left unattributed it emits a yellow line on every startup, and CLAUDE.md does not allow
    /// warnings to sit unexplained.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Thing_DragonAspectOverlay : Thing
    {
        private Pawn target;
        private int level = 1;

        /// <summary>
        /// WHICH hediff keeps this overlay alive. Defaults to Dragon Aspect, because that is
        /// what it was built for.
        ///
        /// It has to be a field rather than a hardcoded def because the Ancient Dragonborn
        /// wears the same armour while carrying Dovahkiin_AncientDragonborn instead. With the
        /// def hardcoded, StillValid() failed on its first rare tick and the summon's armour,
        /// helm and aura all vanished about four seconds after he arrived while he walked on
        /// without them. Found in playtest.
        /// </summary>
        private HediffDef watchedHediff;

        /// <summary>
        /// Draw the spectral axe as part of the overlay.
        ///
        /// Needed because RimWorld does NOT render a pawn's weapon unless they are carrying it
        /// openly - PawnRenderer.DrawEquipment gates on CarryWeaponOpenly(), which is false for
        /// an undrafted pawn going about its business. The summon is autonomous and never
        /// drafted, so his axe was equipped, functional, and invisible except mid-swing.
        /// Drawing it here also keeps it off the pawn render path entirely, which is the same
        /// reason this whole overlay is a follower Thing.
        /// </summary>
        private bool drawAxe;

        // Real time, not ticks: the aura animates while the game is paused, as the shout icons
        // and the Thu'um bar do. Seeded from spawn so two overlays never pulse in lockstep.
        private float phaseOffset;

        // --- geometry ---
        //
        // The armour is NOT drawn at a fixed size. It borrows the pawn's own body overlay mesh
        // via PawnRenderer.GetBodyOverlayMeshSet(), which is public and is the same mesh
        // firefoam and wounds use to paint onto a body.
        //
        // Hardcoding 1.5 was the first version's bug. MeshPool.HumanlikeBodyWidth is indeed
        // 1.5, but that is only the DEFAULT: MeshPool also holds humanlikeBodySet_Male,
        // _Female, _Hulk, _Fat, _Thin and a humanlikeMeshSet_Custom dictionary that body mods
        // populate. On a pawn whose body type or mod uses a different width, a fixed 1.5
        // overlay came out smaller than the pawn - the helm ended up smaller than the head and
        // the shoulder fins sat inside the body outline.
        //
        // Everything below is expressed as a FRACTION of that mesh, so it all scales together
        // whatever size the pawn turns out to be.
        private const float RefBodyWidth = 1.5f;   // what the art was drawn against

        // Ported from the preview the user signed off. Pixel figures there were relative to a
        // 232px pawn; these are those divided by 232, then by RefBodyWidth to become fractions.
        private const float OrbitInnerFrac = 0.159f;
        private const float OrbitOuterFrac = 0.241f;
        private const float FlareInnerFrac = 0.229f;
        private const float FlareOuterFrac = 0.311f;
        private const float RingAzureFrac  = 1.276f;
        private const float RingEmberFrac  = 0.897f;

        private const float LoopSeconds = 3.4f;   // one full pass of the particle cycle

        // Must stay in step with the palette at the top of Tools/GenerateDragonAspect.ps1 -
        // these tint the aura, that generates the armour, and the two are meant to read as one
        // object. Deepened from (255,150,56) and (120,190,255) after the first playtest, where
        // the lighter pair washed out against lit terrain.
        private static readonly Color Ember = new Color(240f / 255f, 118f / 255f, 28f / 255f);
        private static readonly Color Azure = new Color(72f / 255f, 152f / 255f, 238f / 255f);

        // --- cached graphics. Built on first draw, never at def-load: GraphicDatabase is not
        // safe to touch before the game has loaded its content. ---
        // ONE ARMOUR TEXTURE SET PER BODY TYPE, keyed by BodyTypeDef defName.
        //
        // The five silhouettes are different SHAPES, not different sizes, so a single traced
        // set fits exactly one of them. Measured off the body sprites: Male is widest at the
        // shoulders (102px) and tapers; Female is narrow-shouldered (74px), pinches to a 60px
        // waist and is widest at the hips; Thin is a straight 52px tube; Fat reaches 162px at
        // the belly; Hulk is 58px taller than Male. No scale factor reconciles those.
        //
        // Anything with no art of its own - a modded body type, or Biotech's Child and Baby -
        // falls back to Male. That is the wrong shape but the right size, and it never renders
        // as a missing-texture square.
        private static readonly string[] BodyTypeKeys = { "Male", "Female", "Thin", "Fat", "Hulk" };
        private const string FallbackBodyType = "Male";

        private static Dictionary<string, Graphic> bodyL1;
        private static Dictionary<string, Graphic> bodyL2;
        private static Graphic helm;
        private static Graphic ringGraphic;
        private static Graphic axeGraphic;

        /// <summary>World-unit size of the axe, read off whichever ThingDef supplies it.</summary>
        private static float axeDrawSize = 1f;
        private static Graphic flareBlend;
        private static Graphic flareEmber;
        private static Graphic flareAzure;

        private const string TexRoot = "Things/Pawn/DragonAspect/";

        // ---------------------------------------------------------------------------------
        // A SECOND WEARER: Call of Valor.
        //
        // His 36 textures are the SAME FILENAMES as Dragon Aspect's - they come out of the same
        // generator with the palette swapped - and live in Textures/Things/Pawn/CallOfValor/.
        // Only the folder distinguishes the two characters, which is why this is a root rather
        // than a set of new names.
        //
        // The graphics above are STATIC, one shared set, so they cannot simply be re-pointed
        // per instance. Rather than convert every draw site in this file to read through an
        // object - a large edit to a signed-off, playtested file, with one missed site enough to
        // mix the two characters' armour - the statics are treated as "the currently bound set"
        // and REBOUND from a cache at the top of each draw. Drawing is synchronous and
        // single-threaded, so a bind that lasts one DrawAt is safe, and the cache means
        // GraphicDatabase is hit once per root rather than once per frame.
        private sealed class OverlaySet
        {
            public Dictionary<string, Graphic> BodyL1;
            public Dictionary<string, Graphic> BodyL2;
            public Graphic Helm;
            public Graphic Ring;
            public Graphic FlareBlend;
            public Graphic FlareEmber;
        }
        private static readonly Dictionary<string, OverlaySet> setsByRoot =
            new Dictionary<string, OverlaySet>();
        private static string boundRoot;

        /// <summary>
        /// Which texture folder this wearer's armour comes from. Null means Dragon Aspect's own,
        /// so every overlay written before Call of Valor existed keeps working unchanged and an
        /// old save loads with the right art.
        /// </summary>
        private string texRootOverride;

        /// <summary>
        /// Which weapon to draw, when <see cref="drawAxe"/> is set. Null means the Ancient
        /// Dragonborn's axe, which is what this field replaced.
        ///
        /// It must be a def rather than a hardcoded lookup because the two summons carry
        /// different weapons, and because the draw SIZE has to come from the def that is
        /// actually equipped - a hardcoded size gave two very different apparent sizes for two
        /// textures that fill their frames differently, which cost a round once already.
        /// </summary>
        private ThingDef weaponDefOverride;

        /// <summary>
        /// Draw the aura - the underglow rings and the crescent particles.
        ///
        /// FALSE FOR CALL OF VALOR, and that is the user's explicit rule rather than a
        /// preference: the aura is the Dovahkiin's signature, and giving it to a hero of
        /// Sovngarde is the same mistake as giving him her geometry. Defaults true so nothing
        /// that existed before him changes.
        /// </summary>
        private bool drawAura = true;

        private string ActiveTexRoot
        {
            get { return string.IsNullOrEmpty(texRootOverride) ? TexRoot : texRootOverride; }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                phaseOffset = Rand.Value * LoopSeconds;
            }
        }

        /// <summary>
        /// Attach to a pawn. Level is the words known, 1 to 3.
        ///
        /// <paramref name="watch"/> is the hediff whose presence keeps this overlay alive;
        /// null means Dragon Aspect. <paramref name="withAxe"/> draws the spectral greataxe,
        /// which only the Ancient Dragonborn wants.
        /// </summary>
        public void Attach(Pawn pawn, int shoutLevel, HediffDef watch = null, bool withAxe = false)
        {
            target = pawn;
            level = Mathf.Clamp(shoutLevel, 1, 3);
            watchedHediff = watch != null ? watch : DovahkiinDefOf.Dovahkiin_DragonAspect;
            drawAxe = withAxe;
        }

        /// <summary>
        /// Attach a DIFFERENT wearer's armour - Call of Valor's spectral set rather than the
        /// Dovahkiin's own, his greatsword rather than the axe, and no aura.
        ///
        /// Deliberately a second method rather than four more optional parameters on the one
        /// above: that one is called from two places that must keep behaving exactly as they
        /// did, and optional parameters make a silent behaviour change one typo away.
        /// </summary>
        public void AttachAs(Pawn pawn, int shoutLevel, HediffDef watch, string texRoot,
            ThingDef weapon, bool withAura)
        {
            Attach(pawn, shoutLevel, watch, weapon != null);
            texRootOverride = texRoot;
            weaponDefOverride = weapon;
            drawAura = withAura;
        }

        /// <summary>
        /// Rare rather than Normal: this only needs to notice that the hediff has gone, which
        /// is not a per-tick question. CLAUDE.md forbids per-tick work that could be TickRare,
        /// and RocketMan is installed.
        /// </summary>
        public override void TickRare()
        {
            // DO NOT call base.TickRare(). Verse.Thing.Tick, TickRare and TickLong are all
            // six-byte stubs that THROW NotImplementedException - verified by reading their
            // IL. Calling base here threw every 250 ticks, which spammed the log and, worse,
            // meant this overlay never reached its own cleanup and outlived the hediff.
            if (!StillValid())
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private bool StillValid()
        {
            if (target == null || target.Destroyed || !target.Spawned || target.Map != Map)
            {
                return false;
            }
            // Whatever hediff we were told to watch - see the field. A save written before
            // that field existed comes back null, so fall back to Dragon Aspect rather than
            // treating the overlay as orphaned and deleting a perfectly good one on load.
            HediffDef watch = watchedHediff != null
                ? watchedHediff
                : DovahkiinDefOf.Dovahkiin_DragonAspect;
            if (target.health == null || target.health.hediffSet == null || watch == null)
            {
                return false;
            }
            return target.health.hediffSet.GetFirstHediffOfDef(watch) != null;
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // drawLoc is this Thing's own cell, which is meaningless - we ride the PAWN.
            if (!StillValid())
            {
                return;
            }
            EnsureGraphics();

            PawnRenderer renderer = target.Drawer.renderer;
            if (renderer == null)
            {
                return;
            }

            Vector3 basePos = target.Drawer.DrawPos;
            basePos.y = AltitudeLayer.PawnState.AltitudeFor();
            Rot4 rot = target.Rotation;

            // THE PAWN'S OWN BODY ANGLE. Zero standing, ~90 degrees lying down.
            //
            // Reported by the user: the Dovahkiin went down and her armour stayed STANDING
            // over her. Everything here used to draw at Quaternion.identity, which is upright
            // and only upright - so the moment RimWorld laid the body over, the overlay
            // carried on as if nothing had happened.
            //
            // This was never a regression. `Thing_DragonAspectOverlay` was last touched at
            // fe33e61, before any of the art work, and no C# in the mod has changed since.
            // The gap has been here since the overlay was written; it simply needed the
            // Dovahkiin to be downed WITH Dragon Aspect up for anyone to see it.
            //
            // Verse.PawnRenderer.BodyAngle() is public and returns a float - verified by
            // reflection over 1.4's own Assembly-CSharp, not assumed. It is the same value
            // PawnRenderer uses to lay the body down, so borrowing it means the armour and
            // the body can never disagree about which way is up. Same reasoning as taking the
            // body's mesh from GetHumanlikeBodySetForPawn rather than inventing a size: when
            // the engine already computes the number, use ITS number.
            float bodyAngle = renderer.BodyAngle();
            Quaternion bodyQuat = Quaternion.AngleAxis(bodyAngle, Vector3.up);

            // THE MESH THE BODY ITSELF IS DRAWN ON. Not GetBodyOverlayMeshSet().
            //
            // GetBodyOverlayMeshSet() looks like the right call and is not: it returns the
            // per-body-type sets, which are DELIBERATELY INSET because they exist for wounds
            // and firefoam, and those are meant to sit inside the silhouette. Read out of
            // MeshPool..cctor IL: humanlikeBodySet_Male is 1.3x1.3, _Female 1.3x1.4,
            // _Thin 1.2x1.4, _Fat 1.6x1.4, _Hulk 1.5x1.65 - while the BODY is drawn on
            // humanlikeBodySet at 1.5x1.5. Borrowing the overlay set drew this armour at 87%
            // of the pawn, which the user correctly reported as the armour being *inside*
            // their colonist.
            //
            // PawnRenderer.DrawPawnBody calls GetHumanlikeBodySetForPawn, so calling it here
            // means the armour and the body are drawn on the same quad by construction. It
            // also handles Biotech children for free: it diverts to MeshPool.GetMeshSetForWidth
            // when the pawn's life stage carries a body-width override.
            GraphicMeshSet meshSet = HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(target);
            if (meshSet == null)
            {
                return;
            }
            Mesh bodyMesh = meshSet.MeshAt(rot);

            // Everything else is scaled off the pawn's real body width, so the aura grows and
            // shrinks with the armour rather than drifting out of proportion.
            float scale = BodyScaleOf(target, bodyMesh);

            // --- the armour itself, matched to the pawn's facing, size AND body type ---
            Graphic body = BodyArmourFor(target, level);
            if (body != null)
            {
                Graphics.DrawMesh(bodyMesh, basePos, bodyQuat,
                    body.MatAt(rot, this), 0);
            }

            // The spectral axe, drawn by us. See the drawAxe field for why RimWorld will not.
            // Placed at the pawn's side and angled, roughly where a carried weapon sits, and
            // BEHIND the pawn when they face north so it does not cover their back.
            //
            // ONLY WHEN THE GAME IS NOT ALREADY DRAWING IT. RimWorld skips the weapon for an
            // undrafted pawn, which is the whole reason this draw exists - but the moment he
            // engages, his job becomes AttackMelee, whose JobDef sets alwaysShowWeapon, so
            // CarryWeaponOpenly() flips true and DrawEquipment starts drawing it as well.
            // Drawing unconditionally therefore put TWO axes on him mid-fight: Melee Animation's
            // swinging one plus our static one. Found in playtest.
            if (drawAxe && axeGraphic != null && !GameAlreadyDrawsWeapon(target))
            {
                // HOLD ANGLES. The weapon art runs bottom-left to top-right, so its head points
                // up-and-right at about 48 degrees above the horizontal. The drawn direction is
                // therefore (angle - 48) measured clockwise, and the old 145 gave +97 - head
                // pointing at the GROUND with the pommel in the air. That went unnoticed while
                // the weapon was a near-symmetric halberd; the dragonbone axe has a ring pommel
                // at one end and a big blade at the other, so it reads as dragging it.
                //
                // -70 gives -118: head up and back, a shouldered greataxe. That is the pose the
                // user reviewed and approved. West and north are set to comparable poses by the
                // same arithmetic, but were NOT previewed - like the offsets beside them they
                // are eyeballed, and the test script asks for all four facings to be checked.
                // Built as a LOCAL offset and then rotated by the body, for the same reason the
                // helm's head offset is: these numbers say "out to his right, slightly back",
                // which is only true while he is standing. Added to basePos directly they would
                // keep the weapon out to the screen's right while its owner lay sideways.
                Vector3 axeLocal = Vector3.zero;
                float axeAngle;
                float axeAltitude;
                if (rot == Rot4.North)
                {
                    axeAltitude = AltitudeLayer.PawnState.AltitudeFor() - 0.006f;
                    axeLocal.x = -0.34f * scale / RefBodyWidth;
                    axeAngle = -62f;
                }
                else if (rot == Rot4.West)
                {
                    axeAltitude = AltitudeLayer.PawnState.AltitudeFor() + 0.006f;
                    axeLocal.x = -0.30f * scale / RefBodyWidth;
                    axeAngle = -10f;
                }
                else
                {
                    axeAltitude = AltitudeLayer.PawnState.AltitudeFor() + 0.006f;
                    axeLocal.x = 0.34f * scale / RefBodyWidth;
                    axeAngle = -70f;
                }
                axeLocal.z = -0.06f * scale / RefBodyWidth;
                // bodyQuat turns about Y, so it never touches the altitude - set that after.
                Vector3 axePos = basePos + (bodyQuat * axeLocal);
                axePos.y = axeAltitude;
                DrawQuad(axeGraphic, axePos, axeDrawSize * scale / RefBodyWidth,
                    axeAngle + bodyAngle, false, Color.white, 1f);
            }

            if (level < 3)
            {
                return;
            }

            // --- the helm, at the pawn's REAL head position ---
            // BaseHeadOffsetAt is public on PawnRenderer, so the helm follows the head rather
            // than sitting at a guessed offset above the body. It is drawn on the SAME body
            // mesh: the helm art is sized inside its frame to match a head, so sharing the
            // body's mesh keeps helm and armour in proportion on any pawn.
            if (helm != null)
            {
                // THE OFFSET HAS TO BE ROTATED TOO, not just the helm.
                //
                // BaseHeadOffsetAt returns the head's position in the pawn's OWN space -
                // "up from the chest" - so on a downed pawn that vector still points up the
                // screen while the body it belongs to is lying sideways. Rotating the helm
                // without rotating its offset would leave a correctly-tilted helm floating
                // above her chest instead of on her head.
                Vector3 headPos = basePos + (bodyQuat * renderer.BaseHeadOffsetAt(rot));
                headPos.y = AltitudeLayer.PawnState.AltitudeFor() + 0.005f;
                Graphics.DrawMesh(bodyMesh, headPos, bodyQuat,
                    helm.MatAt(rot, this), 0);
            }

            // NO AURA FOR CALL OF VALOR. The user's explicit rule, not a preference: the aura is
            // the Dovahkiin's signature, and giving it to a hero of Sovngarde is the same
            // mistake as giving him her geometry - which is what the whole design brief exists
            // to correct. Gated here, at the single call site, rather than inside DrawAura,
            // so "does this wearer have an aura" is decided in exactly one place.
            if (drawAura)
            {
                DrawAura(basePos, scale);
            }
        }

        /// <summary>
        /// Two bands of constant underglow plus the winking crescents. The whole cycle is
        /// driven from real time so it keeps moving while the game is paused.
        /// </summary>
        private void DrawAura(Vector3 basePos, float scale)
        {
            float t = ((Time.realtimeSinceStartup + phaseOffset) % LoopSeconds) / LoopSeconds;
            float twoPi = Mathf.PI * 2f;

            Vector3 ringPos = basePos;
            ringPos.y = AltitudeLayer.PawnState.AltitudeFor() - 0.004f;   // behind the pawn

            if (ringGraphic != null)
            {
                DrawQuad(ringGraphic, ringPos,
                    RingAzureFrac * scale * (1f + 0.05f * Mathf.Sin(twoPi * t)),
                    0f, false, Azure, 0.72f + 0.28f * Mathf.Sin(twoPi * t));
                DrawQuad(ringGraphic, ringPos,
                    RingEmberFrac * scale * (1f + 0.06f * Mathf.Sin(twoPi * t + Mathf.PI)),
                    0f, false, Ember, 0.62f + 0.26f * Mathf.Sin(twoPi * t + Mathf.PI));
            }

            for (int i = 0; i < Slots.GetLength(0); i++)
            {
                int k = (int)Slots[i, 0];
                float phase = Slots[i, 1];
                float window = Slots[i, 2];
                bool outward = Slots[i, 3] > 0.5f;

                float pos = t * k + phase;
                float u = pos - Mathf.Floor(pos);
                if (u >= window)
                {
                    continue;
                }
                int cycle = ((int)Mathf.Floor(pos)) % k;

                float life = u / window;
                float vis = Mathf.Sin(Mathf.PI * life);
                vis *= Mathf.Sqrt(vis);
                if (vis <= 0.02f)
                {
                    continue;
                }

                // Every appearance re-rolls everything. Rolling per SLOT instead was what made
                // the effect read as a rota: each slot always came back at its own fixed angle.
                float hAng  = Hash01(i, cycle, 1);
                float hRow  = Hash01(i, cycle, 2);
                float hCol  = Hash01(i, cycle, 20);
                float hMir  = Hash01(i, cycle, 4);
                float hDir  = Hash01(i, cycle, 5);
                float hSize = Hash01(i, cycle, 6);
                float hSpin = Hash01(i, cycle, 7);
                float hTumb = Hash01(i, cycle, 8);

                float angle = hAng * 360f + (hDir - 0.5f) * 90f * life;
                float spin = outward
                    ? -90f + (hSpin - 0.5f) * 64f + (hTumb - 0.5f) * 40f * life
                    : hSpin * 360f + (hTumb - 0.5f) * 70f * life;

                bool outer = hRow > 0.5f;
                float orbit = (outer ? OrbitOuterFrac : OrbitInnerFrac) * scale
                    * (0.94f + 0.12f * life);
                float size = (outer ? FlareOuterFrac : FlareInnerFrac) * scale
                    * (0.90f + hSize * 0.22f) * (0.92f + 0.14f * life);

                // Half blended, a quarter flat ember, a quarter flat azure. The blended sprite
                // carries its own colours and is drawn WHITE - tinting it would multiply one
                // end of its gradient into mud.
                Graphic g;
                Color tint;
                if (hCol < 0.50f) { g = flareBlend; tint = Color.white; }
                else if (hCol < 0.75f) { g = flareEmber; tint = Ember; }
                else { g = flareAzure; tint = Azure; }
                if (g == null)
                {
                    continue;
                }

                float rad = angle * Mathf.Deg2Rad;
                Vector3 p = basePos;
                p.x += Mathf.Sin(rad) * orbit;
                p.z += Mathf.Cos(rad) * orbit;
                p.y = AltitudeLayer.PawnState.AltitudeFor() + 0.01f;

                DrawQuad(g, p, size, spin, hMir > 0.5f, tint, vis);
            }
        }

        /// <summary>
        /// One tinted, rotated, optionally mirrored quad. Mirroring is a negative X scale in
        /// the transform rather than a second texture - one sprite covers both handednesses.
        /// </summary>
        private static void DrawQuad(Graphic g, Vector3 pos, float size, float angleDeg,
            bool mirror, Color tint, float alpha)
        {
            if (g == null || alpha <= 0.01f)
            {
                return;
            }
            Material mat = g.MatSingle;
            if (mat == null)
            {
                return;
            }
            Vector3 scale = new Vector3(mirror ? -size : size, 1f, size);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), scale);

            Color c = tint;
            c.a = Mathf.Clamp01(alpha);
            MaterialPropertyBlock block = PropBlock;
            block.Clear();
            block.SetColor(ShaderPropertyIDs.Color, c);
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
        /// Is the GAME already drawing this pawn's weapon? If it is, we must not draw ours too.
        ///
        /// This mirrors the PRIVATE <c>Verse.PawnRenderer.CarryWeaponOpenly()</c>, which is the
        /// exact gate <c>PawnRenderer.DrawEquipment</c> uses. Reimplemented rather than reflected
        /// because **every member it touches is public** - each one verified against 1.4's
        /// Assembly-CSharp before this was written - so there is no private-member dependency and
        /// no per-frame reflection cost.
        ///
        /// Read straight out of that method's IL, in its own order:
        ///   carrying something -> false, Drafted -> true, CurJob.def.alwaysShowWeapon -> true,
        ///   duty.def.alwaysShowWeapon -> true, Lord.LordJob.AlwaysShowWeapon -> true, else false.
        ///
        /// The one that matters here is <c>CurJob.def.alwaysShowWeapon</c>: vanilla sets it on
        /// **AttackMelee**, **AttackStatic** and **Wait_Combat**, which is every state the summon
        /// is in during a fight. Idle, following or wandering it is false, and we draw.
        ///
        /// Mirroring vanilla rather than testing for "is he fighting" ourselves is deliberate: the
        /// two conditions then cannot disagree, so there is no frame where both draw and none
        /// where neither does.
        /// </summary>
        private static bool GameAlreadyDrawsWeapon(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
            {
                return false;
            }
            if (pawn.Drafted)
            {
                return true;
            }
            if (pawn.CurJob != null && pawn.CurJob.def != null && pawn.CurJob.def.alwaysShowWeapon)
            {
                return true;
            }
            if (pawn.mindState != null && pawn.mindState.duty != null
                && pawn.mindState.duty.def != null && pawn.mindState.duty.def.alwaysShowWeapon)
            {
                return true;
            }
            // Fully qualified rather than adding a using for Verse.AI.Group, which this file
            // otherwise has no need of.
            Verse.AI.Group.Lord lord = Verse.AI.Group.LordUtility.GetLord(pawn);
            if (lord != null && lord.LordJob != null && lord.LordJob.AlwaysShowWeapon)
            {
                return true;
            }

            // ------------------------------------------------------------------------------
            // MELEE ANIMATION DRAWS THE WEAPON IN ONE MORE CASE THAN VANILLA DOES
            // ------------------------------------------------------------------------------
            // Mirroring CarryWeaponOpenly alone was not enough, and the user caught the
            // remainder: the axe still doubled occasionally, most visibly mid-swing facing
            // north. Read out of Melee Animation's own IL:
            //
            //   Patch_PawnRenderer_DrawEquipment.Prefix ALWAYS returns false - it suppresses
            //   vanilla's DrawEquipment entirely and draws the weapon itself.
            //
            //   IdleControllerComp.ShouldBeActive draws when CarryWeaponOpenly() is true OR
            //   when the pawn is in a Stance_Busy that has a valid focusTarg and is not
            //   neverAimWeapon - and that second branch is the melee swing and its cooldown.
            //
            // The cooldown stance outlives the attack JOB. So in the gap between the job
            // ending and the stance expiring, CarryWeaponOpenly() is false while Melee
            // Animation is still drawing - and we drew a second one on top.
            //
            // Only consulted when Melee Animation is actually loaded, so the baseline
            // environment keeps vanilla's condition exactly and cannot lose the axe for a
            // second or two after each swing.
            if (MeleeAnimationPresent && pawn.stances != null)
            {
                Stance_Busy busy = pawn.stances.curStance as Stance_Busy;
                if (busy != null && !busy.neverAimWeapon && busy.focusTarg.IsValid)
                {
                    return true;
                }
            }
            // And while a full animation is playing, its own renderer draws the weapon.
            if (MeleeAnimationIsAnimating(pawn))
            {
                return true;
            }
            return false;
        }

        // --- Melee Animation interop. Reflection with null guards, never a hard reference:
        // CLAUDE.md permits an assembly reference to Harmony and HugsLib and nothing else. ---
        private static bool maResolved;
        private static bool maPresent;
        private static System.Reflection.MethodInfo maTryGetAnimator;

        private static bool MeleeAnimationPresent
        {
            get
            {
                ResolveMeleeAnimation();
                return maPresent;
            }
        }

        private static void ResolveMeleeAnimation()
        {
            if (maResolved)
            {
                return;
            }
            maResolved = true;
            try
            {
                // AccessTools.TypeByName searches every loaded assembly, so this finds the mod
                // without naming its file or referencing it.
                System.Type idle = HarmonyLib.AccessTools.TypeByName("AM.Idle.IdleControllerComp");
                System.Type renderer = HarmonyLib.AccessTools.TypeByName("AM.AnimRenderer");
                maPresent = idle != null || renderer != null;
                if (renderer != null)
                {
                    // public static AnimRenderer TryGetAnimator(Pawn) - verified against the
                    // shipped assembly before this was written.
                    maTryGetAnimator = renderer.GetMethod("TryGetAnimator",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new System.Type[] { typeof(Pawn) }, null);
                }
                if (maPresent)
                {
                    Log.Message("[Dovahkiin] Melee Animation detected; the spectral weapon will "
                        + "stand down whenever it is drawing. Animator lookup "
                        + (maTryGetAnimator != null ? "resolved." : "NOT resolved - stance check only."));
                }
            }
            catch (System.Exception e)
            {
                // A silent fallback is indistinguishable from the bug it hides, so say so.
                maPresent = false;
                maTryGetAnimator = null;
                Log.Warning("[Dovahkiin] Could not probe Melee Animation, falling back to "
                    + "vanilla's weapon-drawing rule only: " + e.Message);
            }
        }

        private static bool MeleeAnimationIsAnimating(Pawn pawn)
        {
            ResolveMeleeAnimation();
            if (maTryGetAnimator == null || pawn == null)
            {
                return false;
            }
            try
            {
                return maTryGetAnimator.Invoke(null, new object[] { pawn }) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// How wide the pawn's body actually draws, in world units.
        ///
        /// HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn is public and static and answers
        /// this directly - it is the same number the body mesh is built from, so the aura is
        /// sized against the body rather than against whatever quad we happened to draw on.
        /// The mesh bounds remain a fallback for a modded pawn that returns something absurd.
        /// </summary>
        private static float BodyScaleOf(Pawn pawn, Mesh mesh)
        {
            if (pawn != null)
            {
                float pw = HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(pawn);
                if (pw > 0.01f && pw < 12f)
                {
                    return pw;
                }
            }
            if (mesh == null)
            {
                return RefBodyWidth;
            }
            float w = mesh.bounds.size.x;
            // A degenerate or unexpected mesh falls back to the size the art was drawn for
            // rather than collapsing the overlay to nothing.
            if (w <= 0.01f || w > 12f)
            {
                return RefBodyWidth;
            }
            return w;
        }

        /// <summary>
        /// Deterministic 0..1 hash, the same one the preview used. Deterministic matters:
        /// a reloaded save keeps its particle sequence instead of resynchronising.
        /// </summary>
        private static float Hash01(int a, int b, int c)
        {
            double x = Math.Sin(a * 12.9898 + b * 78.233 + c * 37.719) * 43758.5453;
            return (float)(x - Math.Floor(x));
        }

        // cycles per loop, phase, window, spin mode (1 = points away from the pawn).
        // Phases are SEARCHED, not chosen: hand-picked ones let slots bunch up and left frames
        // with no particle alight at all. This set holds 3 to 7, mean 5.
        private static readonly float[,] Slots = new float[,]
        {
            { 1, 0.342f, 0.30f, 0 }, { 2, 0.429f, 0.24f, 0 }, { 2, 0.153f, 0.22f, 0 },
            { 1, 0.504f, 0.28f, 0 }, { 3, 0.333f, 0.18f, 0 }, { 2, 0.169f, 0.22f, 0 },
            { 1, 0.850f, 0.32f, 0 }, { 2, 0.034f, 0.20f, 0 }, { 2, 0.834f, 0.24f, 0 },
            { 1, 0.908f, 0.26f, 0 }, { 3, 0.779f, 0.17f, 0 }, { 1, 0.855f, 0.29f, 0 },
            { 2, 0.647f, 0.21f, 0 }, { 2, 0.588f, 0.23f, 0 },
            { 2, 0.375f, 0.24f, 1 }, { 1, 0.570f, 0.28f, 1 }, { 3, 0.586f, 0.18f, 1 },
            { 2, 0.949f, 0.22f, 1 }, { 1, 0.209f, 0.26f, 1 }, { 2, 0.066f, 0.23f, 1 },
            { 3, 0.864f, 0.19f, 1 }
        };

        /// <summary>
        /// The armour set for this pawn's body type, falling back to Male for any body type we
        /// ship no art for. Never returns a graphic for a level the pawn has not reached.
        /// </summary>
        private static Graphic BodyArmourFor(Pawn pawn, int shoutLevel)
        {
            Dictionary<string, Graphic> set = shoutLevel >= 2 ? bodyL2 : bodyL1;
            if (set == null)
            {
                return null;
            }
            string key = FallbackBodyType;
            if (pawn != null && pawn.story != null && pawn.story.bodyType != null)
            {
                string defName = pawn.story.bodyType.defName;
                if (!string.IsNullOrEmpty(defName) && set.ContainsKey(defName))
                {
                    key = defName;
                }
            }
            Graphic g;
            return set.TryGetValue(key, out g) ? g : null;
        }

        /// <summary>
        /// Bind the static graphic fields to THIS wearer's texture set, loading it once per root
        /// and caching it.
        ///
        /// An INSTANCE method, which is the whole trick: the call site in DrawAt is unchanged
        /// (`EnsureGraphics();`), so not one of this file's many draw sites had to be touched to
        /// support a second wearer. This file is signed off and playtested, and one missed draw
        /// site would have put one character's armour on the other with no error anywhere.
        /// </summary>
        private void EnsureGraphics()
        {
            string root = ActiveTexRoot;
            EnsureSetBound(root);
            EnsureWeaponGraphic();
        }

        private static void EnsureSetBound(string root)
        {
            if (boundRoot == root && bodyL1 != null)
            {
                return;
            }
            OverlaySet set;
            if (!setsByRoot.TryGetValue(root, out set))
            {
                set = LoadSet(root);
                setsByRoot[root] = set;
            }
            bodyL1 = set.BodyL1;
            bodyL2 = set.BodyL2;
            helm = set.Helm;
            ringGraphic = set.Ring;
            flareBlend = set.FlareBlend;
            flareEmber = set.FlareEmber;
            flareAzure = set.FlareEmber;   // same sprite, tinted per draw
            boundRoot = root;
        }

        private static OverlaySet LoadSet(string root)
        {
            OverlaySet set = new OverlaySet();
            // Graphic_Multi wants _north/_east/_south. A missing _west is mirrored from _east
            // automatically - confirmed empirically, since the body sprites this art was sized
            // against ship exactly three files and render correctly in game.
            //
            // The drawSize passed here is irrelevant to the armour: DrawAt uses the PAWN's
            // mesh, not the graphic's. These graphics are only ever asked for their Material.
            //
            // FILENAMES ARE "DragonAspect_*" FOR BOTH WEARERS. Call of Valor's come out of the
            // same generator with the palette swapped, so only the ROOT differs. That is exactly
            // why his live in their own folder, and why nothing here may hardcode TexRoot.
            Vector2 body = new Vector2(RefBodyWidth, RefBodyWidth);
            set.BodyL1 = new Dictionary<string, Graphic>();
            set.BodyL2 = new Dictionary<string, Graphic>();
            for (int i = 0; i < BodyTypeKeys.Length; i++)
            {
                string key = BodyTypeKeys[i];
                set.BodyL1[key] = GraphicDatabase.Get<Graphic_Multi>(
                    root + "DragonAspect_L1_" + key, ShaderDatabase.Transparent, body, Color.white);
                set.BodyL2[key] = GraphicDatabase.Get<Graphic_Multi>(
                    root + "DragonAspect_L2_" + key, ShaderDatabase.Transparent, body, Color.white);
            }
            // The helm is deliberately NOT per body type: head art does not vary by body type,
            // and BaseHeadOffsetAt already moves it per type via BodyTypeDef.headOffset.
            set.Helm = GraphicDatabase.Get<Graphic_Multi>(root + "DragonAspectHelm",
                ShaderDatabase.Transparent, body, Color.white);

            // MoteGlow for the aura: it is light, not a surface, and should add rather than
            // occlude. The armour above uses Transparent because it IS a surface.
            //
            // Loaded even for a wearer who does not draw one (Call of Valor). His folder holds
            // the aura files because the generator emits them, and loading three graphics that
            // are never drawn costs nothing - while making the load conditional would put a
            // second place where "does he have an aura" is decided.
            Vector2 one = Vector2.one;
            set.Ring = GraphicDatabase.Get<Graphic_Single>(root + "DragonAspectAuraRing",
                ShaderDatabase.MoteGlow, one, Color.white);
            set.FlareBlend = GraphicDatabase.Get<Graphic_Single>(root + "DragonAspectFlare",
                ShaderDatabase.MoteGlow, one, Color.white);
            set.FlareEmber = GraphicDatabase.Get<Graphic_Single>(root + "DragonAspectFlarePlain",
                ShaderDatabase.MoteGlow, one, Color.white);
            return set;
        }

        /// <summary>
        /// The drawn weapon, resolved from whichever def this wearer actually carries.
        ///
        /// Per instance rather than once statically, because the two summons carry different
        /// weapons - and the SIZE has to come from the equipped def too. A hardcoded draw size
        /// gave two very different apparent sizes for two textures that fill their frames
        /// differently, which cost a playtest round on the axe.
        /// </summary>
        private void EnsureWeaponGraphic()
        {
            ThingDef equippedAxe = weaponDefOverride != null
                ? weaponDefOverride
                : DovahkiinDefOf.Dovahkiin_AncientDragonbornAxe;
            if (equippedAxe != null && equippedAxe.graphicData != null)
            {
                axeGraphic = equippedAxe.graphicData.Graphic;
                // Take the DEF'S OWN draw size rather than a hardcoded one. The two axes fill
                // their frames very differently - Medieval Overhaul's texture reaches the
                // frame edges while ours has margin around it - so one number drawn for both
                // gives two noticeably different apparent sizes. Reading it per def means each
                // appears at the size its author intended.
                float declared = equippedAxe.graphicData.drawSize.x;
                axeDrawSize = declared > 0.05f ? declared : 1f;
            }
            // The aura graphics used to be loaded here, from the hardcoded TexRoot. They are now
            // part of the per-root set in LoadSet - leaving them here would have re-pointed the
            // aura at Dragon Aspect's folder on EVERY draw, immediately after the correct set
            // had been bound, and silently undone the binding for three of its six graphics.
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref phaseOffset, "phaseOffset", 0f);
            Scribe_Defs.Look(ref watchedHediff, "watchedHediff");
            Scribe_Values.Look(ref drawAxe, "drawAxe", false);
            // Call of Valor's three. Defaults chosen so a save written before he existed loads
            // as the Dovahkiin's own armour, her aura, and the axe - i.e. unchanged.
            Scribe_Values.Look(ref texRootOverride, "texRootOverride", null);
            Scribe_Defs.Look(ref weaponDefOverride, "weaponDefOverride");
            Scribe_Values.Look(ref drawAura, "drawAura", true);
        }
    }
}
