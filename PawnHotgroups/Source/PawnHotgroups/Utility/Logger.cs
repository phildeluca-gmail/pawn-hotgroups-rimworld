using RimWorld;
using Verse;

namespace PawnHotgroups.Utility
{
    // Named Logger, not Log - Verse.Log is in scope in every file here.
    //
    // Reversed 2026-09-20. Architecture section 6 used to say none of this
    // reaches Player.log - a message about the player's own key press
    // written twice was judged to be noise. That stood until a real defect
    // (Alt+number no longer selecting after a save was reloaded) could not
    // be diagnosed at all, because nothing this mod does had ever reached
    // a log. Tell() now writes the same text to Player.log as well as the
    // message feed, and Info() covers the handful of internal events (a
    // key recognised, the manager waking up after a load) that never had a
    // player-facing message of their own. Every call site is gated on an
    // actual key press or a one-time load event - see HotgroupManager for
    // where each call sits and why none of it can run every frame.
    public static class Logger
    {
        private const string Prefix = "[PawnHotgroups] ";

        // the one load line
        public static void Loaded(string text)
        {
            Log.Message(Prefix + text);
        }

        // Player-facing. Goes to the top-left message feed AND Player.log,
        // same text, since 2026-09-20. historical: false keeps it out of
        // the History tab - these are feedback on a keystroke, not events.
        public static void Tell(string text)
        {
            Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
            Log.Message(Prefix + text);
        }

        // Internal diagnostic line - log only, no on-screen message. For
        // events that have no player-facing wording of their own but that
        // a diagnosis needs: a key recognised, a load finishing.
        public static void Info(string text)
        {
            Log.Message(Prefix + text);
        }
    }
}
