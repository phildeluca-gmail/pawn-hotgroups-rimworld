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

            int index = IndexFor(e.keyCode);

            // world view. Nothing we do means anything without a map.
            // Logged only when the key actually pressed was a hotgroup
            // digit (index >= 0) - IndexFor is a pure lookup with no side
            // effect, so computing it here does not change what happens
            // below, which still returns for every key when there is no
            // map, exactly as before. A non-digit key produces no line.
            if (Find.CurrentMap == null)
            {
                if (index >= 0)
                {
                    string mod = e.control ? "Ctrl+" : (e.alt ? "Alt+" : "");
                    Logger.Info("Key: " + mod + (index + 1) + " pressed with no current map; ignored.");
                }
                return;
            }

            if (index < 0)
            {
                return;
            }

            // Ctrl and Alt are tested BEFORE the plain case, so turning the
            // setting on never steals either of them.
            if (e.control)
            {
                Logger.Info("Key: Ctrl+" + (index + 1) + " recognised - creating hotgroup " + (index + 1) + ".");
                Create(index);
                e.Use();
                return;
            }

            if (e.alt)
            {
                // Shift held at the same time keeps the old additive
                // behaviour (2026-09-20 order). e.shift is the same
                // UnityEngine.Event field as e.control and e.alt above -
                // verified public on UnityEngine.Event in
                // UnityEngine.IMGUIModule.dll on 2026-09-20.
                Logger.Info("Key: Alt+" + (index + 1) + " recognised - selecting hotgroup " + (index + 1) + ".");
                Activate(index, e.shift);
                e.Use();
                return;
            }

            // THE guard - architecture 5.1 step 2, and it used to sit above
            // everything and block all three cases.
            //
            // GUIUtility.keyboardControl is non-zero whenever a control holds
            // the keyboard, and renaming a group in the list window is exactly
            // that: without a guard, typing "2" into a group's name would fire
            // hotgroup 2. But Unity only ever clears keyboardControl when the
            // player clicks a control that does not take focus, and RimWorld
            // never clears it at all - read off Assembly-CSharp 1.5.9214 on
            // 2026-09-18: no method in the game writes GUIUtility.keyboardControl,
            // and Verse.UI.UnfocusCurrentControl is one call to GUI.FocusControl,
            // which does not touch it either. So one click into any text field
            // anywhere - a hotgroup's name here, the Architect search box - left
            // this true for the rest of the session and killed every hotgroup
            // key until the player happened to click a plain button.
            //
            // Only a plain digit is text a field can swallow, so only the plain
            // digit is guarded. Ctrl and Alt combinations are not text input and
            // are let through above.
            if (GUIUtility.keyboardControl != 0)
            {
                return;
            }

            if (PawnHotgroupsMod.Settings != null && PawnHotgroupsMod.Settings.plainNumberSelects)
            {
                // Does the same as Alt (architecture 5.1 step 6), so shift
                // behaves the same way here too.
                Activate(index, e.shift);
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

        // Alt + number, the plain-digit setting, and the Select button in the
        // list window. Reversed 2026-09-20: replaces the selection by
        // default (the order that day); additive=true keeps the old
        // behaviour and is reached only with shift held (decision 5 as
        // rewritten). The Select button always passes false - a mouse click
        // has no modifier in play, and replacing is the obvious default for
        // it too.
        public void Activate(int index, bool additive)
        {
            if (index < 0 || index >= groups.Count)
            {
                return;
            }

            var g = groups[index];

            // Safe on its own regardless of what ExposeData did - this is
            // the method the 2026-09-20 defect broke. A null list here
            // means the load-path repair below did not run or was
            // bypassed some other way; that is worth knowing about.
            if (g.members == null)
            {
                g.members = new List<Pawn>();
                Logger.Info("Activate: hotgroup " + g.Number + " had a null members list; repaired.");
            }

            int held = g.members.Count;
            Logger.Info("Activate: hotgroup " + g.Number + " entered, holding " + held + " member" + (held == 1 ? "" : "s") + ".");

            // drop the dead and departed FIRST, so the player never selects a
            // corpse (architecture 5.3 step 1)
            Prune(g, true);

            // Replacing clears first, even when the group turns out empty -
            // "replace" means the selection ends up as exactly this group,
            // including empty. One line, fired once per Activate call, never
            // per frame.
            if (!additive)
            {
                Find.Selector.ClearSelection();
                Logger.Info("Activate: hotgroup " + g.Number + " replaced the selection.");
            }
            else
            {
                Logger.Info("Activate: hotgroup " + g.Number + " added to the selection.");
            }

            if (g.members.Count == 0)
            {
                Logger.Tell("Hotgroup " + g.Number + " is empty.");
                return;
            }

            var map = Find.CurrentMap;
            var missing = new List<Pawn>();
            int selected = 0;

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
                    selected++;
                }
                else
                {
                    missing.Add(p);
                }
            }

            Logger.Info("Activate: hotgroup " + g.Number + " - " + g.members.Count + " qualified after pruning, " + selected + " selected on this map.");

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
                Logger.Info("Prune: hotgroup " + g.Number + " had a null members list; repaired.");
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

                // Unconditional, every pass, every mode - the 2026-09-20 fix.
                // Scribe_Collections.Look leaves the list null when there is
                // no saved node for this group during LoadingVars, and it can
                // do the same again during ResolvingCrossRefs even after
                // LoadingVars had already repaired it. The old repair below
                // ran only "if (Scribe.mode == LoadSaveMode.LoadingVars)",
                // which a later pass could then undo with nothing to catch
                // it - that was the hole Ctrl (Create, which never reads
                // members) never fell into and Alt (Activate, which does)
                // did. Checking here, right after every Look call, closes it
                // regardless of which pass did the damage.
                if (groups[i].members == null)
                {
                    groups[i].members = new List<Pawn>();
                    Logger.Info("ExposeData: hotgroup " + groups[i].Number + " had a null members list after Scribe_Collections.Look; repaired.");
                }
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                for (int i = 0; i < GroupCount; i++)
                {
                    groups[i].name = (names != null && i < names.Count) ? names[i] : null;
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

            // Once per load, never per frame. Proves whether the component is
            // alive after a load at all - the 2026-09-20 defect (Alt+number
            // stopped selecting after a reload) had nothing to check this
            // against.
            int withMembers = 0;
            foreach (var g in groups)
            {
                if (g.members.Count > 0)
                {
                    withMembers++;
                }
            }
            Logger.Info("FinalizeInit: load complete, " + withMembers + " of " + GroupCount + " hotgroups hold members.");
        }
    }
}
