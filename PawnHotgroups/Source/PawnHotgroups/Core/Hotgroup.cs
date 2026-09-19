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
        //
        // 2026-09-18: the body of Pawn.IsPlayerControlled was finally read off
        // Assembly-CSharp 1.5.9214 rather than guessed at. It is
        // IsColonistPlayerControlled || IsColonyMechPlayerControlled ||
        // IsColonyMutantPlayerControlled, and ALL THREE of those begin with a
        // Spawned test. So IsPlayerControlled is false for a colonist in a
        // caravan, in a drop pod, or anywhere else off a map - and section 7's
        // cleanup was therefore deleting every member who left the map and
        // telling the player "X is no longer in the group". Decision 9 says an
        // absent member is reported, not removed, so the Spawned test cannot be
        // part of qualifying.
        //
        // A spawned pawn is tested exactly as architecture 2.2 writes it. An
        // unspawned one is kept when it is still ours and still takes orders:
        // player faction, not held by anyone (HostFaction is the captor), and
        // not a slave. Those are the same three conditions
        // IsColonistPlayerControlled applies once Spawned is out of the way.
        //
        // Prisoners: no clause adds them back. A prisoner takes no orders, and
        // a captured pawn has a HostFaction.
        public static bool Qualifies(Pawn p)
        {
            if (p == null || p.Destroyed || p.Dead)
            {
                return false;
            }

            if (p.Spawned)
            {
                return p.IsPlayerControlled
                    || (p.Faction == Faction.OfPlayer && p.IsNonMutantAnimal);
            }

            // off a map - in a caravan, in a pod, being carried
            return p.Faction == Faction.OfPlayer
                && p.HostFaction == null
                && !p.IsSlave;
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
