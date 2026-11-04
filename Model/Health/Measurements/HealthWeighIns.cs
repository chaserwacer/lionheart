namespace lionheart.Model.Health.Measurements;

/// <summary>The weigh-ins Health holds — the athlete's scale, MyFitnessPal, Oura — offered as bodyweights.</summary>
public sealed record HealthBodyweightsImport(IReadOnlyList<WeighInCommand> WeighIns);

/// <summary>One weigh-in as Health holds it: the instant, the day it fell on the phone's clock, and the mass.</summary>
/// <remarks>Kilograms because Health holds mass without a unit and the phone has to read it in one.</remarks>
public sealed record WeighInCommand(DateOnly Day, DateTimeOffset At, double Kilograms);

/// <summary>What a batch of weigh-ins did to the athlete's bodyweight.</summary>
/// <param name="Added">Days that had no bodyweight and now have the day's first weigh-in.</param>
/// <param name="DaysAlreadyMeasured">Days that already had one, kept as they were.</param>
/// <param name="Unreadable">Weigh-ins that cannot be a person's weight, left out.</param>
/// <param name="SetsReweighed">
/// Sets on a movement whose weight adds to bodyweight — a weighted dip or pull-up — whose system
/// load now reads a different bodyweight than before.
/// </param>
public sealed record HealthBodyweightImportReport(int Added, int DaysAlreadyMeasured, int Unreadable, int SetsReweighed);
