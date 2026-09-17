using UnityEngine;
using Verse;
// UnityEngine has a Logger of its own, so the plain using pulls in two.
// Alias ours rather than typing Utility.Logger everywhere.
using Logger = PawnHotgroups.Utility.Logger;

namespace PawnHotgroups.Core
{
    // Entry point. RimWorld builds one of these per load and hands it the
    // ModContentPack.
    //
    // No Harmony instance and no PatchAll - this mod patches nothing at all.
    // The behaviour lives in HotgroupManager, a GameComponent, which RimWorld
    // constructs for us because Game.FillComponents finds every GameComponent
    // subclass in every loaded assembly. Architecture 3.4.
    public class PawnHotgroupsMod : Mod
    {
        public static HotgroupSettings Settings;

        public PawnHotgroupsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HotgroupSettings>();
            Logger.Loaded("Loaded.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        // Must match About.xml's <name> exactly. The Mod options list is drawn
        // from this string, NOT from About.xml - getting one and missing the
        // other is the mistake CLAUDE.md records from 2026-09-12.
        public override string SettingsCategory()
        {
            return "Ketjak's Pawn Hotgroups";
        }
    }
}
