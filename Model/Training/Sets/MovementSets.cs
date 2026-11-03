using System.Diagnostics;

namespace lionheart.Model.Training.Sets;

/// <summary>Reading a set list by the movements it trained.</summary>
public static class MovementSets
{
    extension(IEnumerable<SetEntry> sets)
    {
        /// <summary>
        /// The sets that name a movement, grouped by it: movements in the order they first appear,
        /// each one's sets in the order they were performed.
        /// </summary>
        /// <remarks>
        /// A trail run and a freeform set name no movement, and neither does a measured set carrying
        /// <see cref="Guid.Empty"/>, which the session write does not refuse.
        /// </remarks>
        public IEnumerable<IGrouping<Guid, SetEntry>> ByMovement() =>
            sets.Select(set => (Movement: MovementOf(set), Set: set))
                .Where(entry => entry.Movement != Guid.Empty)
                .GroupBy(entry => entry.Movement, entry => entry.Set);
    }

    extension(SetEntry set)
    {
        /// <summary>Every set but a warm-up lift: the sets a routine counts and a record can come from.</summary>
        public bool IsWorking => set is not LiftSet { Kind: SetKind.WarmUp };
    }

    private static Guid MovementOf(SetEntry set) => set switch
    {
        LiftSet lift => lift.MovementDataId,
        DistanceTimeSet distanceTime => distanceTime.MovementDataId,
        TrailRunSet or FreeformSet => Guid.Empty,
        _ => throw new UnreachableException(),
    };
}
