using UnityEngine;
using Verse;

namespace PawnHotgroups.Core
{
    // One setting. Architecture 2.1 - the user's answer to "Alt or the plain
    // number?" was "yes", which picks neither, so Alt is the default and this
    // is the switch for the other reading.
    public class HotgroupSettings : ModSettings
    {
        // Off by default because Alpha1..Alpha4 are the game's own time speed
        // keys (KeyBindings.xml binds them to TimeSpeed_Normal, _Fast,
        // _Superfast, _Ultrafast). Turning this on takes all four.
        public bool plainNumberSelects = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref plainNumberSelects, "plainNumberSelects", false);
        }

        public void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Plain number keys select a hotgroup", ref plainNumberSelects,
                "Off by default. Turning this on means 1, 2, 3 and 4 no longer change game speed - they select hotgroups 1 to 4. Ctrl and Alt keep working either way.");

            listing.Gap();
            listing.Label("Ctrl + 1..9, 0 makes a hotgroup out of the current selection. Alt + the same key adds that group to the selection.");

            listing.End();
        }
    }
}
