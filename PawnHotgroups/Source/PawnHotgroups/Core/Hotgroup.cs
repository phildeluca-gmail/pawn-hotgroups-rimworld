using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PawnHotgroups.Core
{
    // One group. Name plus members, in the order they were selected.
    //
    // Not IExposable on purpose - the members have to be saved with
    // LookMode.Reference and the names with LookMode.Value, and those are two
    // different Scribe_Collections calls that HotgroupManager makes side by
    // side. Architecture section 8.
    public class Hotgroup
    {
        public string name;
        public List<Pawn> members = new List<Pawn>();

        // 1-based, for display and for the fallback name
        public int Number;

        public Hotgroup(int number)
        {
            Number = number;
        }

        public string Name
        {
            get
            {
                if (name.NullOrEmpty())
                {
                    return "Group " + Number;
                }
                return name;
            }
        }

        public int Count
        {
            get { return members.Count; }
        }

        // Architecture 2.2 - "anything to which an order can be issued".
        // IsPlayerControlled is the game's own test and definitely exists;
        // what it returns for a colony animal was NOT confirmed by reflection
        // (a signature is not a body), so the animal clause is here as
        // insurance. Harmless if IsPlayerControlled already covers them -
        // it's an || and a pawn is only stored once either way.
        //
        // Prisoners: no clause adds them back. A prisoner takes no orders.
        public static bool Qualifies(Pawn p)
        {
            if (p == null || p.Destroyed || p.Dead)
            {
                return false;
            }

            return p.IsPlayerControlled
                || (p.Faction == Faction.OfPlayer && p.IsNonMutantAnimal);
        }

        // Where a pawn is, in words. Architecture section 6's table, used by
        // both the missing-member message and the list window so the two can
        // never drift apart.
        //
        // Map.Parent is the MapParent world object - that is what carries the
        // player-facing label ("Sandy Ridge"). Map.ToString() exists but is
        // not a name anyone would want to read.
        public static string WhereIs(Pawn p)
        {
            if (p == null)
            {
                return "off the map";
            }

            var map = p.Map;
            if (map != null)
            {
                if (map.Parent != null && !map.Parent.Label.NullOrEmpty())
                {
                    return map.Parent.Label;
                }
                // unnamed map - a temporary one, a pocket map. Nothing better to say.
                return "another map";
            }

            // GetCaravan is an extension on Pawn in RimWorld.Planet. Returns
            // null when the pawn is not travelling.
            var caravan = p.GetCaravan();
            if (caravan != null)
            {
                return caravan.Label;
            }

            return "off the map";
        }
    }
}
