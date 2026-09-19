using RimWorld;
using UnityEngine;
using Verse;
using PawnHotgroups.Core;

namespace PawnHotgroups.UI
{
    // The bottom-row window. Named and reached from Defs/MainButtonDefs -
    // MainButtonDef.tabWindowClass takes the full type name, which is why the
    // XML says PawnHotgroups.UI.MainTabWindow_Hotgroups and not the bare name
    // vanilla gets away with.
    //
    // Names and reports. It does NOT edit membership - a group is made from
    // the selection and nowhere else (architecture section 9).
    public class MainTabWindow_Hotgroups : MainTabWindow
    {
        private Vector2 scroll = Vector2.zero;

        // Set by a Select button and acted on after the scroll view has closed.
        // Activating mid-draw prunes the very list the rows below are measured
        // from, which left the last group drawn against a height that no longer
        // matched. -1 means nothing pending.
        private int pendingActivate = -1;

        private const float RowPad = 6f;
        private const float LineHeight = 24f;
        private const float NumberWidth = 34f;
        private const float NameWidth = 180f;
        private const float SelectWidth = 90f;
        private const float ScrollBarWidth = 20f;
        private const float BottomPad = 12f;

        public override Vector2 RequestedTabSize
        {
            // Wide enough for the two-line hint and a name plus a member's map
            // on one line, tall enough that ten groups need only a short scroll.
            // MainTabWindow clamps this to the screen, so asking large is safe.
            get { return new Vector2(760f, 700f); }
        }

        // Unity clears GUIUtility.keyboardControl only when a control that does
        // not take focus is clicked, and RimWorld never clears it at all. A name
        // field left focused here would otherwise keep the plain-number setting
        // dead for the rest of the session, so the window drops the keyboard on
        // its way out.
        public override void PreClose()
        {
            base.PreClose();
            // Verse.UI in full: this file's own namespace is PawnHotgroups.UI,
            // so the bare name resolves to that and not to the game's class.
            Verse.UI.UnfocusCurrentControl();
            GUIUtility.keyboardControl = 0;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var mgr = HotgroupManager.Get();
            if (mgr == null)
            {
                Widgets.Label(inRect, "No game loaded.");
                return;
            }

            // Architecture section 7 - the cleanup also runs on every draw.
            // Silent here: a window that shouts "Bob is no longer in the group"
            // once per frame would be unusable, and the same drop is announced
            // the next time the group is activated.
            mgr.PruneAll(false);

            float y = 0f;

            Text.Font = GameFont.Small;

            // The hint is longer than one line at any sensible window width, and
            // a Label clipped to LineHeight simply cut it off mid-sentence.
            // Text.CalcHeight is the game's own measurement for the wrapped
            // height of a string in a given width.
            const string hint = "Ctrl + number sets a group from your selection. Alt + number adds that group to your selection.";
            float hintHeight = Text.CalcHeight(hint, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, hintHeight), hint);
            y += hintHeight + RowPad;

            // measure first so the scroll view knows how tall it is
            float contentHeight = 0f;
            foreach (var g in mgr.groups)
            {
                contentHeight += HeightOf(g);
            }
            // the last group's divider sits at the very bottom of the content;
            // without this it lands on the frame and the row above it reads as
            // cut off
            contentHeight += BottomPad;

            var outRect = new Rect(0f, y, inRect.width, inRect.height - y);
            var viewRect = new Rect(0f, 0f, inRect.width - ScrollBarWidth, contentHeight);

            pendingActivate = -1;

            Widgets.BeginScrollView(outRect, ref scroll, viewRect, true);

            float ry = 0f;
            for (int i = 0; i < mgr.groups.Count; i++)
            {
                ry = DrawRow(mgr, i, viewRect.width, ry);
            }

            Widgets.EndScrollView();

            // outside the scroll view, so pruning a member cannot move the rows
            // that have already been drawn this pass
            if (pendingActivate >= 0)
            {
                mgr.Activate(pendingActivate);
                pendingActivate = -1;
            }
        }

        private static float HeightOf(Hotgroup g)
        {
            // header line, then one line per member, then the gap
            int lines = 1 + (g.Count == 0 ? 1 : g.Count);
            return lines * LineHeight + RowPad * 2f;
        }

        private float DrawRow(HotgroupManager mgr, int index, float width, float y)
        {
            var g = mgr.groups[index];

            var header = new Rect(0f, y, width, LineHeight);
            Widgets.DrawHighlightIfMouseover(header);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(4f, y, NumberWidth, LineHeight), g.Number.ToString());
            Text.Anchor = TextAnchor.UpperLeft;

            // The only place a name is set. This is the text field that makes
            // HotgroupManager's keyboardControl guard necessary.
            string typed = Widgets.TextField(new Rect(NumberWidth, y, NameWidth, LineHeight - 2f), g.Name);
            if (typed != g.Name)
            {
                g.name = typed;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(NumberWidth + NameWidth + 8f, y, 140f, LineHeight),
                g.Count == 0 ? "empty" : g.Count + (g.Count == 1 ? " pawn" : " pawns"));
            Text.Anchor = TextAnchor.UpperLeft;

            // Does exactly what Alt + the number does, by calling the same
            // method - the window is not a second copy of the behaviour. The
            // call itself waits until the scroll view has closed, because
            // Activate prunes the list these rows were measured from.
            var button = new Rect(width - SelectWidth - 4f, y, SelectWidth, LineHeight - 2f);
            if (Widgets.ButtonText(button, "Select"))
            {
                pendingActivate = index;
            }

            y += LineHeight;

            if (g.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(NumberWidth + 8f, y, width - NumberWidth - 8f, LineHeight),
                    "Select some pawns and press Ctrl+" + KeyHintFor(g.Number) + ".");
                GUI.color = Color.white;
                y += LineHeight;
            }
            else
            {
                var map = Find.CurrentMap;
                foreach (var p in g.members)
                {
                    var line = new Rect(NumberWidth + 8f, y, width - NumberWidth - 12f, LineHeight);

                    // Decision 14 - where each member is, every time, by the
                    // same wording as the missing-member message. The current
                    // map is spelled out rather than left blank, so a mixed
                    // group reads at a glance instead of looking half-broken.
                    bool here = p.Spawned && p.Map == map;

                    if (!here)
                    {
                        GUI.color = Color.gray;
                    }
                    Widgets.Label(line, p.LabelShortCap + "  -  " + Hotgroup.WhereIs(p));
                    GUI.color = Color.white;

                    y += LineHeight;
                }
            }

            y += RowPad * 2f;
            Widgets.DrawLineHorizontal(0f, y - RowPad, width);

            return y;
        }

        private static string KeyHintFor(int number)
        {
            return number == 10 ? "0" : number.ToString();
        }

        // first pass drew each group as one label with the names run
        // together. Kept as a note, not as code - the per-member line is what
        // decision 14 needs, because each name carries its own map.
        // Widgets.Label(rect, string.Join(", ", names));
    }
}
