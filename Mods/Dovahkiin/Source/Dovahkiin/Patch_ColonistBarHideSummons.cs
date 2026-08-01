// ============================================================================================
// THE SUMMONS DO NOT BELONG IN THE COLONIST BAR.
//
// The user, after the first Call of Valor playtest: both summons "appear as naked pawn on their
// top screen icon". They do, and there are two separate things wrong with that:
//
//   1. The portrait shows the pawn underneath, not the spectral armour - because the armour is
//      a world-space Thing and a portrait is not the world. PortraitsCache renders a pawn from
//      its own graphics only, so a follower Thing cannot reach it BY CONSTRUCTION. That is the
//      price of keeping this overlay off PawnRenderer, which is what keeps it clear of
//      RocketMan (RISKS.md section 10), and it is still the right trade.
//
//   2. They should not have been up there at all. The notebook records the intent as "not
//      draftable and not in the colonist bar", and `p.drafter = null` delivered the first half
//      only - it does nothing about the bar, which lists player-faction humanlikes.
//
// The user chose to fix (2), which closes both summon complaints and leaves only the Dovahkiin's
// own portrait unchanged under Dragon Aspect. Deliberately NOT fixing (1): that needs a patch on
// the portrait render path, and this mod has avoided every render patch on purpose.
//
// WHY NOT MAKE THEM NON-COLONISTS INSTEAD, which would need no patch at all?
// Because the only lever is HostFaction. Read off the real property with a decompiler rather
// than guessed: `IsFreeColonist => IsColonist && HostFaction == null`, and
// `IsColonist => Faction != null && Faction.IsPlayer && RaceProps.Humanlike` (plus slavery).
// Setting a HostFaction turns them into GUESTS, which changes how they behave and who they
// fight for - a gameplay change to fix a cosmetic complaint. Rejected.
//
// So this patches the bar's own cache, which is as narrow as it gets: if it ever breaks, the
// worst case is that the bar looks wrong. Nothing here can touch the summons themselves.
// ============================================================================================
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Drop the mod's temporary summons out of the colonist bar's cached entries.
    ///
    /// Postfix on the PRIVATE ColonistBar.CheckRecacheEntries, reaching cachedEntries through
    /// Harmony's `___field` injection. Read out of the decompiled class rather than assumed:
    /// `Entries` is a property that calls CheckRecacheEntries() and then returns cachedEntries,
    /// so filtering in this postfix happens before any caller can see the list - including
    /// ColonistBarDrawLocsFinder, which takes its positions from that same property. Filtering
    /// anywhere later would have left the entries and the draw positions disagreeing about how
    /// many icons there are.
    ///
    /// It also runs on the early-return path, when nothing was recached. That is harmless: the
    /// list has already been filtered, so the second pass removes nothing.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class Patch_ColonistBarHideSummons
    {
        public static void Postfix(List<ColonistBar.Entry> ___cachedEntries)
        {
            if (___cachedEntries == null || ___cachedEntries.Count == 0)
            {
                return;
            }
            for (int i = ___cachedEntries.Count - 1; i >= 0; i--)
            {
                Pawn pawn = ___cachedEntries[i].pawn;
                if (pawn != null && IsTemporarySummon(pawn))
                {
                    ___cachedEntries.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Is this one of the mod's temporary summons?
        ///
        /// By PawnKindDef, which is a reference compare against two defs - this runs on a GUI
        /// path that is hit every frame the bar is visible, and CLAUDE.md forbids per-tick work
        /// where a cached lookup will do. Walking the hediff set here instead would search every
        /// colonist's whole health record every frame to answer a question their kind already
        /// answers.
        ///
        /// Null-tolerant on the defs: if a def failed to load, this simply never matches and the
        /// bar behaves exactly as vanilla does.
        /// </summary>
        private static bool IsTemporarySummon(Pawn pawn)
        {
            PawnKindDef kind = pawn.kindDef;
            if (kind == null)
            {
                return false;
            }
            return kind == DovahkiinDefOf.Dovahkiin_AncientDragonbornKind
                || kind == DovahkiinDefOf.Dovahkiin_CallOfValorKind;
        }
    }
}
