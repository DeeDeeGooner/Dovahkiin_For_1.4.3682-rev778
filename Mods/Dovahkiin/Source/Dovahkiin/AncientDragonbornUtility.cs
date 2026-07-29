// Implements: Dragon Aspect's third word - creating the Ancient Dragonborn.
//
// ============================================================================================
// GENERATING A PAWN IS THE RISKY HALF. EVERY CHOICE HERE IS ABOUT LIMITING WHAT HE CAN TOUCH.
// ============================================================================================
// RISKS.md section 9 names temporary pawns as the top save-corruption risk in this mod. The
// lifetime and cleanup live in Hediff_AncientDragonborn, which is doomed by construction. This
// file's job is to make sure the pawn that hediff is attached to is as inert as possible:
//
//   - generated with no relations, no ideo, no backstory and no title, so he cannot form
//     social links, appear in an ideology role, or be remembered by anyone
//   - not recruitable, so no prisoner or conversion path can try to keep him
//   - no needs the colony has to service - he never eats, sleeps or gets a mood
//   - invisible, because the ONLY thing that should be on screen is the spectral armour
//
// If ANY step fails, the whole summon is abandoned and the pawn destroyed. A half-built summon
// is exactly the stranded pseudo-colonist the design exists to prevent, so there is no
// "carry on without that bit" path.
// ============================================================================================
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;   // PlayOneShot is an extension method on SoundDef, not a member

namespace Dovahkiin
{
    public static class AncientDragonbornUtility
    {
        /// <summary>
        /// Summon him beside the Dovahkiin. Silent no-op on any failure - Dragon Aspect's
        /// armour, stats and cooldown are already applied by the time this runs, so a failed
        /// summon costs the ally and nothing else.
        /// </summary>
        public static void TrySummon(Pawn caster)
        {
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }
            PawnKindDef kind = DovahkiinDefOf.Dovahkiin_AncientDragonbornKind;
            HediffDef summonDef = DovahkiinDefOf.Dovahkiin_AncientDragonborn;
            if (kind == null || summonDef == null)
            {
                DovahkiinMod.VerboseLog("Ancient Dragonborn: defs missing, summon skipped.");
                return;
            }

            IntVec3 cell;
            if (!TryFindLandingCell(caster, out cell))
            {
                DovahkiinMod.VerboseLog("Ancient Dragonborn: no free cell beside the caster.");
                return;
            }

            Pawn summon = null;
            try
            {
                summon = GenerateSummon(kind, caster);
                if (summon == null)
                {
                    return;
                }

                GenSpawn.Spawn(summon, cell, caster.Map, WipeMode.Vanish);

                // The doomed hediff goes on LAST but before anything can go wrong afterwards -
                // from this point the pawn is guaranteed to end itself even if we throw.
                Hediff_AncientDragonborn h = HediffMaker.MakeHediff(summonDef, summon)
                    as Hediff_AncientDragonborn;
                if (h == null)
                {
                    Log.Error("[Dovahkiin] Dovahkiin_AncientDragonborn did not produce a "
                        + "Hediff_AncientDragonborn. Destroying the summon rather than leaving "
                        + "an immortal pawn. Check hediffClass on the def.");
                    summon.Destroy(DestroyMode.Vanish);
                    return;
                }

                DovahkiinTuningDef t = DovahkiinTuningDef.Current;
                int lifetime = t != null ? t.ancientDragonbornLifetimeTicks : 3750;
                h.Configure(lifetime, Rand.Value < 0.5f);
                summon.health.AddHediff(h);

                GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
                if (reg != null)
                {
                    reg.NotifyAncientDragonbornSummoned(summon);
                }

                EquipAxe(summon);
                SpawnArmourOverlay(summon);
                DoArrivalEffect(summon);
            }
            catch (System.Exception e)
            {
                Log.Error("[Dovahkiin] Ancient Dragonborn summon failed: " + e);
                // A partially built summon is the exact hazard RISKS.md section 9 describes.
                // Better no ally than a permanent one.
                if (summon != null && !summon.Destroyed)
                {
                    Hediff_AncientDragonborn.VanishNow(summon);
                }
            }
        }

