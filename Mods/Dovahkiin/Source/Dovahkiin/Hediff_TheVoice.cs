// Implements: SPEC.md 3.1, 4.1, 4.2, 5.1 - the per-pawn Voice state.
//
// Phase 1 builds the data model and its save/load only. Nothing here casts anything yet;
// shouts arrive in Phase 2, souls in Phase 3.
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Holds everything about a Dovahkiin's Voice: which shouts they have levelled, how many
    /// unspent souls they hold, and the shared Thu'um cooldown (SPEC.md 4.2).
    ///
    /// Word *discovery* is world state and lives on the registry (OD-8). Word *levels* are
    /// per-pawn and live here, which is why a second Dovahkiin re-buys them with souls.
    /// </summary>
    public class Hediff_TheVoice : HediffWithComps
    {
        /// <summary>shoutDefName -> level reached, 1..maxWordLevel. Absent means level 0.</summary>
        private Dictionary<string, int> shoutLevels = new Dictionary<string, int>();

        /// <summary>Spendable Dragon Souls. Separate from Attunement, which is never spent (SPEC.md 5.1).</summary>
        private int unspentSouls;

        /// <summary>SPEC.md 3.5: souls absorbed by a child are banked until they come of age.</summary>
        private int bankedSouls;

        /// <summary>Absolute tick the shared Thu'um cooldown ends. SPEC.md 4.2.</summary>
        private int thuumReadyTick;

        // Scribe scratch - Scribe_Collections needs concrete lists for dictionary round-trips.
        private List<string> scribeShoutKeys;
        private List<int> scribeShoutValues;

        public int UnspentSouls
        {
            get { return unspentSouls; }
        }

        public int BankedSouls
        {
            get { return bankedSouls; }
        }

        public bool ThuumReady
        {
            get { return Find.TickManager.TicksGame >= thuumReadyTick; }
        }

        /// <summary>
        /// Never auto-removed. The Voice is part of who the pawn is, not a condition that heals;
        /// leaving this to the default "remove when severity &lt;= 0" would make it fragile to
        /// anything that nudges severity.
        /// </summary>
        public override bool ShouldRemove
        {
            get { return false; }
        }

        public int GetShoutLevel(string shoutDefName)
        {
            int level;
            return shoutLevels.TryGetValue(shoutDefName, out level) ? level : 0;
        }

        public void SetShoutLevel(string shoutDefName, int level)
        {
            if (level <= 0)
            {
                shoutLevels.Remove(shoutDefName);
            }
            else
            {
                shoutLevels[shoutDefName] = level;
            }
        }

        public void GrantSouls(int count, bool banked)
        {
            if (count <= 0)
            {
                return;
            }
            if (banked)
            {
                bankedSouls += count;
            }
            else
            {
                unspentSouls += count;
            }
        }

        public bool TrySpendSouls(int count)
        {
            if (count <= 0 || unspentSouls < count)
            {
                return false;
            }
            unspentSouls -= count;
            return true;
        }

        /// <summary>SPEC.md 3.5: called when a child Dovahkiin comes of age.</summary>
        public void ReleaseBankedSouls()
        {
            unspentSouls += bankedSouls;
            bankedSouls = 0;
        }

        /// <summary>
        /// SPEC.md 4.2: strain is the anti-spam valve. Each cast adds severity to a visible,
        /// decaying VoiceStrain hediff; recovery is lengthened in proportion. Tune this rather
        /// than nerfing individual shouts.
        ///
        /// Returns the multiplier to apply to this cast's cooldown.
        /// </summary>
        public float RegisterStrainAndGetMultiplier()
        {
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            float perCast = t == null ? 0.34f : t.voiceStrainPerCast;
            float perSeverity = t == null ? 0.6f : t.voiceStrainCooldownFactor;

            if (pawn == null || pawn.health == null || DovahkiinDefOf.Dovahkiin_VoiceStrain == null)
            {
                return 1f;
            }

            Hediff strain = pawn.health.hediffSet
                .GetFirstHediffOfDef(DovahkiinDefOf.Dovahkiin_VoiceStrain);
            if (strain == null)
            {
                strain = pawn.health.AddHediff(DovahkiinDefOf.Dovahkiin_VoiceStrain);
                strain.Severity = perCast;
            }
            else
            {
                strain.Severity += perCast;
            }

            // Multiplier counts strain accrued BEFORE this cast, so the first shout in a fight
            // is never penalised.
            float prior = Mathf.Max(0f, strain.Severity - perCast);
            return 1f + (prior * perSeverity);
        }

        public void StartThuumCooldown(int ticks)
        {
            int until = Find.TickManager.TicksGame + ticks;
            if (until > thuumReadyTick)
            {
                thuumReadyTick = until;
            }
        }

        public void ClearThuumCooldown()
        {
            thuumReadyTick = 0;
        }

        /// <summary>Ticks remaining on the shared recovery, for UI. Zero when ready.</summary>
        public int ThuumCooldownRemaining
        {
            get { return Mathf.Max(0, thuumReadyTick - Find.TickManager.TicksGame); }
        }

        public override string TipStringExtra
        {
            get
            {
                // TODO(PHASE2): show known shouts and the cooldown once shouts exist.
                return "Dovahkiin_TheVoice_Tip".Translate(unspentSouls.Named("SOULS"));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref unspentSouls, "unspentSouls", 0);
            Scribe_Values.Look(ref bankedSouls, "bankedSouls", 0);
            Scribe_Values.Look(ref thuumReadyTick, "thuumReadyTick", 0);
            Scribe_Collections.Look(ref shoutLevels, "shoutLevels", LookMode.Value, LookMode.Value,
                ref scribeShoutKeys, ref scribeShoutValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && shoutLevels == null)
            {
                shoutLevels = new Dictionary<string, int>();
            }
        }
    }
}
