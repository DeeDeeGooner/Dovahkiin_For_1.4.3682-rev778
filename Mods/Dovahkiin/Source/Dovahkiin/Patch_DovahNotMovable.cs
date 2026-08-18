// A DOVAH IN FLIGHT IS NOT MOVABLE BY ANYTHING EXTERNAL - AND THE ROPE WAS ONLY HALF OF IT.
//
// ============================================================================================
// THE USER'S INVIOLABLE RULE, RESTATED 2026-08-18
// ============================================================================================
// "He still gets pulled by skeletons. Dovahs in flight are strictly not MOVABLE."
//
// Patch_DovahCannotBeRoped already refuses VANILLA roping (Pawn_RopeTracker.CreateRope), and that
// was a real fix for a real cause - EnterState still carries the write-up of skeletons lassoing
// him across the map. It is not THIS cause. Two mechanisms, one symptom; the log named the
// difference:
//
//     Exception ticking TM_GiantSkeletonR1169434 ... at Verse.AI.JobDriver.Notify_PatherArrived
//     [Dovahkiin] HOVER-DIAG ... state=Flight | job=IdleWhileDespawned ... dest=(0, 0, 0)
//
// Those two lines are adjacent in BOTH that log and the one before it, which is what turned a
// coincidence into a diagnosis. `IdleWhileDespawned` is vanilla's own Despawned think tree taking
// its SECOND branch (Data/Core/Defs/ThinkTreeDefs/SubTrees_Misc.xml):
//
//     ThinkNode_ConditionalSpawned (invert)
//       -> ThinkNode_ConditionalSpawnedOrAnyParentSpawned -> JobGiver_Carried
//       -> JobGiver_IdleWhileDespawned                              <-- he got THIS one
//
// So he was neither spawned NOR held by anything spawned: despawned into limbo. Decompiling
// TorannMagic.FlyingObject_Spinning.Launch shows precisely why:
//
//     if (flyingThing != null && flyingThing.Spawned) flyingThing.DeSpawn(DestroyMode.Vanish);
//
// The cargo is despawned into a PLAIN FIELD rather than into a ThingOwner, so ParentHolder is
// null. The projectile then flies to its target and does GenSpawn.Spawn(flyingThing, ...) on
// impact. That is the "pull": he is taken off the map and put back somewhere else.
//
// THIS IS WHY NONE OF THE FIVE EXISTING AIRBORNE PATCHES TOUCHED IT. Stun, rope, pawn collision,
// foreign stances and terrain cost - every one of them assumes he is still SPAWNED and still
// using the pather. This mechanism bypasses the pather completely, so there is no stun to ignore
// and no path to defend.
//
// ============================================================================================
// WHY REFLECTION, AND WHY NOT Thing.DeSpawn
// ============================================================================================
// CLAUDE.md: "Do not AddReference to another mod's DLL other than Harmony and HugsLib. Everything
// else - RimWorld of Magic, Dragon's Descent, VEF, JecsTools - is reflection + null-guard only."
// So the carrier type cannot be named at compile time.
//
// It is also not ONE type. RimWorld of Magic ships 26 FlyingObject_* classes - Spinning, Leap,
// DemonFlight, ValiantCharge, PsionicDash, Whirlwind, DragonStrike and the rest - every one a
// Verse.Projectile subclass declaring its OWN Launch, with no shared RWoM base class to patch. So
// rather than naming any of them, this walks the Projectile subclasses ONCE at startup and patches
// every Launch carrying a `Thing flyingThing` parameter. The cargo parameter is itself the
// signature of "this thing takes a pawn somewhere", which is why matching on it generalises to
// movers this session has never seen.
//
// ONE SWEEP, AT INIT, NEVER PER TICK. Afterwards the only cost is the prefix's own reference
// compare - the same guard the other airborne patches use.
//
// AND IT IS DELIBERATELY *NOT* A PATCH ON Thing.DeSpawn, which is the true chokepoint and was
// rejected on purpose: an airborne dragon is legitimately despawned when he DIES and when he
// leaves the map, and a blanket refusal would leak a dragon that should be gone. Refusing the
// LAUNCH cannot do that, because a launch is never how a dragon dies.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Dovahkiin
{
    /// <summary>
    /// Refuses to let any third-party projectile take an airborne dovah as cargo. Applied by
    /// reflection from <see cref="DovahkiinMod"/>; a complete no-op when no such mod is installed,
    /// which is what keeps the baseline environment unaffected.
    /// </summary>
    public static class DovahCargoRefusal
    {
        /// <summary>The cargo parameter whose presence identifies a pawn-carrying launcher.</summary>
        private const string CargoParameterName = "flyingThing";

        private static bool swept;

        /// <summary>
        /// Patch every Projectile subclass method named Launch that takes a `Thing flyingThing`.
        /// Returns how many were patched, so the caller can put the count in the log - a sweep that
        /// silently finds nothing looks identical to a sweep that works.
        /// </summary>
        public static int Apply(Harmony harmony)
        {
            if (swept || harmony == null)
            {
                return 0;
            }
            swept = true;

            MethodInfo prefix = typeof(DovahCargoRefusal).GetMethod(
                "RefuseAirborneCargo", BindingFlags.Public | BindingFlags.Static);
            if (prefix == null)
            {
                return 0;
            }

            List<Type> carriers;
            try
            {
                carriers = typeof(Projectile).AllSubclassesNonAbstract();
            }
            catch (Exception e)
            {
                Log.Warning("[Dovahkiin] Could not enumerate Projectile subclasses, so a dovah in "
                    + "flight may still be carried by another mod: " + e.Message);
                return 0;
            }

            int patched = 0;
            for (int i = 0; i < carriers.Count; i++)
            {
                Type t = carriers[i];
                MethodInfo[] methods;
                try
                {
                    // DeclaredOnly: an inherited Launch is patched on the class that declares it,
                    // and reaching it again through every subclass would stack duplicate prefixes.
                    methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance
                        | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    continue; // a type whose members will not load is not one we can protect
                }

                for (int m = 0; m < methods.Length; m++)
                {
                    MethodInfo method = methods[m];
                    if (method.Name != "Launch" || method.IsAbstract
                        || method.ContainsGenericParameters)
                    {
                        continue;
                    }
                    if (!TakesCargo(method))
                    {
                        continue;
                    }
                    try
                    {
                        harmony.Patch(method, new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception e)
                    {
                        Log.Warning("[Dovahkiin] Could not patch " + t.FullName + ".Launch; a dovah "
                            + "in flight may still be carried by it: " + e.Message);
                    }
                }
            }
            return patched;
        }

        /// <summary>Does this Launch overload take a Thing it will carry?</summary>
        private static bool TakesCargo(MethodInfo method)
        {
            ParameterInfo[] ps = method.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].Name == CargoParameterName
                    && typeof(Thing).IsAssignableFrom(ps[i].ParameterType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// THE REFUSAL. Public because Harmony resolves it by reflection.
        ///
        /// Destroys the launcher rather than merely skipping it: the caller has already spawned the
        /// projectile, and Launch is what gives it an origin and a destination - so a projectile
        /// that is skipped but left on the map is one with neither. Verified safe against the real
        /// call site: TorannMagic.Projectile_Attraction.LaunchFlyingObect ends ON the Launch call
        /// and never touches the instance afterwards.
        /// </summary>
        public static bool RefuseAirborneCargo(Thing __instance, Thing flyingThing)
        {
            Pawn cargo = flyingThing as Pawn;
            if (cargo == null || !DragonAirborneCheck.IsAirborneDragon(cargo))
            {
                return true; // not our dragon, or he is on the ground - none of our business
            }
            if (__instance != null && !__instance.Destroyed)
            {
                __instance.Destroy(DestroyMode.Vanish);
            }
            return false;
        }
    }
}
