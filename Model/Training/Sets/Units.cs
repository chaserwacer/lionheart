namespace lionheart.Model.Training.Sets;

// Member order is load-bearing: persisted as ints (PersonalRecords table, set JSON).
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
