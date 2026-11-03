using System.Diagnostics;

namespace lionheart.Model.Training.Sets;

// Member order is load-bearing: persisted as ints in set and record JSON.
public enum WeightUnit
{
    Kilograms,
    Pounds
}

public enum DistanceUnit
{
    Meters,
    Yards,
    Miles,
    Kilometers
}

/// <summary>
/// What a distance unit is worth, so two distances logged in different ones can be ranked.
/// </summary>
/// <remarks>
/// Deliberately not the same treatment weights get, which never convert: these four are exact
/// definitions of the same physical quantity, and two of them are metric spellings of one scale.
/// </remarks>
public static class DistanceUnits
{
    extension(DistanceUnit unit)
    {
        public double MetersEach() => unit switch
        {
            DistanceUnit.Meters => 1,
            DistanceUnit.Yards => 0.9144,
            DistanceUnit.Miles => 1609.344,
            DistanceUnit.Kilometers => 1000,
            _ => throw new UnreachableException()
        };
    }
}