        /// <summary>
        /// A humanlike with as little attached to him as RimWorld allows. See the header for
        /// why each flag is set.
        /// </summary>
        private static Pawn GenerateSummon(PawnKindDef kind, Pawn caster)
        {
            PawnGenerationRequest req = new PawnGenerationRequest(
                kind,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1,
                true,      // forceGenerateNewPawn - never redress an existing world pawn
                false,     // allowDead
                false,     // allowDowned
                false,     // canGeneratePawnRelations - he has no family, ever
                true,      // mustBeCapableOfViolence
                0f,        // colonistRelationChanceFactor
                false,     // forceAddFreeWarmLayerIfNeeded
                true,      // allowGay
                false,     // allowPregnant
                false,     // allowFood
                false,     // allowAddictions
                false,     // inhabitant
                false,     // certainlyBeenInCryptosleep
                false,     // forceRedressWorldPawnIfFormerColonist
                false);    // worldPawnFactionDoesntMatter

            Pawn p = PawnGenerator.GeneratePawn(req);
            if (p == null)
            {
                return null;
            }

            // He is a shard of the Dovahkiin's soul, not a person. Strip everything that would
            // make the colony treat him as one.
            p.Name = new NameSingle("Dovahkiin_AncientDragonborn_Name".Translate());

            // PawnKindDef carries no <skills> field in 1.4 - checked against Core rather than
            // assumed, and an unrecognised XML field would have been silently skipped. So his
            // competence is set here instead. A randomly generated tribal can roll Melee 2,
            // which would make the rescue arrive and immediately lose.
            if (p.skills != null)
            {
                SkillRecord melee = p.skills.GetSkill(SkillDefOf.Melee);
                if (melee != null)
                {
                    melee.Level = 14;
                    melee.passion = Passion.None;
                }
            }

            if (p.workSettings != null)
            {
                p.workSettings.EnableAndInitialize();
                p.workSettings.DisableAll();
            }
            if (p.playerSettings != null)
            {
                p.playerSettings.medCare = MedicalCareCategory.NoCare;
                p.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            }
            // Not draftable and not in the colonist bar: the user asked for an autonomous ally,
            // and every extra system a temporary pawn touches is another way to strand him.
            p.drafter = null;

            return p;
        }

        /// <summary>The nearest standable cell to the caster, so he lands at their shoulder.</summary>
        private static bool TryFindLandingCell(Pawn caster, out IntVec3 cell)
        {
            Map map = caster.Map;
            IntVec3 origin = caster.Position;
            if (CellFinder.TryFindRandomCellNear(origin, map, 2,
                    c => c.InBounds(map) && c.Standable(map) && c != origin
                         && GenSight.LineOfSight(origin, c, map, true),
                    out cell))
            {
                return true;
            }
            // Widen once before giving up - a pawn fighting in a doorway has few free cells.
            return CellFinder.TryFindRandomCellNear(origin, map, 5,
                c => c.InBounds(map) && c.Standable(map) && c != origin, out cell);
        }

        private static void EquipAxe(Pawn summon)
        {
            ThingDef axeDef = DovahkiinDefOf.Dovahkiin_AncientDragonbornAxe;
            if (axeDef == null || summon.equipment == null)
            {
                return;
            }
            ThingWithComps axe = ThingMaker.MakeThing(axeDef, null) as ThingWithComps;
            if (axe == null)
            {
                return;
            }
            summon.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            summon.equipment.AddEquipment(axe);
        }

        /// <summary>
        /// The level-3 spectral armour, on him. He is invisible, so this IS what the player
        /// sees - the armour walking around on its own, which is the whole look.
        /// </summary>
        private static void SpawnArmourOverlay(Pawn summon)
        {
            ThingDef overlayDef = DovahkiinDefOf.Dovahkiin_DragonAspectOverlay;
            if (overlayDef == null)
            {
                return;
            }
            Thing_DragonAspectOverlay overlay =
                ThingMaker.MakeThing(overlayDef, null) as Thing_DragonAspectOverlay;
            if (overlay == null)
            {
                return;
            }
            GenSpawn.Spawn(overlay, summon.Position, summon.Map);
            overlay.Attach(summon, 3);
        }

        private static void DoArrivalEffect(Pawn summon)
        {
            Map map = summon.Map;
            if (map == null)
            {
                return;
            }
            for (int i = 0; i < 18; i++)
            {
                FleckMaker.ThrowDustPuffThick(
                    summon.DrawPos + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)),
                    map, Rand.Range(1.4f, 2.4f), new Color(0.55f, 0.75f, 1f, 0.7f));
            }
            if (DovahkiinVanillaDefOf.PsychicSoothePulserCast != null)
            {
                DovahkiinVanillaDefOf.PsychicSoothePulserCast.PlayOneShot(
                    new TargetInfo(summon.Position, map, false));
            }
        }
    }
}
