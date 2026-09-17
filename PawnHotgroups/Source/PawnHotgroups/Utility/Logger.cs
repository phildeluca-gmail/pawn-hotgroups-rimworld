using RimWorld;
using Verse;

namespace PawnHotgroups.Utility
{
    // Named Logger, not Log - Verse.Log is in scope in every file here.
    //
    // Deliberately thin. This mod has one load line and a handful of
    // player-facing messages, and architecture section 6 says the messages do
    // NOT get echoed into Player.log - a message about the player's own key
    // press written twice is noise. So there is no verbose switch and no log
    // file; if one is ever wanted, copy RimWarOdds/Core/LogFile.cs.
    public static class Logger
    {
        private const string Prefix = "[PawnHotgroups] ";

        // the one load line
        public static void Loaded(string text)
        {
            Log.Message(Prefix + text);
        }

        // Player-facing. Goes to the top-left message feed and nowhere else.
        // historical: false keeps it out of the History tab - these are not
        // events, they are feedback on a keystroke.
        public static void Tell(string text)
        {
            Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
