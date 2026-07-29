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
                Graphics.DrawMesh(bodyMesh, basePos, Quaternion.identity,
                    body.MatAt(rot, this), 0);
            }

            // The spectral axe, drawn by us. See the drawAxe field for why RimWorld will not.
            // Placed at the pawn's side and angled, roughly where a carried weapon sits, and
            // BEHIND the pawn when they face north so it does not cover their back.
            if (drawAxe && axeGraphic != null)
            {
                Vector3 axePos = basePos;
                float axeAngle;
                if (rot == Rot4.North)
                {
                    axePos.y = AltitudeLayer.PawnState.AltitudeFor() - 0.006f;
                    axePos.x -= 0.34f * scale / RefBodyWidth;
                    axeAngle = 205f;
                }
                else if (rot == Rot4.West)
                {
                    axePos.y = AltitudeLayer.PawnState.AltitudeFor() + 0.006f;
                    axePos.x -= 0.30f * scale / RefBodyWidth;
                    axeAngle = 200f;
                }
                else
                {
                    axePos.y = AltitudeLayer.PawnState.AltitudeFor() + 0.006f;
                    axePos.x += 0.34f * scale / RefBodyWidth;
                    axeAngle = 145f;
                }
                axePos.z -= 0.06f * scale / RefBodyWidth;
                DrawQuad(axeGraphic, axePos, axeDrawSize * scale / RefBodyWidth, axeAngle, false,
                    Color.white, 1f);
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
                Vector3 headPos = basePos + renderer.BaseHeadOffsetAt(rot);
                headPos.y = AltitudeLayer.PawnState.AltitudeFor() + 0.005f;
                Graphics.DrawMesh(bodyMesh, headPos, Quaternion.identity,
                    helm.MatAt(rot, this), 0);
            }

            DrawAura(basePos, scale);
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

        private static void EnsureGraphics()
        {
            if (bodyL1 != null)
            {
                return;
            }
            // Graphic_Multi wants _north/_east/_south. A missing _west is mirrored from _east
            // automatically - confirmed empirically, since the body sprites this art was sized
            // against ship exactly three files and render correctly in game.
            //
            // The drawSize passed here is irrelevant to the armour: DrawAt uses the PAWN's
            // mesh, not the graphic's. These graphics are only ever asked for their Material.
            Vector2 body = new Vector2(RefBodyWidth, RefBodyWidth);
            bodyL1 = new Dictionary<string, Graphic>();
            bodyL2 = new Dictionary<string, Graphic>();
            for (int i = 0; i < BodyTypeKeys.Length; i++)
            {
                string key = BodyTypeKeys[i];
                bodyL1[key] = GraphicDatabase.Get<Graphic_Multi>(
                    TexRoot + "DragonAspect_L1_" + key, ShaderDatabase.Transparent, body, Color.white);
                bodyL2[key] = GraphicDatabase.Get<Graphic_Multi>(
                    TexRoot + "DragonAspect_L2_" + key, ShaderDatabase.Transparent, body, Color.white);
            }
            // The helm is deliberately NOT per body type: head art does not vary by body type,
            // and BaseHeadOffsetAt already moves it per type via BodyTypeDef.headOffset.
            helm = GraphicDatabase.Get<Graphic_Multi>(TexRoot + "DragonAspectHelm",
                ShaderDatabase.Transparent, body, Color.white);

            // The Ancient Dragonborn's axe. Cutout, not Transparent: it is a solid object.
            //
            // Resolved from the SAME ThingDef the summon actually equips, so the drawn axe and
            // the carried one can never diverge.
            ThingDef equippedAxe = DovahkiinDefOf.Dovahkiin_AncientDragonbornAxe;
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

            // MoteGlow for the aura: it is light, not a surface, and should add rather than
            // occlude. The armour above uses Transparent because it IS a surface.
            Vector2 one = Vector2.one;
            ringGraphic = GraphicDatabase.Get<Graphic_Single>(TexRoot + "DragonAspectAuraRing",
                ShaderDatabase.MoteGlow, one, Color.white);
            flareBlend = GraphicDatabase.Get<Graphic_Single>(TexRoot + "DragonAspectFlare",
                ShaderDatabase.MoteGlow, one, Color.white);
            flareEmber = GraphicDatabase.Get<Graphic_Single>(TexRoot + "DragonAspectFlarePlain",
                ShaderDatabase.MoteGlow, one, Color.white);
            flareAzure = flareEmber;   // same sprite, tinted per draw
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref phaseOffset, "phaseOffset", 0f);
            Scribe_Defs.Look(ref watchedHediff, "watchedHediff");
            Scribe_Values.Look(ref drawAxe, "drawAxe", false);
        }
    }
}
