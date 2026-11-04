using lionheart.Functional;

namespace lionheart.Model.Health;

/// <summary>The date of birth Health holds for the athlete, offered to keep the profile's age current.</summary>
/// <remarks>
/// The profile keeps an age rather than a date, so an age typed in once goes stale on the next
/// birthday, and the maximum heart rate the cardio zones fall back on is predicted from it. The
/// phone offers Health's date on every sync and the age follows it.
/// </remarks>
public sealed record HealthBirthDate(DateOnly DateOfBirth)
{
    /// <summary>Older than anyone has lived; a date of birth further back is a typing slip.</summary>
    private const int Oldest = 120;

    /// <summary>The age a person born on this date is on <paramref name="today"/>, or why the date cannot be a birth date.</summary>
    public Result<int, Problem> AgeOn(DateOnly today)
    {
        var years = today.Year - DateOfBirth.Year;
        var age = today < DateOfBirth.AddYears(years) ? years - 1 : years;

        return DateOfBirth > today || age > Oldest
            ? Result<int, Problem>.Failure(new Problem.Invalid(nameof(DateOfBirth), "Not the date of birth of a living person."))
            : Result<int, Problem>.Success(age);
    }
}

/// <summary>The profile's age after Health's date of birth was read, and whether it had to change.</summary>
public sealed record ProfileAgeView(int Age, bool Changed);
