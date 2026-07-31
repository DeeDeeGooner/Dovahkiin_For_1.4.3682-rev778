// ============================================================================================
// CALL OF VALOR - summoning the hero of Sovngarde.
//
// Built to AncientDragonbornUtility's pattern, because that pattern is now playtested and
// signed off, and RISKS.md section 9 puts temporary pawns as the top save-corruption risk in
// the mod. The two things that pattern gets right and that must not be lost here:
//
//   1. THE DOOMED HEDIFF GOES ON EARLY. From the moment it is attached the pawn is guaranteed
//      to end itself even if everything after this line throws.
//   2. LOAD-BEARING AND DECORATIVE STEPS ARE WRAPPED SEPARATELY. The Ancient Dragonborn's first
//      playtest produced NO ALLY AT ALL because one catch-all covered both, and a weapon def
//      missing CompEquippable took the whole summon down with it. A decorative step that fails
//      must log loudly and cost only itself.
//
// WHAT DIFFERS FROM HIM, all of it deliberate and all of it from the user's spec:
//   - he arrives THROUGH A PORTAL on the target cell, not in a puff beside the caster
//   - armour 0.66 sharp / 0.33 blunt, weaker than the Ancient Dragonborn's 0.75/0.75
//   - TWICE the lifetime - derived from his field rather than written out again
//   - Unrelenting Force and Frost Breath, no fire (Hediff_CallOfValor)
//   - his own greatsword, his own armour folder, and NO AURA
// ============================================================================================
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public static class CallOfValorUtility
    {
        /// <summary>Where Call of Valor's armour textures live. See that folder's README.</summary>
        public const string ValorTexRoot = "Things/Pawn/CallOfValor/";

        /// <summary>
        /// Open a portal on <paramref name="cell"/> and bring the hero through it.
        ///
        /// The portal is opened FIRST and the pawn spawned immediately, rather than the pawn
        /// being scheduled for the portal's arrival tick. That is a deliberate simplification
        /// with a real reason behind it: a deferred spawn needs somewhere to live across those
        /// 54 ticks, and every candidate - a queued action, a second Thing, a hediff on the
        /// caster - is one more thing that can be interrupted, saved mid-flight, or orphaned by
        /// a map change. RISKS.md section 9 is about exactly that class of state. The portal
        /// still flashes at ArrivalTick, so the effect reads correctly; he is simply already
        /// standing there when it does.
        /// </summary>
        public static void TrySummon(Pawn caster, IntVec3 targetCell)
        {
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }
            PawnKindDef kind = DovahkiinDefOf.Dovahkiin_CallOfValorKind;
            HediffDef summonDef = DovahkiinDefOf.Dovahkiin_CallOfValor;
            if (kind == null || summonDef == null)
            {
                DovahkiinMod.VerboseLog("Call of Valor: defs missing, summon skipped.");
                return;
            }

            IntVec3 cell;
            if (!TryFindLandingCell(caster, targetCell, out cell))
            {
                DovahkiinMod.VerboseLog("Call of Valor: no free cell at the target.");
                return;
            }

            Pawn summon = null;
            try
            {
                summon = GenerateSummon(kind);
                if (summon == null)
                {
                    return;
                }

                GenSpawn.Spawn(summon, cell, caster.Map, WipeMode.Vanish);

                Hediff_CallOfValor doom = HediffMaker.MakeHediff(summonDef, summon)
                    as Hediff_CallOfValor;
                if (doom == null)
                {
                    Log.Error("[Dovahkiin] Dovahkiin_CallOfValor did not produce a "
                        + "Hediff_CallOfValor. Destroying the summon rather than leaving an "
                        + "immortal pawn. Check hediffClass on the def.");
                    summon.Destroy(DestroyMode.Vanish);
                    return;
                }

                // TWICE the Ancient Dragonborn's life, DERIVED rather than written out.
                //
                // The user's rule is "double his", and his own lifetime was raised from 3750 to
                // 15000 on 2026-07-31 - at which point a literal 7500 recorded elsewhere silently
                // stopped meaning what it said. Deriving it means the relationship survives the
                // next time his number moves. The explicit field still wins if it is set, so the
                // multiple is a default and not a cage.
                DovahkiinTuningDef tuning = DovahkiinTuningDef.Current;
                int lifetime = 30000;
                if (tuning != null)
                {
                    lifetime = tuning.callOfValorLifetimeTicks > 0
                        ? tuning.callOfValorLifetimeTicks
                        : tuning.ancientDragonbornLifetimeTicks * 2;
                }
                doom.Configure(lifetime, Rand.Value < 0.5f);
                summon.health.AddHediff(doom);

                GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
                if (reg != null)
                {
                    // The SAME registry list the Ancient Dragonborn uses, on purpose. Its job is
                    // to sweep strays on load - a summon that somehow outlived its hediff - and
                    // that sweep does not care which kind of summon it is looking at. A second
                    // list would be a second thing to remember to sweep.
                    reg.NotifyAncientDragonbornSummoned(summon);
                }

                // --- from here down, failures are cosmetic and must not cost the ally ---
                TryCosmetic(summon, "open portal", () => Thing_ValorPortal.Open(summon.Map, cell));
                TryCosmetic(summon, "equip greatsword", () => EquipGreatsword(summon));
                TryCosmetic(summon, "spawn armour overlay", () => SpawnArmourOverlay(summon));
            }
            catch (Exception e)
            {
                Log.Error("[Dovahkiin] Call of Valor summon failed: " + e);
                if (summon != null && !summon.Destroyed)
                {
                    Hediff_AncientDragonborn.VanishNow(summon);
                }
            }
        }

        private static void TryCosmetic(Pawn summon, string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Log.Error("[Dovahkiin] Call of Valor: '" + what + "' failed. He is still here "
                    + "and still doomed; only this detail is missing. " + e);
            }
        }

        /// <summary>
        /// The target cell if it will take him, else the nearest that will.
        ///
        /// Unlike the Ancient Dragonborn, who lands at the caster's shoulder, this one is AIMED -
        /// the portal opens where the player pointed. Falling back to a cell near the CASTER
        /// would put him somewhere the player did not ask for; the fallback stays near the
        /// TARGET so a blocked cell shifts him by a pace rather than across the fight.
        /// </summary>
        private static bool TryFindLandingCell(Pawn caster, IntVec3 targetCell, out IntVec3 cell)
        {
            Map map = caster.Map;
            IntVec3 origin = targetCell.IsValid ? targetCell : caster.Position;
            if (origin.InBounds(map) && origin.Standable(map))
            {
                cell = origin;
                return true;
            }
            if (CellFinder.TryFindRandomCellNear(origin, map, 2,
                    candidate => candidate.InBounds(map) && candidate.Standable(map), out cell))
            {
                return true;
            }
            return CellFinder.TryFindRandomCellNear(origin, map, 5,
                candidate => candidate.InBounds(map) && candidate.Standable(map), out cell);
        }

        /// <summary>
        /// A humanlike with as little attached to him as RimWorld allows.
        ///
        /// NO ECHO, unlike the Ancient Dragonborn. He wears a fallen Dovahkiin's face because he
        /// IS a shard of that soul; Call of Valor is a hero of Sovngarde answering a call, and
        /// giving him a dead colonist's face would say the opposite about who he is.
        /// </summary>
        private static Pawn GenerateSummon(PawnKindDef kind)
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

            Pawn pawn = PawnGenerator.GeneratePawn(req);
            if (pawn == null)
            {
                return null;
            }

            pawn.Name = new NameSingle("Dovahkiin_CallOfValor_Name".Translate());

            // PawnKindDef has no <skills> field in 1.4 - checked against Core rather than
            // assumed, and an unrecognised XML field is silently skipped. A randomly generated
            // pawn can roll Melee 2, which would make a legendary hero arrive and lose.
            if (pawn.skills != null)
            {
                SkillRecord melee = pawn.skills.GetSkill(SkillDefOf.Melee);
                if (melee != null)
                {
                    melee.Level = 16;
                    melee.passion = Passion.None;
                }
            }

            if (pawn.workSettings != null)
            {
                pawn.workSettings.EnableAndInitialize();
                pawn.workSettings.DisableAll();
            }
            if (pawn.playerSettings != null)
            {
                pawn.playerSettings.medCare = MedicalCareCategory.NoCare;
                pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            }
            pawn.drafter = null;
            return pawn;
        }

        private static void EquipGreatsword(Pawn summon)
        {
            ThingDef swordDef = DovahkiinDefOf.Dovahkiin_ValorGreatsword;
            if (swordDef == null || summon.equipment == null)
            {
                return;
            }
            ThingWithComps sword = ThingMaker.MakeThing(swordDef, null) as ThingWithComps;
            if (sword == null)
            {
                return;
            }
            summon.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            summon.equipment.AddEquipment(sword);
        }

        /// <summary>
        /// His armour, from HIS folder, with HIS weapon, and NO aura.
        ///
        /// AttachAs rather than Attach: all three of those differ from the Dovahkiin's, and the
        /// texture folder is the only thing separating his 36 textures from hers - they share
        /// every filename, because they come out of the same generator with the palette swapped.
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
            overlay.AttachAs(summon, 3, DovahkiinDefOf.Dovahkiin_CallOfValor, ValorTexRoot,
                DovahkiinDefOf.Dovahkiin_ValorGreatsword, false);
        }
    }
}
