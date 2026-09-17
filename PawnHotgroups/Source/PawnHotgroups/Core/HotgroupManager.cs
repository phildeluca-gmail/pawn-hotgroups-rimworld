using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
// UnityEngine has a Logger of its own, so the plain using pulls in two.
// Alias ours rather than typing Utility.Logger everywhere.
using Logger = PawnHotgroups.Utility.Logger;

namespace PawnHotgroups.Core
{
    // The whole of the behaviour. A GameComponent, so we get three things from
    // one class and patch nothing: GameComponentOnGUI for the keys, ExposeData
    // for the save slot, FinalizeInit for the after-load cleanup.
    //
    // RimWorld constructs this itself - Game.FillComponents walks every loaded
    // assembly for GameComponent subclasses and news them up with the Game.
    // That is why the (Game) constructor has to exist even though we ignore it.
    public class HotgroupManager : GameComponent
    {
        public const int GroupCount = 10;

        public List<Hotgroup> groups = new List<Hotgroup>();

        public HotgroupManager(Game game)
        {
            Rebuild();
        }

        // 10 entries, always. An older save with fewer widens here and nothing
        // downstream has to check a length.
        private void Rebuild()
        {
            while (groups.Count < GroupCount)
            {
                groups.Add(new Hotgroup(groups.Count + 1));
            }
            while (groups.Count > GroupCount)
            {
                groups.RemoveAt(groups.Count - 1);
            }

            // Number is display-only but it has to match the slot after any
            // repair, or the fallback names come out wrong
            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].Number = i + 1;
                if (groups[i].members == null)
                {
                    groups[i].members = new List<Pawn>();
                }
            }
        }

        public static HotgroupManager Get()
        {
            if (Current.Game == null)
            {
                return null;
            }
            return Current.Game.GetComponent<HotgroupManager>();
        }

        // ---- keys ------------------------------------------------------

        public override void GameComponentOnGUI()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
            {
                return;
            }

            // THE guard. keyboardControl is non-zero whenever a text field has
            // focus, and renaming a group in the list window is exactly that.
            // Without this, typing "2" into a group's name fires hotgroup 2.
            // Architecture 5.1 step 2 - it has its own numbered step because
            // it is the one that gets forgotten.
            if (GUIUtility.keyboardControl != 0)
            {
                return;
            }

            // world view. Nothing we do means anything without a map.
            if (Find.CurrentMap == null)
            {
                return;
            }

            int index = IndexFor(e.keyCode);
            if (index < 0)
            {
                return;
            }

            // Ctrl and Alt are tested BEFORE the plain case, so turning the
            // setting on never steals either of them.
            if (e.control)
            {
                Create(index);
                e.Use();
                return;
            }

            if (e.alt)
            {
                Activate(index);
                e.Use();
                return;
            }

            if (PawnHotgroupsMod.Settings != null && PawnHotgroupsMod.Settings.plainNumberSelects)
            {
                Activate(index);
                e.Use();
            }

            // setting off: fall through WITHOUT calling Use(), so 1..4 still
            // change game speed
        }

        // Alpha1..Alpha9 -> 0..8, Alpha0 -> 9. Keypad the same, because a
        // player with a number pad expects it to work and it costs one line.
        private static int IndexFor(KeyCode k)
        {
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9)
            {
                return k - KeyCode.Alpha1;
            }
            if (k == KeyCode.Alpha0)
            {
                return 9;
            }
            if (k >= KeyCode.Keypad1 && k <= KeyCode.Keypad9)
            {
                return k - KeyCode.Keypad1;
            }
            if (k == KeyCode.Keypad0)
            {
                return 9;
            }
            return -1;
        }

        // ---- creating --------------------------------------------------

        // Ctrl + number. Always replaces - decision 4, "throws it away".
        public void Create(int index)
        {
            if (index < 0 || index >= groups.Count)
            {
                return;
            }

            var g = groups[index];
            var keep = new List<Pawn>();
            foreach (var p in Find.Selector.SelectedPawns)
            {
                if (Hotgroup.Qualifies(p) && !keep.Contains(p))
                {
                    keep.Add(p);
                }
            }

            g.members = keep;   // name is NOT touched - "miners" stays "miners"

            if (keep.Count == 0)
            {
                // deliberate: this is the only way to empty a group, and
                // refusing would leave a mistake with no undo
                Logger.Tell("Hotgroup " + g.Number + " cleared.");
                return;
            }

            Logger.Tell("Hotgroup " + g.Number + " set: " + keep.Count + " pawn" + (keep.Count == 1 ? "" : "s") + ".");
        }

        // ---- selecting -------------------------------------------------

        // Alt + number, and the Select button in the list window. Adds to the
        // selection, never clears it (decision 5).
        public void Activate(int index)
        {
            if (index < 0 || index >= groups.Count)
            {
                return;
            }

            var g = groups[index];

            // drop the dead and departed FIRST, so the player never selects a
            // corpse (architecture 5.3 step 1)
            Prune(g, true);

            if (g.members.Count == 0)
            {
                Logger.Tell("Hotgroup " + g.Number + " is empty.");
                return;
            }

            var map = Find.CurrentMap;
            var missing = new List<Pawn>();

            foreach (var p in g.members)
            {
                if (p.Spawned && p.Map == map)
                {
                    // IsSelected first - re-selecting is a no-op for the game
                    // but Select also plays the designator-deselect dance, so
                    // skip it
                    if (!Find.Selector.IsSelected(p))
                    {
                        Find.Selector.Select(p, false, false);
                    }
                }
                else
                {
                    missing.Add(p);
                }
            }

            // Nothing selects across maps. That filter is what decision 7 asks
            // for anyway, and it is also why we never find out what
            // Selector.Select does with an off-map pawn.
            ReportMissing(missing);
        }

        // ---- the missing-member message (architecture section 6) --------

        // One message per activation, never one per pawn. Grouped by place,
        // the places joined with a space.
        private static void ReportMissing(List<Pawn> missing)
        {
            if (missing.Count == 0)
            {
                return;
            }

            // place -> names, in first-seen order. A plain list of pairs is
            // easier to keep ordered than a Dictionary here.
            var places = new List<string>();
            var names = new List<List<string>>();

            foreach (var p in missing)
            {
                string where = Hotgroup.WhereIs(p);
                int at = places.IndexOf(where);
                if (at < 0)
                {
                    places.Add(where);
                    names.Add(new List<string>());
                    at = places.Count - 1;
                }
                names[at].Add(p.LabelShort);
            }

            var sb = new StringBuilder();
            for (int i = 0; i < places.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(Phrase(names[i], places[i]));
            }

            Logger.Tell(sb.ToString());
        }

        // "Bob is on Sandy Ridge!" / "Bob, Ada and Kim are on Sandy Ridge!"
        // "off the map" reads as a place but not as one you can be "on", so it
        // gets its own shape.
        private static string Phrase(List<string> who, string place)
        {
            string joined = JoinNames(who);
            string verb = who.Count > 1 ? "are" : "is";

            if (place == "off the map")
            {
                return joined + " " + verb + " off the map!";
            }
            return joined + " " + verb + " on " + place + "!";
        }

        // hand-rolled rather than GenText - one less unverified call, and the
        // "and" placement is the whole point
        private static string JoinNames(List<string> who)
        {
            if (who.Count == 1)
            {
                return who[0];
            }
            if (who.Count == 2)
            {
                return who[0] + " and " + who[1];
            }

            var sb = new StringBuilder();
            for (int i = 0; i < who.Count; i++)
            {
                if (i == who.Count - 1)
                {
                    sb.Append(" and ");
                }
                else if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(who[i]);
            }
            return sb.ToString();
        }

        // ---- dead and departed (architecture section 7) -----------------

        // Runs at the top of every activation and every time the list window
        // draws. Never on a tick - this mod does not tick at all.
        public static void Prune(Hotgroup g, bool announce)
        {
            if (g.members == null)
            {
                g.members = new List<Pawn>();
                return;
            }

            for (int i = g.members.Count - 1; i >= 0; i--)
            {
                var p = g.members[i];

                if (p == null)
                {
                    // no name to report - a reference the save could not
                    // resolve, usually the mod having been off for a while
                    g.members.RemoveAt(i);
                    continue;
                }

                if (Hotgroup.Qualifies(p))
                {
                    continue;
                }

                // covers dead, destroyed, banished, sold, captured, gone
                // hostile - decision 10 says the reason does not matter
                string name = p.LabelShort;
                g.members.RemoveAt(i);
                if (announce)
                {
                    Logger.Tell(name + " is no longer in the group.");
                }
            }
        }

        public void PruneAll(bool announce)
        {
            foreach (var g in groups)
            {
                Prune(g, announce);
            }
        }

        // ---- saving (architecture section 8) ---------------------------

        public override void ExposeData()
        {
            base.ExposeData();

            Rebuild();

            var names = new List<string>();
            for (int i = 0; i < GroupCount; i++)
            {
                names.Add(groups[i].name);
            }

            Scribe_Collections.Look(ref names, "names", LookMode.Value);

            // One list per group rather than a list of lists. LookMode.Reference
            // is the only mode that survives a pawn being loaded after this
            // component - Deep would save a second copy of every colonist.
            for (int i = 0; i < GroupCount; i++)
            {
                var members = groups[i].members;
                Scribe_Collections.Look(ref members, "members" + i, LookMode.Reference);
                groups[i].members = members;
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                for (int i = 0; i < GroupCount; i++)
                {
                    groups[i].name = (names != null && i < names.Count) ? names[i] : null;
                    if (groups[i].members == null)
                    {
                        groups[i].members = new List<Pawn>();
                    }
                }
            }

            // NOTHING touches the pawns here. During LoadingVars the lists are
            // full of nulls waiting on ResolvingCrossRefs, so pruning now would
            // silently empty every group. Cleanup is in FinalizeInit.
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rebuild();

            // References are resolved by now. Silent - a save that lost a pawn
            // while the mod was off would otherwise open with a wall of
            // messages nobody can act on.
            PruneAll(false);
        }
    }
}
