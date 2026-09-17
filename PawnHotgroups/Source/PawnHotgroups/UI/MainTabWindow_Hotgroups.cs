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

        private const float RowPad = 6f;
        private const float LineHeight = 24f;
        private const float NumberWidth = 34f;
        private const float NameWidth = 180f;
        private const float SelectWidth = 90f;

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(620f, 620f); }
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
            Widgets.Label(new Rect(0f, y, inRect.width, LineHeight),
                "Ctrl + number sets a group from your selection. Alt + number adds that group to your selection.");
            y += LineHeight + RowPad;

            // measure first so the scroll view knows how tall it is
            float contentHeight = 0f;
            foreach (var g in mgr.groups)
            {
                contentHeight += HeightOf(g);
            }

            var outRect = new Rect(0f, y, inRect.width, inRect.height - y);
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(outRect, ref scroll, viewRect, true);

            float ry = 0f;
            for (int i = 0; i < mgr.groups.Count; i++)
            {
                ry = DrawRow(mgr, i, viewRect.width, ry);
            }

            Widgets.EndScrollView();
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
            // method - the window is not a second copy of the behaviour.
            var button = new Rect(width - SelectWidth - 4f, y, SelectWidth, LineHeight - 2f);
            if (Widgets.ButtonText(button, "Select"))
            {
                mgr.Activate(index);
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
