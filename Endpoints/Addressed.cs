using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints;

/// <summary>
/// A request that arrives in two halves: the record it addresses, from the route, and what to do
/// to it, from the body.
/// </summary>
/// <remarks>
/// An Ardalis endpoint takes exactly one request object, so a transition route needs its route id
/// and its payload declared together. One generic wrapper says that once instead of a
/// near-identical pair of classes per route. It never reaches the OpenAPI document as a schema —
/// <see cref="Id"/> is described as a path parameter and <see cref="Body"/> as the request body —
/// so the generated TypeScript client sees the payload type, not this.
/// <para>
/// Settable properties rather than the usual immutable record: MVC's complex-object binder reads
/// the per-property binding sources below and writes each half through its setter. A positional
/// record would bind through its constructor instead, where the attributes would have to be
/// smuggled in as <c>[property: FromBody]</c> — the same thing, spelled worse.
/// </para>
/// </remarks>
public sealed class Addressed<TBody>
{
    [FromRoute(Name = "id")]
    public Guid Id { get; set; }

    [FromBody]
    public TBody Body { get; set; } = default!;
}
