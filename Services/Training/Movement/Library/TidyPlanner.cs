using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Import;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// Plans a tidy of one athlete's library by the rules: what becomes of each movement and each part
/// row, what is added, and every reason it cannot be done cleanly.
/// </summary>
/// <remarks>
/// Pure, so the whole plan — every rename and merge the dry run prints — is decided and checked
/// before a row is touched. A refusal names everything wrong at once, so one edit to the rules or
/// one correction in the app settles it rather than one run per problem.
/// <para>
/// Movements whose names the rules make the same are merged. The one kept is the one already named
/// that way, else the one with the most history, else the oldest, so a run is deterministic.
/// Part rows are matched by name ignoring case, which is also how two rows spelling one name are
/// found and folded together.
/// </para>
/// </remarks>
public static class TidyPlanner
{
    public static Result<TidyPlan, Problem> Plan(LibraryState state, MovementLibraryRules rules)
    {
        var outcomes = Fates(state, rules);
        var survivors = Survivors(outcomes);
        var added = Added(rules, survivors);
        IReadOnlyList<MovementName> after = [.. survivors.Values, .. added];

        var bases = PartsOf(MovementPart.Base, [.. state.Bases.Select(row => row.Part)], after, state, rules);
        var equipment = PartsOf(MovementPart.Equipment, state.Equipment, after, state, rules);
        var modifiers = PartsOf(MovementPart.Modifier, state.Modifiers, after, state, rules);
        var carries = BaseCarries(state, outcomes, bases);

        IReadOnlyList<string> refusals =
        [
            .. RetiredInUse(state, outcomes),
            .. SetupConflicts(state, outcomes),
            .. carries.Refusals,
            .. Collisions(after),
            .. Unsettled(rules, after),
        ];

        return refusals.Count == 0
            ? Result<TidyPlan, Problem>.Success(new TidyPlan(
                outcomes, added, [.. bases.Outcomes, .. equipment.Outcomes, .. modifiers.Outcomes],
                carries.Carries, SetupCarries(state, outcomes)))
            : Result<TidyPlan, Problem>.Failure(new Problem.Conflict(
                "The library cannot be tidied cleanly, so nothing has been written:\n"
                + string.Join("\n", refusals.Select(refusal => $"  {refusal}"))));
    }

    // ---- Movements ----

    private static IReadOnlyList<MovementOutcome> Fates(LibraryState state, MovementLibraryRules rules)
    {
        var retired = state.Movements
            .Select(movement => (Movement: movement, Retirement: rules.RetirementOf(movement.Name)))
            .Where(entry => entry.Retirement is not null)
            .Select(entry => new MovementOutcome(
                entry.Movement.Id, entry.Movement.Name, new MovementFate.Retired(entry.Retirement!.Unsure)));

        var kept = state.Movements
            .Where(movement => rules.RetirementOf(movement.Name) is null)
            .GroupBy(movement => rules.Target(movement.Name), MovementName.Matching)
            .SelectMany(group => Settled(group.Key, [.. group], rules));

        return [.. retired, .. kept];
    }

    /// <summary>The movements the rules name <paramref name="target"/>: one kept under it, the rest merged into that one.</summary>
    private static IEnumerable<MovementOutcome> Settled(
        MovementName target, IReadOnlyList<HeldMovement> named, MovementLibraryRules rules)
    {
        var kept = named
            .OrderByDescending(movement => movement.Name.SameAs(target))
            .ThenByDescending(movement => movement.Uses.Total)
            .ThenBy(movement => movement.CreatedAt)
            .ThenBy(movement => movement.Id)
            .First();

        string Unsure(HeldMovement movement) => rules.RestatementOf(movement.Name)?.Unsure ?? string.Empty;

        return named.Select(movement => new MovementOutcome(movement.Id, movement.Name,
            movement == kept
                ? kept.Name.SameAs(target) ? new MovementFate.Kept() : new MovementFate.Restated(target, Unsure(movement))
                : new MovementFate.Merged(kept.Id, target, movement.Uses, Unsure(movement))));
    }

    private static IReadOnlyDictionary<Guid, MovementName> Survivors(IReadOnlyList<MovementOutcome> outcomes) =>
        TidyPlan.SurvivorsOf(outcomes);

