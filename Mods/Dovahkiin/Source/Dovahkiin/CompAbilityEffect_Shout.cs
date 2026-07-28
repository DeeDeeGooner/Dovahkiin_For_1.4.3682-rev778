// Implements: SPEC.md 4.2 (cost, shared cooldown, strain), 4.3 (casting), 4.4a (the shouts).
//
// Design: every shout is a vanilla AbilityDef whose comps list starts with a Shout comp. That
// comp owns the economy - Thu'um cost, the shared cooldown, strain, witnesses - so individual
// shout effects only have to implement what they DO. Verified in COMPAT.md that vanilla
// AbilityDef works here and needs no VEF or JecsTools dependency (RISKS.md section 2).
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Dovahkiin
{
    public class CompProperties_Shout : CompProperties_AbilityEffect
    {
        /// <summary>Which shout this ability belongs to, and at which level.</summary>
        public ShoutDef shout;
        public int level = 1;

        public CompProperties_Shout()
        {
            compClass = typeof(CompAbilityEffect_Shout);
        }
    }

    /// <summary>
    /// The economy comp. Put this FIRST in an ability's comps list; the effect comps that follow
    /// do the actual damage or utility.
    /// </summary>
    public class CompAbilityEffect_Shout : CompAbilityEffect
    {
        public new CompProperties_Shout Props
        {
            get { return (CompProperties_Shout)props; }
        }

        private Pawn Caster
        {
            get { return parent.pawn; }
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = ShoutUtility.CannotCastReason(Caster, Props.shout, Props.level);
            return reason != null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            // Charge only once the shout has actually gone off.
            ShoutUtility.NotifyShoutCast(Caster, Props.shout, Props.level);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            string reason = ShoutUtility.CannotCastReason(Caster, Props.shout, Props.level);
            return reason;
        }
    }

    // ------------------------------------------------------------------
    // Unrelenting Force - Fus Ro Dah. SPEC.md 4.4a.
    // L1 staggers one target; L2/L3 knock back a cone; L3 also briefly stuns.
    // ------------------------------------------------------------------

    public class CompProperties_ShoutKnockback : CompProperties_AbilityEffect
    {
        public float coneAngle = 0f;      // 0 = single target
        public float range = 8f;
        public float knockbackCells = 0f;
        public int stunTicks = 0;
        public float damageAmount = 0f;

        /// <summary>Split the blunt damage across this many hits, so bruising spreads.</summary>
        public int damageInstances = 1;

        /// <summary>Aim those hits with the shared core-over-extremities priority rule.</summary>
        public bool spreadDamage = false;

        public CompProperties_ShoutKnockback()
        {
            compClass = typeof(CompAbilityEffect_ShoutKnockback);
        }
    }

    public class CompAbilityEffect_ShoutKnockback : CompAbilityEffect
    {
        public new CompProperties_ShoutKnockback Props
        {
            get { return (CompProperties_ShoutKnockback)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return;
            }

            // The wave carries the hit. Cold blue-white for force - this shout has no element,
            // it is pressure. Nothing is damaged here: the front applies the payload as it
            // arrives, so victims are struck when it reaches them, not on cast.
            Thing_ShoutWave wave = Thing_ShoutWave.Spawn(caster, target.Cell,
                Props.range, Props.coneAngle,
                new Color(0.45f, 0.75f, 1f), DovahkiinDefOf.Dovahkiin_Fleck_ForceWave, 1.9f);
            if (wave != null)
            {
                wave.SetPayload(DamageDefOf.Blunt, Props.damageAmount, Props.knockbackCells,
                    Props.stunTicks, false, false, null, 1f,
                    Props.damageInstances, 0f, null, 1f, Props.spreadDamage);
            }
            SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(caster.Position, caster.Map, false));
        }
    }

    // ------------------------------------------------------------------
    // Fire Breath - Yol Toor Shul. SPEC.md 4.4a.
    // ------------------------------------------------------------------

    /// <summary>
    /// One breath-cone comp for every elemental shout. Fire and Frost differ only in data -
    /// damage def, particle def, tint and the hediff they leave behind - so they share a class
    /// rather than duplicating the cone logic.
    /// </summary>
    public class CompProperties_ShoutCone : CompProperties_AbilityEffect
    {
        public float coneAngle = 45f;
        public float range = 7f;
        public float damageAmount = 12f;
        public DamageDef damageDef;
        public bool ignitesPawns = false;
        public bool ignitesGround = false;

        /// <summary>Ice-encasing. Frost Breath at full power should hold a target in place.</summary>
        public int stunTicks = 0;

        /// <summary>Split the damage across this many hits, so it spreads over body parts.</summary>
        public int damageInstances = 1;

        /// <summary>
        /// Aim each hit with the shared core-over-extremities rule rather than letting RimWorld
        /// roll. Off for breath weapons - a torrent engulfing a target SHOULD catch a foot - but
        /// on for Cyclone, whose light bruising would otherwise just crush toes.
        /// </summary>
        public bool spreadDamage = false;

        /// <summary>Snow laid in the wake of the front, per cell.</summary>
        public float snowDepth = 0f;

        /// <summary>Left on everything the front touches. Frost's chill, for example.</summary>
        public HediffDef appliedHediff;
        public float appliedHediffSeverity = 1f;

        /// <summary>A second hediff, e.g. Frost Breath's ice-encasing freeze.</summary>
        public HediffDef secondaryHediff;
        public float secondaryHediffSeverity = 1f;

        /// <summary>Front speed in cells per second. Defaults to the shared shout speed.</summary>
        public float waveCellsPerSecond = Thing_ShoutWave.CellsPerSecond;

        /// <summary>Above zero makes the wave a constant-width lane instead of a cone.</summary>
        public float laneWidth = 0f;

        /// <summary>
        /// Above zero makes the wave a travelling VORTEX of this radius instead of a cone -
        /// a tornado crossing the ground toward the target. Cyclone.
        /// </summary>
        public float vortexRadius = 0f;
        public float vortexSpinPerTick = 22f;

        /// <summary>Below 1 makes the wave fainter. Cyclone is meant to be barely visible.</summary>
        public float alphaScale = 1f;

        /// <summary>Dismay's fear. Vanilla PanicFlee, so this needs no other mod present.</summary>
        public MentalStateDef mentalState;
        public float mentalStateChance = 0f;

        /// <summary>
        /// Fraction of hits repeated immediately on already-damaged parts. Fire Breath only:
        /// it is what makes flame deadlier than frost without simply inflating its numbers.
        /// </summary>
        public float reburnFraction = 0f;

        public FleckDef fleckDef;
        public Color tint = Color.white;
        public float fleckScale = 2.1f;
        public SoundDef castSound;

        public CompProperties_ShoutCone()
        {
            compClass = typeof(CompAbilityEffect_ShoutCone);
        }
    }

    public class CompAbilityEffect_ShoutCone : CompAbilityEffect
    {
        public new CompProperties_ShoutCone Props
        {
            get { return (CompProperties_ShoutCone)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return;
            }

            Thing_ShoutWave wave = Thing_ShoutWave.Spawn(caster, target.Cell,
                Props.range, Props.coneAngle, Props.tint,
                Props.fleckDef ?? DovahkiinDefOf.Dovahkiin_Fleck_FireWave, Props.fleckScale,
                Props.waveCellsPerSecond, Props.laneWidth,
                Props.vortexRadius, Props.vortexSpinPerTick, Props.alphaScale);
            if (wave != null)
            {
                wave.SetPayload(Props.damageDef ?? DamageDefOf.Flame, Props.damageAmount, 0f,
                    Props.stunTicks, Props.ignitesPawns, Props.ignitesGround,
                    Props.appliedHediff, Props.appliedHediffSeverity,
                    Props.damageInstances, Props.snowDepth,
                    Props.secondaryHediff, Props.secondaryHediffSeverity,
                    Props.spreadDamage, Props.reburnFraction,
                    Props.mentalState, Props.mentalStateChance);
            }
            SoundDef sound = Props.castSound ?? DovahkiinVanillaDefOf.Explosion_Flame;
            sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map, false));
        }
    }

    // ------------------------------------------------------------------
    // Whirlwind Sprint and Marked for Death used to have bespoke comps here. Both are gone:
    //
    //  - The dash is now done by verbClass Verb_CastAbilityJump (vanilla's own path). Moving
    //    the caster from inside a comp's Apply() despawned it mid-cast and destroyed the pawn
    //    outright. See the warning in Abilities_Batch1.xml. Do not reintroduce it.
    //
    //  - Marked for Death is now a CompProperties_ShoutCone that carries no damage, only the
    //    mark hediff. It travels and hits a cone like every other shout, and the damage-over-
    //    time lives on the hediff (HediffComp_DamageOverTime) scaled by word level.
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Clear Skies - Lok Vah Koor. SPEC.md 4.4a. Cheap, flavourful, no target.
    // ------------------------------------------------------------------

    public class CompProperties_ShoutClearSkies : CompProperties_AbilityEffect
    {
        public int durationTicks = 30000;

        /// <summary>
        /// Cosmetic ring expanding outward from the caster. Clear Skies previously had NO
        /// visible effect at all - the weather simply changed, which reads as nothing happening
        /// when it is already clear or when the change is gradual.
        /// </summary>
        // Radius reduced from 14 after playtest ("reduce the size of the circle") and the tint
        // pushed slightly further toward blue.
        public float ringRadius = 9f;
        public Color ringTint = new Color(0.68f, 0.86f, 1f);
        public FleckDef ringFleck;
        public float ringFleckScale = 2f;
        public float ringCellsPerSecond = 24f;
        public bool ringDistortion = true;

        /// <summary>Below 1 makes the ring fainter. Colour alone cannot do it.</summary>
        public float ringAlpha = 1f;

        public CompProperties_ShoutClearSkies()
        {
            compClass = typeof(CompAbilityEffect_ShoutClearSkies);
        }
    }

    public class CompAbilityEffect_ShoutClearSkies : CompAbilityEffect
    {
        public new CompProperties_ShoutClearSkies Props
        {
            get { return (CompProperties_ShoutClearSkies)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster == null ? null : caster.Map;
            if (map == null || map.weatherManager == null)
            {
                return;
            }
            // Transition to clear weather and hold it. Never touches the tick manager or any
            // global condition - it is a local weather change, exactly as SPEC.md 4.4a says.
            map.weatherManager.TransitionTo(WeatherDefOf.Clear);
            map.weatherDecider.DisableRainFor(Props.durationTicks);

            // Thunder_OnMap, positional, matching Unrelenting Force - this is a shout that
            // reaches the sky, and it should sound like one from where the caster stands.
            // It was Thunder_OffMap on camera, which is the distant rumble used for weather
            // ambience and read as no sound at all.
            SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(caster.Position, map, false));

            if (Props.ringRadius > 0f)
            {
                ShoutTargeting.SpawnRingBurst(caster, Props.ringRadius, Props.ringTint,
                    Props.ringFleck, Props.ringFleckScale, Props.ringCellsPerSecond,
                    Props.ringDistortion, Props.ringAlpha);
            }
        }
    }

    // ------------------------------------------------------------------
    // Slow Time - Tiid Klo Ul, and Become Ethereal - Feim Zii Gron. SPEC.md 4.4a.
    //
    // Both are self-buffs: no target, no wave, no damage. The shout puts a hediff on the caster
    // and the hediff is the whole effect, so one comp covers both and the two differ only in XML.
    //
    // SPEC.md 4.4a is explicit that Slow Time is self-haste ONLY and must NEVER touch
    // Find.TickManager. Nothing here does, and nothing here may. The haste is a MoveSpeed offset
    // on the hediff plus an attack-cooldown multiplier applied in
    // Patch_VerbProperties_AdjustedCooldownTicks - both caster-local.
    // ------------------------------------------------------------------

    public class CompProperties_ShoutSelfBuff : CompProperties_AbilityEffect
    {
        /// <summary>The hediff placed on the caster. This is the entire effect.</summary>
        public HediffDef hediffDef;

        /// <summary>Severity IS the shout level, so the hediff's stages can scale with words.</summary>
        public float severity = 1f;

        /// <summary>
        /// Overrides the hediff's own HediffComp_Disappears duration, so all three levels of a
        /// shout share one HediffDef and differ only in how long they last. Zero leaves the
        /// def's own value alone.
        /// </summary>
        public int durationTicks = 0;

        public SoundDef castSound;

        /// <summary>
        /// True for vanilla sounds authored with onCamera - the "...Global" ones. Playing one of
        /// those positionally is silent from anywhere but the caster's exact tile.
        /// </summary>
        public bool soundOnCamera = false;

        public FleckDef fleckDef;
        public float fleckScale = 1.6f;

        /// <summary>
        /// Cosmetic ring expanding outward from the caster. Zero radius disables it.
        /// A self-buff otherwise fires with no visible sign that anything happened at all.
        /// </summary>
        public float ringRadius = 0f;
        public Color ringTint = Color.white;
        public FleckDef ringFleck;
        public float ringFleckScale = 1.8f;

        /// <summary>Ring speed. High values read as a snap outward rather than a drifting wave.</summary>
        public float ringCellsPerSecond = 26f;

        /// <summary>Adds vanilla's near-invisible heat-shimmer ripple at the caster.</summary>
        public bool ringDistortion = false;

        /// <summary>Below 1 makes the ring fainter. Colour alone cannot do it.</summary>
        public float ringAlpha = 1f;

        /// <summary>
        /// Slow Time's other half: a slow laid on EVERY other pawn in range, allies included,
        /// so the world appears to crawl while the caster does not.
        ///
        /// This is deliberately NOT an attack. It applies a hediff and nothing else - no
        /// damage, no DamageInfo, no instigator - so it cannot anger a faction, cannot break a
        /// non-aggression pact, and cannot make a friendly caravan turn hostile. The ability
        /// itself is also declared hostile=false.
        /// </summary>
        public HediffDef bystanderHediff;
        public float bystanderSeverity = 1f;

        /// <summary>
        /// Radius of the slow. **ZERO OR LESS MEANS THE ENTIRE MAP**, which is what Slow Time
        /// uses. A finite radius produced a visibly wrong result in playtest: raiders a little
        /// way outside it carried on at normal speed while their neighbours crawled, which reads
        /// as a bug rather than as an effect. Time does not have an edge.
        /// </summary>
        public float bystanderRadius = 0f;
        public int bystanderDurationTicks = 0;

        public CompProperties_ShoutSelfBuff()
        {
            compClass = typeof(CompAbilityEffect_ShoutSelfBuff);
        }
    }

    public class CompAbilityEffect_ShoutSelfBuff : CompAbilityEffect
    {
        public new CompProperties_ShoutSelfBuff Props
        {
            get { return (CompProperties_ShoutSelfBuff)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.health == null || Props.hediffDef == null)
            {
                return;
            }

            // Refresh rather than stack. Shouting again should restart the effect, not leave two
            // copies racing each other to expire - and severity here is the shout level, so a
            // second copy cast at a lower level would quietly weaken the first.
            Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (existing != null)
            {
                caster.health.RemoveHediff(existing);
            }

            Hediff added = HediffMaker.MakeHediff(Props.hediffDef, caster);
            added.Severity = Props.severity;
            caster.health.AddHediff(added);

            // The duration override MUST happen after the hediff is made: HediffComp_Disappears
            // fills ticksToDisappear from its own props during PostMake, so anything set earlier
            // is overwritten.
            if (Props.durationTicks > 0)
            {
                HediffWithComps withComps = added as HediffWithComps;
                if (withComps != null && withComps.comps != null)
                {
                    for (int i = 0; i < withComps.comps.Count; i++)
                    {
                        HediffComp_Disappears disappears = withComps.comps[i] as HediffComp_Disappears;
                        if (disappears != null)
                        {
                            disappears.ticksToDisappear = Props.durationTicks;
                        }
                    }
                }
            }

            if (caster.Map == null)
            {
                return;
            }
            if (Props.fleckDef != null)
            {
                // AttachedOverlay rides the pawn's own draw position and touches nothing in the
                // render pipeline, so it cannot collide with RocketMan the way a PawnRenderer
                // patch would.
                FleckMaker.AttachedOverlay(caster, Props.fleckDef, Vector3.zero,
                    Props.fleckScale, -1f);
            }
            if (Props.ringRadius > 0f)
            {
                ShoutTargeting.SpawnRingBurst(caster, Props.ringRadius, Props.ringTint,
                    Props.ringFleck, Props.ringFleckScale, Props.ringCellsPerSecond,
                    Props.ringDistortion, Props.ringAlpha);
            }
            if (Props.bystanderHediff != null)
            {
                SlowBystanders(caster);
            }
            if (Props.castSound != null)
            {
                if (Props.soundOnCamera)
                {
                    Props.castSound.PlayOneShotOnCamera(caster.Map);
                }
                else
                {
                    Props.castSound.PlayOneShot(new TargetInfo(caster.Position, caster.Map, false));
                }
            }
        }

        /// <summary>
        /// Slow Time's world half. Everyone in range except the caster is slowed - allies and
        /// enemies alike, which is the point: the caster should look fast relative to everything.
        ///
        /// SPEC.md 4.4a forbids a global time effect and forbids touching Find.TickManager, and
        /// this honours that literally. RimWorld has no sub-normal game speed to reach for
        /// anyway: TimeSpeed runs Paused, Normal, Fast, Superfast, Ultrafast with nothing below
        /// Normal, and TickRateMultiplier is a computed getter in the innermost tick loop that
        /// RocketMan already contends for. Slowing everyone else produces the same picture with
        /// none of that risk.
        ///
        /// NOT AN ATTACK, and this matters: only a hediff is applied. No DamageInfo, no
        /// instigator, no TakeDamage - so no faction takes offence and no ally turns hostile.
        /// </summary>
        private void SlowBystanders(Pawn caster)
        {
            Map map = caster.Map;
            if (map == null)
            {
                return;
            }
            // Zero or less means the WHOLE MAP - see the field comment. Cost is one pass over
            // the spawned-pawn list, which is dozens of entries, not a cell scan.
            bool wholeMap = Props.bystanderRadius <= 0f;
            float radiusSq = Props.bystanderRadius * Props.bystanderRadius;

            List<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p == null || p == caster || p.Dead || p.health == null)
                {
                    continue;
                }
                if (!wholeMap
                    && (p.Position - caster.Position).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }
                Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(Props.bystanderHediff);
                if (existing != null)
                {
                    p.health.RemoveHediff(existing);
                }
                Hediff slowed = HediffMaker.MakeHediff(Props.bystanderHediff, p);
                slowed.Severity = Props.bystanderSeverity;
                p.health.AddHediff(slowed);

                if (Props.bystanderDurationTicks > 0)
                {
                    HediffWithComps wc = slowed as HediffWithComps;
                    if (wc != null && wc.comps != null)
                    {
                        for (int ci = 0; ci < wc.comps.Count; ci++)
                        {
                            HediffComp_Disappears dis = wc.comps[ci] as HediffComp_Disappears;
                            if (dis != null)
                            {
                                dis.ticksToDisappear = Props.bystanderDurationTicks;
                            }
                        }
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Soul Tear - Rii Vaaz Zol. SPEC.md 4.4f, RISKS.md section 9.
    //
    // SINGLE TARGET. No cone, no chain, no splash - the spec is explicit, and it is what keeps
    // the most powerful shout in the mod from also being the widest.
    //
    // Heavy direct damage, and on a roll the target rises as a DEAD PUPPET: it joins the player
    // faction, fights for a limited time, and then dies. It is never restored, never recruited,
    // never healed out of it. See Hediff_DeadPuppet for why that is the safe design.
    // ------------------------------------------------------------------

    public class CompProperties_ShoutSoulTear : CompProperties_AbilityEffect
    {
        /// <summary>Shout level 1-3. Puppet chance and duration come from the tuning def.</summary>
        public int level = 1;

        /// <summary>Direct impact damage. Split across a few hits so it wounds rather than
        /// removing one limb outright.</summary>
        public float damageAmount = 40f;
        public int damageInstances = 3;
        public DamageDef damageDef;

        /// <summary>
        /// ARMOUR PENETRATION. Zero is what made this shout useless in playtest: a heavily
        /// armoured modded raider shrugged the whole thing off, because Dovahkiin_SoulWither is
        /// Blunt-parented and therefore fully reduced by blunt armour. A breath weapon may
        /// reasonably be stopped by plate; the mod's most powerful shout may not.
        /// </summary>
        public float armorPenetration = 0.75f;

        // --- the visible bolt -----------------------------------------------------------
        // Narrow lane, not a cone: TES5's Soul Tear is a single bolt that stops at the first
        // body it meets. laneWidth keeps it to a line, stopOnFirstPawn (set in code) halts it,
        // and a long trail makes it read as something thrown rather than a puff.
        public float range = 18f;
        public float laneWidth = 1.4f;
        public float waveCellsPerSecond = 22f;
        public int trailBands = 7;
        public float fleckScale = 1.9f;

        public SoundDef castSound;
        public FleckDef fleckDef;
        public Color tint = new Color(0.62f, 0.16f, 0.85f);

        public CompProperties_ShoutSoulTear()
        {
            compClass = typeof(CompAbilityEffect_ShoutSoulTear);
        }
    }

    public class CompAbilityEffect_ShoutSoulTear : CompAbilityEffect
    {
        public new CompProperties_ShoutSoulTear Props
        {
            get { return (CompProperties_ShoutSoulTear)props; }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest)
                && SoulTearUtility.IsLegalTarget(target.Pawn, parent.pawn);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return;
            }

            // A VISIBLE BOLT, not an instant hit. Playtest asked for a seen projectile that
            // stops at the first body, as it does in TES5 - so this uses the same travelling
            // wave as every other shout, in a narrow lane, with a long trail, and set to halt
            // on the first pawn it reaches.
            //
            // Damage and the puppet roll ride ALONG with the front and land on arrival, which
            // is the rule this class has followed since Phase 2a: cause and effect must line up
            // on screen. Applying them on cast would resolve the tear a second before the bolt
            // visibly got there.
            Thing_ShoutWave wave = Thing_ShoutWave.Spawn(caster, target.Cell,
                Props.range, 0f, Props.tint,
                Props.fleckDef ?? DovahkiinDefOf.Dovahkiin_Fleck_SoulTearWave,
                Props.fleckScale, Props.waveCellsPerSecond, Props.laneWidth);
            if (wave != null)
            {
                wave.SetPayload(Props.damageDef ?? DamageDefOf.Blunt, Props.damageAmount, 0f,
                    0, false, false, null, 1f,
                    Mathf.Max(1, Props.damageInstances), 0f, null, 1f,
                    true, 0f, null, 0f, Props.armorPenetration);
                wave.SetSoulTear(Props.level, true, Props.trailBands);
            }

            SoundDef sound = Props.castSound ?? SoundDefOf.Thunder_OnMap;
            sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map, false));
        }
    }

    /// <summary>
    /// Soul Tear's rules and its puppet roll, in one place.
    ///
    /// Static because the roll is resolved by Thing_ShoutWave when the bolt arrives, not by the
    /// ability comp on cast - the comp no longer has the victim at the moment it matters.
    /// </summary>
    internal static class SoulTearUtility
    {
        /// <summary>
        /// SPEC.md 4.4f: valid only on hostile pawns. Never colonists, never player-faction,
        /// never tamed animals, never a pawn already puppeted.
        /// </summary>
        internal static bool IsLegalTarget(Pawn victim, Pawn caster)
        {
            if (victim == null || caster == null || victim == caster || victim.Destroyed)
            {
                return false;
            }
            if (victim.Faction != null && victim.Faction.IsPlayer)
            {
                return false;
            }
            if (!victim.HostileTo(caster))
            {
                return false;
            }
            if (DovahkiinDefOf.Dovahkiin_DeadPuppet != null && victim.health != null
                && victim.health.hediffSet.GetFirstHediffOfDef(
                    DovahkiinDefOf.Dovahkiin_DeadPuppet) != null)
            {
                return false; // already a puppet - never re-tear one
            }
            return true;
        }

        /// <summary>Roll for the puppet and raise it, at the moment the bolt lands.</summary>
        internal static void Resolve(Pawn caster, Pawn victim, int level)
        {
            if (!IsLegalTarget(victim, caster))
            {
                return;
            }
            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            int index = Mathf.Clamp(level - 1, 0, 2);

            float chance = 0f;
            if (t != null && t.soulTearPuppetChanceByLevel != null
                && index < t.soulTearPuppetChanceByLevel.Count)
            {
                chance = t.soulTearPuppetChanceByLevel[index];
            }
            if (chance <= 0f)
            {
                return; // Level 1 is damage only by design - its chance is zero.
            }
            if (!Rand.Chance(chance))
            {
                // Say so. A failed roll is otherwise indistinguishable from a broken shout,
                // which is exactly the confusion Storm Call's silent misses caused.
                if (caster.Faction != null && caster.Faction.IsPlayer)
                {
                    Messages.Message(
                        "Dovahkiin_SoulTear_Held".Translate(
                            victim.LabelShortCap.Named("PAWN")),
                        victim, MessageTypeDefOf.NeutralEvent, false);
                }
                return;
            }

            float hours = 0f;
            if (t != null && t.soulTearPuppetHoursByLevel != null
                && index < t.soulTearPuppetHoursByLevel.Count)
            {
                hours = t.soulTearPuppetHoursByLevel[index];
            }
            if (hours <= 0f)
            {
                return;
            }
            RaisePuppet(victim, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
        }

        /// <summary>
        /// Raise the victim as a doomed puppet.
        ///
        /// If the tear killed it, it is resurrected first - which is also what makes the puppet
        /// combat-worthy, since resurrection clears the wounds the shout just inflicted. If it
        /// survived, it is torn into service alive. Both end in the same place: carrying
        /// Hediff_DeadPuppet, which kills it on expiry.
        /// </summary>
        private static void RaisePuppet(Pawn victim, int lifetimeTicks)
        {
            if (DovahkiinDefOf.Dovahkiin_DeadPuppet == null)
            {
                Log.Error("[Dovahkiin] Soul Tear rolled a puppet but Dovahkiin_DeadPuppet is "
                    + "missing, so nothing was raised. Look for an XML error above.");
                return;
            }

            if (victim.Dead)
            {
                // Resurrect, not ResurrectWithSideEffects: the side-effect version can inflict
                // brain damage and resurrection sickness, which would leave a puppet that
                // cannot fight - and fighting for its short life is the whole point.
                ResurrectionUtility.Resurrect(victim);
                if (victim.Dead || victim.Destroyed)
                {
                    return; // resurrection refused; leave it dead rather than half-raised
                }
            }

            victim.SetFaction(Faction.OfPlayer, null);

            Hediff h = HediffMaker.MakeHediff(DovahkiinDefOf.Dovahkiin_DeadPuppet, victim);
            Hediff_DeadPuppet puppet = h as Hediff_DeadPuppet;
            if (puppet != null)
            {
                puppet.SetLifetime(lifetimeTicks);
            }
            victim.health.AddHediff(h);

            GameComponent_DragonbornRegistry reg = GameComponent_DragonbornRegistry.Get;
            if (reg != null)
            {
                reg.NotifyPuppetRaised(victim);
            }

            Messages.Message(
                "Dovahkiin_SoulTear_Raised".Translate(victim.LabelShortCap.Named("PAWN")),
                victim, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // ------------------------------------------------------------------
    // Storm Call - Strun Bah Qo. SPEC.md 4.4e.
    //
    // The comp only spawns the storm and hands it its parameters. All the targeting rules -
    // and in particular the OUTDOOR rule, which is a hard requirement - live in
    // Thing_StormCall, because they have to be re-checked for every strike rather than once
    // on cast. Pawns move and take cover mid-storm.
    // ------------------------------------------------------------------

    public class CompProperties_ShoutStormCall : CompProperties_AbilityEffect
    {
        /// <summary>Shout level, 1-3. Strike count and duration are read from the tuning def.</summary>
        public int level = 1;

        /// <summary>Overrides the tuning def's radius when above zero.</summary>
        public float radiusOverride = 0f;

        public SoundDef castSound;

        public CompProperties_ShoutStormCall()
        {
            compClass = typeof(CompAbilityEffect_ShoutStormCall);
        }
    }

    public class CompAbilityEffect_ShoutStormCall : CompAbilityEffect
    {
        public new CompProperties_ShoutStormCall Props
        {
            get { return (CompProperties_ShoutStormCall)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return;
            }

            DovahkiinTuningDef t = DovahkiinTuningDef.Current;
            int index = Mathf.Clamp(Props.level - 1, 0, 2);

            int strikes = 3;
            if (t != null && t.stormCallStrikesByLevel != null
                && index < t.stormCallStrikesByLevel.Count)
            {
                strikes = t.stormCallStrikesByLevel[index];
            }

            int duration = 240;
            if (t != null && t.stormCallDurationTicksByLevel != null
                && index < t.stormCallDurationTicksByLevel.Count)
            {
                duration = t.stormCallDurationTicksByLevel[index];
            }

            float radius = Props.radiusOverride > 0f
                ? Props.radiusOverride
                : (t == null ? 25f : t.stormCallRadius);

            // Centred on the CASTER, not on the targeted cell: Storm Call in TES5 is a storm
            // gathering over the Dragonborn, not an artillery strike placed on a spot. The
            // ability is deliberately no-target for the same reason.
            Thing_StormCall.Spawn(caster, caster.Position, radius, strikes, duration);

            SoundDef sound = Props.castSound ?? SoundDefOf.Thunder_OnMap;
            sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map, false));
        }
    }

    // ------------------------------------------------------------------

    internal static class ShoutTargeting
    {
        /// <summary>
        /// Cells within range of the caster, optionally restricted to a cone facing the target.
        /// coneAngle 0 means "the target cell only".
        /// </summary>
        internal static IEnumerable<IntVec3> CellsInCone(
            Pawn caster, IntVec3 targetCell, float range, float coneAngle)
        {
            Map map = caster.Map;
            if (coneAngle <= 0f)
            {
                if (targetCell.InBounds(map))
                {
                    yield return targetCell;
                }
                yield break;
            }

            Vector3 origin = caster.Position.ToVector3Shifted();
            Vector3 facing = (targetCell.ToVector3Shifted() - origin);
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f)
            {
                yield break;
            }
            facing.Normalize();
            float halfAngle = coneAngle * 0.5f;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(caster.Position, range, true))
            {
                if (!c.InBounds(map) || c == caster.Position)
                {
                    continue;
                }
                Vector3 to = c.ToVector3Shifted() - origin;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                if (Vector3.Angle(facing, to.normalized) <= halfAngle)
                {
                    yield return c;
                }
            }
        }

        internal static IEnumerable<Pawn> PawnsInCone(
            Pawn caster, IntVec3 targetCell, float range, float coneAngle)
        {
            Map map = caster.Map;
            // Materialise first: the effects mutate pawns (damage, stun, knockback), and
            // iterating a live thing list while doing that is a crash waiting to happen.
            List<Pawn> hits = new List<Pawn>();
            foreach (IntVec3 c in CellsInCone(caster, targetCell, range, coneAngle))
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn p = things[i] as Pawn;
                    if (p != null && p != caster && !hits.Contains(p))
                    {
                        hits.Add(p);
                    }
                }
            }
            return hits;
        }

        /// <summary>
        /// The furthest standable cell along the line from origin towards target, stopping at
        /// the first obstruction. Used by Whirlwind Sprint so a dash never lands inside a wall.
        /// </summary>
        internal static IntVec3 NearestStandableTowards(Map map, IntVec3 origin, IntVec3 target)
        {
            IntVec3 best = origin;
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(origin, target))
            {
                if (!c.InBounds(map) || !c.Standable(map))
                {
                    break;
                }
                best = c;
            }
            // PointsOnLineOfSight excludes the endpoint; take it if it is reachable and clear.
            if (target.InBounds(map) && target.Standable(map)
                && GenSight.LineOfSight(origin, target, map, true))
            {
                best = target;
            }
            return best;
        }

        /// <summary>
        /// Fling a pawn away from the caster - a real arcing flight over time, not a teleport.
        ///
        /// Uses vanilla PawnFlyer, the same mechanism behind skip and jump, so the victim visibly
        /// travels. Playtest feedback on the first build was that instant repositioning read as
        /// a glitch rather than a blow, which was fair.
        /// </summary>
        internal static void Knockback(Pawn victim, IntVec3 from, float cells)
        {
            Map map = victim.Map;
            if (map == null || victim.Position == from || !victim.Spawned)
            {
                return;
            }
            IntVec3 dir = victim.Position - from;
            Vector3 norm = new Vector3(dir.x, 0f, dir.z).normalized;

            IntVec3 dest = victim.Position;
            for (int i = 1; i <= Mathf.RoundToInt(cells); i++)
            {
                IntVec3 candidate = victim.Position + new IntVec3(
                    Mathf.RoundToInt(norm.x * i), 0, Mathf.RoundToInt(norm.z * i));
                if (!candidate.InBounds(map) || !candidate.Standable(map))
                {
                    break; // Stop at the first wall - never fling anyone inside terrain.
                }
                dest = candidate;
            }
            if (dest == victim.Position)
            {
                return;
            }

            ThingDef flyerDef = DovahkiinDefOf.Dovahkiin_ShoutFlyer;
            if (flyerDef != null)
            {
                PawnFlyer flyer = PawnFlyer.MakeFlyer(flyerDef, victim, dest, null, null, false);
                if (flyer != null)
                {
                    GenSpawn.Spawn(flyer, dest, map);
                    return;
                }
            }

            // Fallback: still move them rather than doing nothing - but say so. A silent
            // fallback here is indistinguishable from a teleport, which is exactly the bug
            // report that came back from playtest 1.
            Log.Warning("[Dovahkiin] PawnFlyer unavailable for " + victim.LabelShortCap
                + " (flyerDef null: " + (flyerDef == null) + "). Falling back to instant move.");
            victim.Position = dest;
            victim.Notify_Teleported(false, true);
        }

        /// <summary>
        /// A purely cosmetic ring expanding outward from the caster - a circle centred on them,
        /// not a cone. Used by the shouts that affect nobody but the caster (Slow Time) or the
        /// whole map (Clear Skies), which would otherwise go off with no visible sign at all.
        ///
        /// It is the ordinary shout wave with coneAngle 360 and NO payload: no damage, no
        /// hediff, no knockback, no stun. It cannot hurt anyone, including bystanders.
        ///
        /// The optional distortion is vanilla's Fleck_HeatWaveDistortion - a near-invisible
        /// ripple in the air, which is Core, so it works on the baseline environment.
        /// </summary>
        internal static void SpawnRingBurst(Pawn caster, float radius, Color tint,
            FleckDef fleck, float fleckScale, float cellsPerSecond, bool distortion,
            float alphaScale = 1f)
        {
            if (caster == null || caster.Map == null)
            {
                return;
            }
            // targetCell is the caster's own cell: a ring has no facing. Thing_ShoutWave handles
            // that degenerate case explicitly rather than emitting nothing.
            Thing_ShoutWave.Spawn(caster, caster.Position, radius, 360f, tint,
                fleck, fleckScale, cellsPerSecond, 0f, 0f, 22f, alphaScale);

            if (distortion && DovahkiinVanillaDefOf.Fleck_HeatWaveDistortion != null)
            {
                FleckMaker.Static(caster.DrawPos, caster.Map,
                    DovahkiinVanillaDefOf.Fleck_HeatWaveDistortion, 6f);
            }
        }

        /// <summary>
        /// A visible blast along the shout's cone. SPEC.md 4.3 wants a directional shockwave in
        /// the facing direction; this drives it off the same geometry the damage uses, so what
        /// the player sees is exactly what got hit.
        /// </summary>
        internal static void SpawnConeVfx(
            Pawn caster, IntVec3 targetCell, float range, float coneAngle,
            FleckDef fleck, float scale, float density)
        {
            Map map = caster.Map;
            if (map == null || fleck == null)
            {
                return;
            }
            foreach (IntVec3 c in CellsInCone(caster, targetCell, range, coneAngle))
            {
                if (!Rand.Chance(density))
                {
                    continue;
                }
                FleckMaker.Static(c.ToVector3Shifted(), map, fleck, scale);
            }
            // A brighter pulse at the caster's mouth so the origin reads clearly.
            FleckMaker.Static(caster.DrawPos, map, FleckDefOf.PsycastAreaEffect, 1.2f);
        }
    }
}
