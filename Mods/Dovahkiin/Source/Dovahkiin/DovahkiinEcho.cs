// Implements: the fallen Dovahkiin's echo - Ancient Dragonborn summons wear the face of the
// last Dovahkiin to die. User's idea, and a good one.
//
// ============================================================================================
// WHY THIS IS APPEARANCE ONLY, AND WHY THAT MATTERS
// ============================================================================================
// The echo copies how a dead Dovahkiin LOOKED and nothing else. No traits, no backstory, no
// skills, no name, no relations, no title.
//
// That restraint is deliberate. A summon that inherited a dead colonist's identity would be a
// pawn the colony could plausibly recognise, mourn, or form opinions about - and every one of
// those is a hook into a system that expects the pawn to persist. The summon is doomed by
// construction and vanishes in 1.5 in-game hours; it must stay something the rest of the game
// treats as scenery. A face is safe. An identity is not.
//
// Every field is null-tolerant on the way out, because a saved echo can outlive the mod that
// supplied its hair or body type. A missing def means "leave the generated value alone", never
// a failed summon.
// ============================================================================================
using RimWorld;
using UnityEngine;
using Verse;

namespace Dovahkiin
{
    public class DovahkiinEcho : IExposable
    {
        private BodyTypeDef bodyType;
        private HeadTypeDef headType;
        private HairDef hairDef;
        private FurDef furDef;
        private Color hairColor = Color.white;
        private Color skinColor = Color.white;
        private Gender gender = Gender.None;
        private bool hasColors;

        /// <summary>Only for the log line - never shown to the player, never given to a summon.</summary>
        private string sourceName;

        public string SourceName
        {
            get { return sourceName; }
        }

        /// <summary>Nothing to copy from means no echo. Callers check this before applying.</summary>
        public bool IsUsable
        {
            get { return bodyType != null || headType != null || hairDef != null; }
        }

        public static DovahkiinEcho CaptureFrom(Pawn p)
        {
            if (p == null || p.story == null)
            {
                return null;
            }
            DovahkiinEcho e = new DovahkiinEcho();
            e.bodyType = p.story.bodyType;
            e.headType = p.story.headType;
            e.hairDef = p.story.hairDef;
            e.furDef = p.story.furDef;
            e.hairColor = p.story.HairColor;
            e.skinColor = p.story.SkinColorBase;
            e.hasColors = true;
            e.gender = p.gender;
            e.sourceName = p.LabelShortCap;
            return e;
        }

        /// <summary>
        /// Wear this face. Each field is applied only if it survived, so a save whose hair def
        /// came from a since-removed mod produces a summon with generated hair rather than an
        /// exception mid-summon.
        /// </summary>
        public void ApplyTo(Pawn p)
        {
            if (p == null || p.story == null)
            {
                return;
            }

            if (gender != Gender.None)
            {
                p.gender = gender;
            }
            if (bodyType != null)
            {
                p.story.bodyType = bodyType;
            }
            if (headType != null)
            {
                p.story.headType = headType;
            }
            if (hairDef != null)
            {
                p.story.hairDef = hairDef;
            }
            if (furDef != null)
            {
                p.story.furDef = furDef;
            }
            if (hasColors)
            {
                p.story.HairColor = hairColor;
                p.story.SkinColorBase = skinColor;
            }

            // Appearance is cached. Without this he keeps the face he was generated with and
            // every field set above is silently ignored - the change is real in data and
            // invisible on screen, which is the worst way for this to fail.
            if (p.Drawer != null && p.Drawer.renderer != null && p.Drawer.renderer.graphics != null)
            {
                p.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref bodyType, "bodyType");
            Scribe_Defs.Look(ref headType, "headType");
            Scribe_Defs.Look(ref hairDef, "hairDef");
            Scribe_Defs.Look(ref furDef, "furDef");
            Scribe_Values.Look(ref hairColor, "hairColor", Color.white);
            Scribe_Values.Look(ref skinColor, "skinColor", Color.white);
            Scribe_Values.Look(ref hasColors, "hasColors", false);
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref sourceName, "sourceName", null);
        }
    }
}
