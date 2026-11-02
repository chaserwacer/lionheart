namespace lionheart.Tests.Support;

/// <summary>
/// The collection every live-model test class belongs to, so they run one at a time.
/// </summary>
/// <remarks>
/// They share one scarce resource — a single locally hosted model — and it cannot serve concurrent
/// requests: four at once against the configured model answer three failures and one success, over
/// raw HTTP with no application code involved. xUnit parallelises across classes by default, so
/// without this the live suite fails in a way that says nothing about the code under test. One
/// collection is the whole fix; the tests themselves are unchanged.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveModelCollection
{
    public const string Name = "live model";
}
