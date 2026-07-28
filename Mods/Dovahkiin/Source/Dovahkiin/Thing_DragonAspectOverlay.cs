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
    public class Thing_DragonAspectOverlay : Thing
    {
        private Pawn target;
        private int level = 1;

        // Real time, not ticks: the aura animates while the game is paused, as the shout icons
        // and the Thu'um bar do. Seeded from spawn so two overlays never pulse in lockstep.
        private float phaseOffset;

        // --- geometry, all in world units. A human body sprite draws at ~1.5. ---
        private const float BodyDrawSize = 1.5f;
        private const float HelmDrawSize = 0.93f;

        // Ported from the preview that the user signed off. Pixel figures there were relative
        // to a 232px pawn; these are those divided by 232 and multiplied by BodyDrawSize.
        private const float OrbitInner = 0.239f;
        private const float OrbitOuter = 0.362f;
        private const float FlareInner = 0.343f;
        private const float FlareOuter = 0.466f;
        private const float RingAzure  = 1.914f;
        private const float RingEmber  = 1.345f;

        private const float LoopSeconds = 3.4f;   // one full pass of the particle cycle

        private static readonly Color Ember = new Color(1f, 0.588f, 0.220f);
        private static readonly Color Azure = new Color(0.470f, 0.745f, 1f);

        // --- cached graphics. Built on first draw, never at def-load: GraphicDatabase is not
        // safe to touch before the game has loaded its content. ---
        private static Graphic bodyL1;
        private static Graphic bodyL2;
        private static Graphic helm;
        private static Graphic ringGraphic;
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

        /// <summary>Attach to a pawn. Level is the words known, 1 to 3.</summary>
        public void Attach(Pawn pawn, int shoutLevel)
        {
            target = pawn;
            level = Mathf.Clamp(shoutLevel, 1, 3);
        }

        /// <summary>
        /// Rare rather than Normal: this only needs to notice that the hediff has gone, which
        /// is not a per-tick question. CLAUDE.md forbids per-tick work that could be TickRare,
        /// and RocketMan is installed.
        /// </summary>
        public override void TickRare()
        {
            base.TickRare();
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
            if (target.health == null || target.health.hediffSet == null
                || DovahkiinDefOf.Dovahkiin_DragonAspect == null)
            {
                return false;
            }
            return target.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DragonAspect) != null;
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // drawLoc is this Thing's own cell, which is meaningless - we ride the PAWN.
            if (!StillValid())
            {
                return;
            }
            EnsureGraphics();

            Vector3 basePos = target.Drawer.DrawPos;
            basePos.y = AltitudeLayer.PawnState.AltitudeFor();
            Rot4 rot = target.Rotation;

            // --- the armour itself, matched to the pawn's facing ---
            Graphic body = level >= 2 ? bodyL2 : bodyL1;
            if (body != null)
            {
                Graphics.DrawMesh(body.MeshAt(rot), basePos, Quaternion.identity,
                    body.MatAt(rot, this), 0);
            }

            if (level < 3)
            {
                return;
            }

            // --- the helm, at the pawn's REAL head position ---
            // BaseHeadOffsetAt is public on PawnRenderer, so the helm follows the head rather
            // than being pinned at a guessed offset above the body.
            if (helm != null && target.Drawer.renderer != null)
            {
                Vector3 headPos = basePos + target.Drawer.renderer.BaseHeadOffsetAt(rot);
                headPos.y = AltitudeLayer.PawnState.AltitudeFor() + 0.005f;
                Graphics.DrawMesh(helm.MeshAt(rot), headPos, Quaternion.identity,
                    helm.MatAt(rot, this), 0);
            }

            DrawAura(basePos);
        }

        /// <summary>
        /// Two bands of constant underglow plus the winking crescents. The whole cycle is
        /// driven from real time so it keeps moving while the game is paused.
        /// </summary>
        private void DrawAura(Vector3 basePos)
        {
            float t = ((Time.realtimeSinceStartup + phaseOffset) % LoopSeconds) / LoopSeconds;
            float twoPi = Mathf.PI * 2f;

            Vector3 ringPos = basePos;
            ringPos.y = AltitudeLayer.PawnState.AltitudeFor() - 0.004f;   // behind the pawn

            if (ringGraphic != null)
            {
                DrawQuad(ringGraphic, ringPos, RingAzure * (1f + 0.05f * Mathf.Sin(twoPi * t)),
                    0f, false, Azure, 0.72f + 0.28f * Mathf.Sin(twoPi * t));
                DrawQuad(ringGraphic, ringPos, RingEmber * (1f + 0.06f * Mathf.Sin(twoPi * t + Mathf.PI)),
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
                float orbit = (outer ? OrbitOuter : OrbitInner) * (0.94f + 0.12f * life);
                float size = (outer ? FlareOuter : FlareInner)
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

        private static void EnsureGraphics()
        {
            if (bodyL1 != null)
            {
                return;
            }
            // Graphic_Multi wants _north/_east/_south. A missing _west is mirrored from _east
            // automatically - confirmed empirically, since the body sprites this art was sized
            // against ship exactly three files and render correctly in game.
            Vector2 body = new Vector2(BodyDrawSize, BodyDrawSize);
            bodyL1 = GraphicDatabase.Get<Graphic_Multi>(TexRoot + "DragonAspect_L1",
                ShaderDatabase.Transparent, body, Color.white);
            bodyL2 = GraphicDatabase.Get<Graphic_Multi>(TexRoot + "DragonAspect_L2",
                ShaderDatabase.Transparent, body, Color.white);
            helm = GraphicDatabase.Get<Graphic_Multi>(TexRoot + "DragonAspectHelm",
                ShaderDatabase.Transparent, new Vector2(HelmDrawSize, HelmDrawSize), Color.white);

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
        }
    }
}
