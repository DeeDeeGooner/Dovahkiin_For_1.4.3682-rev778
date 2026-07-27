// Implements: SPEC.md 5.2 (OD-9) - the mod's own shout resource.
//
// This is what shouts actually spend. It exists on the Dovahkiin and nobody else, it works with
// no other mods installed, and it keeps working if RimWorld of Magic is uninstalled mid-save
// (which also answers OD-6). RWoM mana/stamina grow as a *bonus* on top, never as the thing
// shouts depend on.
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class Need_Thuum : Need
    {
        public Need_Thuum(Pawn pawn) : base(pawn)
        {
        }

        /// <summary>
        /// SPEC.md 5.2: flat, linear, uncapped growth per soul. The user asked for a pool that
        /// deepens forever, and Need.MaxLevel being virtual is what makes that expressible -
        /// a vanilla Need is otherwise a fixed 0..1 bar.
        /// </summary>
        public override float MaxLevel
        {
            get
            {
                DovahkiinTuningDef t = DovahkiinTuningDef.Current;
                if (t == null)
                {
                    return 10f;
                }
                return t.thuumBaseMax + (t.thuumPerSoul * SoulCount);
            }
        }

        private int SoulCount
        {
            get
            {
                if (pawn == null || pawn.health == null || DovahkiinDefOf.Dovahkiin_DragonSoulAttunement == null)
                {
                    return 0;
                }
                Hediff_DragonSoulAttunement a = pawn.health.hediffSet
                    .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_DragonSoulAttunement)
                    as Hediff_DragonSoulAttunement;
                return a == null ? 0 : a.Souls;
            }
        }

        public override void SetInitialLevel()
        {
            CurLevel = MaxLevel;
        }

        /// <summary>
        /// Regeneration. NeedInterval runs on the need tick (roughly every 150 ticks), not every
        /// tick - CLAUDE.md forbids per-tick work and RocketMan punishes it.
        /// </summary>
        public override void NeedInterval()
        {
            if (IsFrozen)
            {
                return;
            }
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float perDay = t == null ? 12f : t.thuumRegenPerDay;

            // 150 ticks per need interval, 60000 ticks per day.
            float perInterval = perDay * (150f / GenDate.TicksPerDay) * MaxLevel;
            CurLevel = Mathf.Min(CurLevel + perInterval, MaxLevel);
        }

        public bool CanAfford(float cost)
        {
            return CurLevel >= cost;
        }

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }
            CurLevel -= cost;
            return true;
        }

        public override string GetTipString()
        {
            return "Dovahkiin_Need_Thuum_Tip".Translate(
                CurLevel.ToString("F0").Named("CUR"),
                MaxLevel.ToString("F0").Named("MAX"));
        }

        // ------------------------------------------------------------------
        // Custom bar colour: ember orange when full, fading to a cold violet when spent.
        // ------------------------------------------------------------------

        private static readonly Color FullColor = new Color(0.95f, 0.55f, 0.15f); // ember orange
        private static readonly Color EmptyColor = new Color(0.42f, 0.22f, 0.62f); // deep violet

        /// <summary>
        /// One cached gradient strip, built once and reused for every draw.
        ///
        /// A texture MUST NOT be created per frame here - this redraws continuously whenever the
        /// Needs tab is open, and a new Texture2D each time leaks GPU memory steadily.
        /// </summary>
        private static Texture2D gradientTex;

        private const int GradientWidth = 128;

        /// <summary>
        /// The bar runs violet on the LEFT to ember on the RIGHT, blending through the middle.
        ///
        /// Two earlier attempts were wrong and both are worth recording:
        ///   1. A single colour lerped by fill level. At any given moment the bar was one flat
        ///      shade, so the "gradient" only existed if you watched it drain over time.
        ///   2. Two stacked halves - which split the bar the wrong way (a horizontal seam,
        ///      top/bottom) and used two FLAT colours with a hard edge between them. What was
        ///      wanted is a vertical seam, left/right, with the colours fading into each other.
        ///
        /// Hence a real horizontal gradient. The blend is concentrated in the middle 40% so each
        /// colour still owns roughly half the bar - a "50/50 gradient" rather than a straight
        /// linear ramp, which would read as mud in the centre.
        /// </summary>
        private static Texture2D GradientTexture()
        {
            if (gradientTex != null)
            {
                return gradientTex;
            }
            gradientTex = new Texture2D(GradientWidth, 1, TextureFormat.RGBA32, false);
            gradientTex.wrapMode = TextureWrapMode.Clamp;
            gradientTex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < GradientWidth; i++)
            {
                float t = i / (float)(GradientWidth - 1);
                // Smoothstep across 0.30..0.70, so the outer thirds stay close to pure.
                float f = Mathf.Clamp01((t - 0.30f) / 0.40f);
                f = f * f * (3f - 2f * f);
                gradientTex.SetPixel(i, 0, Color.Lerp(EmptyColor, FullColor, f));
            }
            gradientTex.Apply();
            return gradientTex;
        }

        /// <summary>
        /// Draw the filled portion of the bar using the gradient strip.
        ///
        /// The gradient is anchored to the FULL bar width and then clipped to however much is
        /// filled, rather than being squashed into the filled part. That is what makes the
        /// colour mean something: a full bar reaches the ember end, and a nearly-spent one shows
        /// only the violet, so the bar visibly cools as it empties.
        /// </summary>
        private static void DrawThuumBar(Rect barRect, float pct)
        {
            Widgets.DrawBoxSolid(barRect, new Color(0.09f, 0.09f, 0.11f));

            pct = Mathf.Clamp01(pct);
            if (pct <= 0f)
            {
                return;
            }
            Rect filled = new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height);
            // texCoords takes only the left `pct` of the strip, so x maps to the same colour
            // regardless of how full the bar is.
            GUI.DrawTextureWithTexCoords(filled, GradientTexture(), new Rect(0f, 0f, pct, 1f));
        }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue,
            float customMargin = -1f, bool drawArrows = true, bool doTooltip = true,
            Rect? rectForTooltip = null, bool drawStatChanges = false)
        {
            if (rect.height > 70f)
            {
                float shrink = (rect.height - 70f) / 2f;
                rect.height = 70f;
                rect.y += shrink;
            }

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (doTooltip)
            {
                TooltipHandler.TipRegion(rectForTooltip ?? rect, new TipSignal(GetTipString, 1147));
            }

            float margin = customMargin >= 0f ? customMargin : rect.height / 5f;
            Rect labelRect = new Rect(rect.x + margin, rect.y, rect.width - margin * 2f,
                rect.height / 2f);
            Text.Font = rect.height > 55f ? GameFont.Small : GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(labelRect, LabelCap);
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(labelRect, CurLevel.ToString("F0") + " / " + MaxLevel.ToString("F0"));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect barRect = new Rect(rect.x + margin, rect.y + rect.height / 2f,
                rect.width - margin * 2f, rect.height / 2f - margin);
            DrawThuumBar(barRect, CurLevelPercentage);
        }
    }
}