    private static IReadOnlyList<MovementName> Added(MovementLibraryRules rules, IReadOnlyDictionary<Guid, MovementName> survivors) =>
        [.. rules.Seed.Where(seed => !survivors.Values.Any(name => name.SameAs(seed)))];

    // ---- Parts ----

    private sealed record PartPlan(IReadOnlyList<PartOutcome> Outcomes, IReadOnlyDictionary<string, Guid?> RowFor);

    /// <summary>
    /// What becomes of each row of one part, and which row each name the library ends with is
    /// spelt by — an existing one, or none yet for a name that is created.
    /// </summary>
    /// <remarks>
    /// Rows already spelling a name the library keeps are settled first, so a renamed row folds
    /// into an existing one rather than taking its name; among rows spelling one name the one more
    /// movements use is kept. A row no movement used before is left alone: the athlete may have
    /// made it for a movement still to come.
    /// </remarks>
    private static PartPlan PartsOf(
        MovementPart part, IReadOnlyList<PartRow> rows, IReadOnlyList<MovementName> after,
        LibraryState state, MovementLibraryRules rules)
    {
        var kept = after.Select(name => NameOf(part, name)).Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var uses = state.Movements.Select(movement => RowOf(part, movement)).OfType<Guid>()
            .GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
        int UsesOf(PartRow row) => uses.GetValueOrDefault(row.Id);

        var rowFor = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);
        var outcomes = new List<PartOutcome>();

