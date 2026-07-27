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

        private const int GradientSteps = 24;
        private static Texture2D[] gradientCache;

        private static readonly Color FullColor = new Color(0.95f, 0.55f, 0.15f); // ember orange
        private static readonly Color EmptyColor = new Color(0.42f, 0.22f, 0.62f); // deep violet

        /// <summary>
        /// Textures are cached in fixed steps and generated once. Creating a texture per frame
        /// would leak GPU memory steadily - this is drawn every time the Needs tab is open.
        ///
        /// 50/50 SPLIT, not a continuous blend. The bar was a single colour lerped across the
        /// whole range, so at any moment it was one flat shade and the "gradient" was only
        /// visible by watching it drain over time. It is now drawn in two halves - violet
        /// underneath, ember on top - which reads as a gradient at a glance, standing still.
        /// The lerp is kept but compressed into each half, so the seam is not a hard edge.
        /// </summary>
        private static Texture2D BarTextureFor(float pct)
        {
            if (gradientCache == null)
            {
                gradientCache = new Texture2D[GradientSteps + 1];
            }
            int step = Mathf.Clamp(Mathf.RoundToInt(pct * GradientSteps), 0, GradientSteps);
            if (gradientCache[step] == null)
            {
                Color c = Color.Lerp(EmptyColor, FullColor, (float)step / GradientSteps);
                gradientCache[step] = SolidColorMaterials.NewSolidColorTexture(c);
            }
            return gradientCache[step];
        }

        /// <summary>
        /// Draw the filled portion as two stacked halves rather than one flat colour.
        /// Lower half violet-leaning, upper half ember-leaning, each still shaded slightly by
        /// how full the bar is so it keeps the "cools as it empties" behaviour.
        /// </summary>
        private static void DrawSplitBar(Rect barRect, float pct)
        {
            Widgets.DrawBoxSolid(barRect, new Color(0.09f, 0.09f, 0.11f));

            float filledWidth = barRect.width * Mathf.Clamp01(pct);
            if (filledWidth <= 0f)
            {
                return;
            }
            float half = barRect.height * 0.5f;
            Color lower = Color.Lerp(EmptyColor, FullColor, Mathf.Clamp01(pct) * 0.35f);
            Color upper = Color.Lerp(EmptyColor, FullColor, 0.55f + Mathf.Clamp01(pct) * 0.45f);

            Widgets.DrawBoxSolid(
                new Rect(barRect.x, barRect.y + half, filledWidth, barRect.height - half), lower);
            Widgets.DrawBoxSolid(
                new Rect(barRect.x, barRect.y, filledWidth, half), upper);
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
            float pct = CurLevelPercentage;
            DrawSplitBar(barRect, pct);
        }
    }
}
