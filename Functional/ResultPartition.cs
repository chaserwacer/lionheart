using System.Diagnostics;

namespace lionheart.Functional;

// The split of many independent results into what succeeded and what did not. One of the two
// places an orchestrator would otherwise write a loop that inspects a result's case — the thing
// the house standards reserve to this layer. The other is UnfoldAsync, in ResultLoop.cs.

/// <summary>What a batch of independent attempts produced, and what each refusal was.</summary>
/// <remarks>
/// Both halves are answers. A batch that refuses nine of ten items still delivered one, and a
/// caller that has to report what it skipped needs the refusals as data rather than as a single
/// problem that discarded the other eight.
/// </remarks>
public sealed record Partitioned<T, TProblem>(IReadOnlyList<T> Delivered, IReadOnlyList<TProblem> Refused);

/// <summary>Splitting a batch of fallible attempts without letting the first refusal end it.</summary>
public static class PartitionExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Runs a fallible step over every item and keeps both outcomes, rather than letting the
        /// first refusal stand for the whole batch.
        /// </summary>
        /// <remarks>
        /// The shape <see cref="ResultExtensions.Traverse"/> cannot express, and the one a bulk
        /// load needs: one malformed record out of a thousand must be reportable without costing
        /// the other nine hundred and ninety-nine. Where a refusal genuinely makes the remaining
        /// work meaningless, that is still <c>Traverse</c>.
        /// </remarks>
        public Partitioned<U, TProblem> Partition<U, TProblem>(Func<T, Result<U, TProblem>> selector)
        {
            var delivered = new List<U>();
            var refused = new List<TProblem>();

            foreach (var item in source)
            {
                switch (selector(item))
                {
                    case Success<U, TProblem> success:
                        delivered.Add(success.Value);
                        break;
                    case Failure<U, TProblem> failure:
                        refused.Add(failure.Problem);
                        break;
                    default:
                        throw new UnreachableException();
                }
            }

            return new Partitioned<U, TProblem>(delivered, refused);
        }
    }
}