        foreach (var spelling in rows.Where(row => kept.Contains(row.Name.Trim(), StringComparer.OrdinalIgnoreCase))
                     .GroupBy(row => row.Name.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var ordered = spelling.OrderByDescending(UsesOf).ThenBy(row => row.Id).ToList();
            var canonical = ordered[0];
            rowFor[spelling.Key] = canonical.Id;

            var respelt = rules.RenamedPart(part, canonical.Name.Trim());
            if (respelt != canonical.Name.Trim())
                outcomes.Add(new PartOutcome(part, canonical.Name, new PartFate.Renamed(canonical.Id, respelt)));
            outcomes.AddRange(ordered.Skip(1).Select(row =>
                new PartOutcome(part, row.Name, new PartFate.Folded(row.Id, canonical.Name.Trim()))));
        }

        foreach (var row in rows.Where(row => !kept.Contains(row.Name.Trim(), StringComparer.OrdinalIgnoreCase)).OrderBy(row => row.Id))
        {
            var renamed = rules.RenamedPart(part, row.Name.Trim());
            var renames = !renamed.Equals(row.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                          && kept.Contains(renamed, StringComparer.OrdinalIgnoreCase);

            if (renames && rowFor.ContainsKey(renamed))
            {
                outcomes.Add(new PartOutcome(part, row.Name, new PartFate.Folded(row.Id, renamed)));
            }
            else if (renames)
            {
                rowFor[renamed] = row.Id;
                outcomes.Add(new PartOutcome(part, row.Name, new PartFate.Renamed(row.Id, renamed)));
            }
            else if (UsesOf(row) > 0)
            {
                outcomes.Add(new PartOutcome(part, row.Name, new PartFate.Removed(row.Id)));
            }
        }

        foreach (var name in kept.Where(name => !rowFor.ContainsKey(name)))
        {
            rowFor[name] = null;
            outcomes.Add(new PartOutcome(part, name, new PartFate.Created()));
        }

        return new PartPlan(outcomes, rowFor);
    }

    internal static string NameOf(MovementPart part, MovementName name) => part switch
    {
        MovementPart.Base => name.Base.Trim(),
        MovementPart.Equipment => name.Equipment.Trim(),
        MovementPart.Modifier => name.Modifier.Trim(),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static Guid? RowOf(MovementPart part, HeldMovement movement) => part switch
    {
        MovementPart.Base => movement.BaseId,
        MovementPart.Equipment => movement.EquipmentId,
        MovementPart.Modifier => movement.ModifierId,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    // ---- What a base that goes says ----

    private sealed record CarryPlan(IReadOnlyList<BaseCarry> Carries, IReadOnlyList<string> Refusals);

    /// <summary>
    /// For each base that goes while saying something — a classification the athlete stated, a
    /// description — whether the base its movements land on already says the same, can take it
    /// over, or would lose it.
    /// </summary>
    /// <remarks>
    /// A base folded into another by a rename is the athlete's own base under a new name, so the
    /// other takes over whatever of it it does not say itself. A base emptied because its movements
    /// were restated elsewhere hands over only to a base made for them; carrying a classification
    /// onto an existing base would change what that base's other movements train.
    /// </remarks>
    private static CarryPlan BaseCarries(LibraryState state, IReadOnlyList<MovementOutcome> outcomes, PartPlan bases)
    {
        var landsOn = LandingNames(outcomes);
        var byId = state.Bases.ToDictionary(row => row.Id);

        var going = bases.Outcomes
            .SelectMany(outcome => outcome.Fate switch
            {
                PartFate.Folded folded => [(Row: byId[folded.RowId], Receivers: (IReadOnlyList<string>)[folded.Into], Folded: true)],
                PartFate.Removed removed => [(Row: byId[removed.RowId], Receivers: ReceiversOf(removed.RowId, state, landsOn), Folded: false)],
                _ => Array.Empty<(BaseRow Row, IReadOnlyList<string> Receivers, bool Folded)>(),
            })
            .Where(entry => entry.Row.SaysAnything && entry.Receivers.Count > 0)
            .ToList();

        var carries = new List<BaseCarry>();
        var refusals = new List<string>();

        foreach (var (row, receivers, folded) in going)
        {
            var losses = receivers
                .Select(name => (Name: name, Row: bases.RowFor[name] is { } id ? byId[id] : null))
                .Select(receiver => (receiver.Name, receiver.Row, Lost: Unsaid(row, receiver.Row)))
                .ToList();

            if (losses.All(loss => !loss.Lost.SaysAnything))
                continue;

            var (name, taker, lost) = losses[0];
            var canTake = losses.Count == 1 && (taker is null || folded) && Takes(taker, lost);
            if (canTake)
                carries.Add(new BaseCarry(row.Name, name, lost.Statement, lost.Description));
            else
                refusals.Add($"base '{row.Name}' goes, and what the athlete said about it — {Said(row)} — would be lost: "
                             + $"its movements land on {string.Join(", ", receivers.Select(receiver => $"'{receiver}'"))}, which "
                             + "say something else. Restate the classification in the app, or rename the base instead.");
        }

        refusals.AddRange(carries
            .GroupBy(carry => carry.Into, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(carry => (carry.Statement, carry.Description.Trim())).Distinct().Count() > 1)
            .Select(group => $"bases {string.Join(" and ", group.Select(carry => $"'{carry.From}'"))} fold into '{group.Key}' "
                             + "and say different things about it."));

        return new CarryPlan(
            [.. carries.GroupBy(carry => carry.Into, StringComparer.OrdinalIgnoreCase).Select(group => group.First())],
            refusals);
    }

    /// <summary>What the going base says that the receiving one does not: each part only where it differs.</summary>
    private static BaseRow Unsaid(BaseRow going, BaseRow? receiver) => going with
    {
        Statement = going.Statement is StatedClassification.Stated && receiver?.Statement != going.Statement
            ? going.Statement
            : new StatedClassification.Unstated(),
        Description = going.Description.Trim().Length > 0 && receiver?.Description.Trim() != going.Description.Trim()
            ? going.Description.Trim()
            : string.Empty,
    };

    /// <summary>Whether a base can take over what another said without overwriting anything of its own.</summary>
    private static bool Takes(BaseRow? receiver, BaseRow lost) =>
        receiver is null
        || ((lost.Statement is StatedClassification.Unstated || receiver.Statement is StatedClassification.Unstated)
            && (lost.Description.Length == 0 || receiver.Description.Trim().Length == 0));

    /// <summary>The bases a going base's movements land on, kept or merged; a retired one lands nowhere.</summary>
    private static IReadOnlyList<string> ReceiversOf(
        Guid baseId, LibraryState state, IReadOnlyDictionary<Guid, MovementName> landsOn) =>
        [
            .. state.Movements
                .Where(movement => movement.BaseId == baseId && landsOn.ContainsKey(movement.Id))
                .Select(movement => landsOn[movement.Id].Base.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

    /// <summary>The name each movement ends up under, whether it is kept or merged into another.</summary>
    private static IReadOnlyDictionary<Guid, MovementName> LandingNames(IReadOnlyList<MovementOutcome> outcomes) =>
        outcomes
            .SelectMany(outcome => outcome.Fate switch
            {
                MovementFate.Kept => [(outcome.MovementDataId, Name: outcome.From)],
                MovementFate.Restated restated => [(outcome.MovementDataId, Name: restated.To)],
                MovementFate.Merged merged => [(outcome.MovementDataId, Name: merged.To)],
                MovementFate.Retired => Array.Empty<(Guid MovementDataId, MovementName Name)>(),
                _ => throw new System.Diagnostics.UnreachableException(),
            })
            .ToDictionary(landing => landing.MovementDataId, landing => landing.Name);

    private static string Said(BaseRow row) =>
        string.Join(" and ", new[]
        {
            row.Statement is StatedClassification.Stated ? "a stated classification" : null,
            row.Description.Trim().Length > 0 ? $"the description \"{row.Description.Trim()}\"" : null,
        }.OfType<string>());

    // ---- Merged setups ----

    private static IReadOnlyList<SetupCarry> SetupCarries(LibraryState state, IReadOnlyList<MovementOutcome> outcomes) =>
        [
            .. MergeGroups(state, outcomes)
                .Where(group => group.Kept.Setup == MovementSetup.None)
                .Select(group => (group.Kept, Setup: group.Merged.Select(movement => movement.Setup)
                    .FirstOrDefault(setup => setup != MovementSetup.None)))
                .Where(group => group.Setup is not null)
                .Select(group => new SetupCarry(group.Kept.Id, group.Setup!))
        ];

    private static IEnumerable<string> SetupConflicts(LibraryState state, IReadOnlyList<MovementOutcome> outcomes) =>
        MergeGroups(state, outcomes)
            .Where(group => group.Merged.Prepend(group.Kept)
                .Select(movement => movement.Setup).Where(setup => setup != MovementSetup.None).Distinct().Count() > 1)
            .Select(group => $"{string.Join(" and ", group.Merged.Prepend(group.Kept).Select(movement => $"'{MovementNames.Of(movement.Name)}'"))} "
                             + "merge, and their setups differ; make them agree in the app first.");

    private static IEnumerable<(HeldMovement Kept, IReadOnlyList<HeldMovement> Merged)> MergeGroups(
        LibraryState state, IReadOnlyList<MovementOutcome> outcomes)
    {
        var byId = state.Movements.ToDictionary(movement => movement.Id);
        return TidyPlan.MergesOf(outcomes)
            .GroupBy(merge => merge.Value, merge => byId[merge.Key])
            .Select(group => (byId[group.Key], (IReadOnlyList<HeldMovement>)[.. group]));
    }

    // ---- Refusals ----

    private static IEnumerable<string> RetiredInUse(LibraryState state, IReadOnlyList<MovementOutcome> outcomes)
    {
        var byId = state.Movements.ToDictionary(movement => movement.Id);
        return outcomes
            .Where(outcome => outcome.Fate is MovementFate.Retired && !byId[outcome.MovementDataId].Uses.IsUnused)
            .Select(outcome => $"'{MovementNames.Of(outcome.From)}' is to be retired, but "
                               + $"{byId[outcome.MovementDataId].Uses} name it.");
    }

    /// <summary>A name the tidy ends with that the rules would change on the next run, which would make it never finish.</summary>
    private static IEnumerable<string> Unsettled(MovementLibraryRules rules, IReadOnlyList<MovementName> after) =>
        after
            .Where(name => !rules.Target(name).SameAs(name))
            .Select(name => $"{name} would be renamed again, as {rules.Target(name)}, by the next run; the rules disagree.");

    /// <summary>Two movements the library would show under one name, which the athlete could not tell apart.</summary>
    private static IEnumerable<string> Collisions(IReadOnlyList<MovementName> after) =>
        after
            .GroupBy(MovementNames.Of, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{string.Join(" and ", group)} would both read '{group.Key}'.");
}
