namespace Axial.Refined

open Axial.Check

/// <summary>An inclusive range of ordered values where <c>Lower &lt;= Upper</c>.</summary>
/// <remarks>
/// An interval is always inhabited. Emptiness is represented by <c>Interval option</c>,
/// which is what <c>intersect</c> returns, rather than by a second type — carrying a
/// possibly-empty interval would double every operation without making any of them total.
///
/// The two ends are named for their roles as bounds, not for a traversal: an interval has
/// no direction, and <c>between 5 1</c> equals <c>between 1 5</c>. Wire formats that read
/// better as <c>start</c>/<c>end</c> choose those field names at the schema, which is
/// independent of these members — see <c>RefinedSchemas.dateRange</c>.
///
/// The invariant assumes the value type is <em>totally</em> ordered. <c>float</c> and
/// <c>float32</c> are not: <c>NaN</c> compares false against everything, so
/// <c>between nan x</c> cannot order its arguments and yields an interval whose bounds are
/// inverted. Use <c>Interval&lt;FiniteFloat&gt;</c>, which excludes <c>NaN</c> by
/// construction, or <c>create</c>, which rejects the pair. This is the same defect
/// <see cref="T:Axial.Refined.FiniteFloat"/> exists to remove.
/// </remarks>
type Interval<'value when 'value: comparison> =
    private {
        LowerValue: 'value
        UpperValue: 'value
    }

    /// <summary>Returns the inclusive lower bound.</summary>
    member this.Lower =
        this.LowerValue

    /// <summary>Returns the inclusive upper bound.</summary>
    member this.Upper =
        this.UpperValue

    override this.ToString() =
        $"[{this.LowerValue}, {this.UpperValue}]"

/// Operations over inclusive ranges of ordered values.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Interval =

    // Construction ---------------------------------------------------------------------

    /// <summary>Builds the interval containing exactly one value. Total.</summary>
    let singleton value = { LowerValue = value; UpperValue = value }

    /// <summary>
    /// Builds the smallest interval containing both values, ordering them as needed.
    /// Total — this is the constructor to reach for first.
    /// </summary>
    /// <remarks>
    /// Requires a total order. With <c>float</c> or <c>float32</c>, a <c>NaN</c> argument
    /// cannot be ordered against anything and produces inverted bounds; prefer
    /// <c>Interval&lt;FiniteFloat&gt;</c> or <c>create</c> there.
    /// </remarks>
    let between first second =
        if first <= second then
            { LowerValue = first; UpperValue = second }
        else
            { LowerValue = second; UpperValue = first }

    /// <summary>
    /// Builds an interval from a pair the caller asserts is already ordered, failing when
    /// it is not. Use this at a boundary, where an inverted pair is a caller error worth
    /// reporting rather than silently repairing; use <c>between</c> when either order is
    /// acceptable input.
    /// </summary>
    let create lower upper : Result<Interval<'value>, CheckFailure list> =
        if lower <= upper then
            Ok { LowerValue = lower; UpperValue = upper }
        else
            Error [ CheckFailure.Custom "interval" ]

    /// <summary>Returns the inclusive lower bound.</summary>
    let lower (interval: Interval<'value>) = interval.Lower

    /// <summary>Returns the inclusive upper bound.</summary>
    let upper (interval: Interval<'value>) = interval.Upper

    /// <summary>Returns the bounds as a pair.</summary>
    let toPair (interval: Interval<'value>) = interval.Lower, interval.Upper

    /// <summary>Returns whether the interval contains exactly one value.</summary>
    let isSingleton (interval: Interval<'value>) = interval.Lower = interval.Upper

    // Containment ----------------------------------------------------------------------

    /// <summary>Returns whether the value lies within the inclusive bounds.</summary>
    let contains value (interval: Interval<'value>) =
        value >= interval.Lower && value <= interval.Upper

    /// <summary>Returns whether every value of the inner interval lies within the outer one.</summary>
    let containsInterval (inner: Interval<'value>) (outer: Interval<'value>) =
        inner.Lower >= outer.Lower && inner.Upper <= outer.Upper

    /// <summary>Returns whether the two intervals share at least one value.</summary>
    let overlaps (first: Interval<'value>) (second: Interval<'value>) =
        first.Lower <= second.Upper && second.Lower <= first.Upper

    // Combination ----------------------------------------------------------------------

    /// <summary>
    /// Returns the shared portion of two intervals, or <c>None</c> when they are disjoint.
    /// The option is the honest representation of an empty result.
    /// </summary>
    let intersect (first: Interval<'value>) (second: Interval<'value>) =
        let lower = max first.Lower second.Lower
        let upper = min first.Upper second.Upper
        if lower <= upper then Some { LowerValue = lower; UpperValue = upper } else None

    /// <summary>
    /// Returns the combined interval when the two overlap, or <c>None</c> when a gap
    /// between them would have to be invented. Use <c>span</c> to close the gap instead.
    /// </summary>
    let union (first: Interval<'value>) (second: Interval<'value>) =
        if overlaps first second then
            Some
                { LowerValue = min first.Lower second.Lower
                  UpperValue = max first.Upper second.Upper }
        else
            None

    /// <summary>Returns the smallest interval containing both inputs, gap included. Total.</summary>
    let span (first: Interval<'value>) (second: Interval<'value>) =
        { LowerValue = min first.Lower second.Lower
          UpperValue = max first.Upper second.Upper }

    /// <summary>Returns the smallest interval containing the input and the value. Total.</summary>
    let extendTo value (interval: Interval<'value>) = span interval (singleton value)

    /// <summary>Returns the smallest interval containing every supplied value. Total.</summary>
    let ofNonEmptyList (values: NonEmptyList<'value>) =
        let (NonEmpty(head, tail)) = values
        tail |> List.fold (fun state value -> extendTo value state) (singleton head)

    // Derived operations ---------------------------------------------------------------

    /// <summary>Restricts a value to the interval's bounds. Total.</summary>
    let clamp value (interval: Interval<'value>) =
        if value < interval.Lower then interval.Lower
        elif value > interval.Upper then interval.Upper
        else value

    /// <summary>
    /// Applies a mapping to both bounds. The result is re-ordered, so a mapping that
    /// reverses the ordering still yields a well-formed interval.
    /// </summary>
    let mapMonotonic mapping (interval: Interval<'value>) =
        between (mapping interval.Lower) (mapping interval.Upper)

    /// <summary>Returns the portable constraint that admits exactly this interval's values.</summary>
    let toConstraint (interval: Interval<'value>) : Constraint<'value> =
        Constraint.between interval.Lower interval.Upper

    /// <summary>Returns how long an interval of instants lasts. Total and non-negative.</summary>
    let duration (interval: Interval<System.DateTimeOffset>) = interval.Upper - interval.Lower

    /// <summary>
    /// Returns the distance between the bounds. Total, and widened to 64 bits because the
    /// width of <c>Int32.MinValue .. Int32.MaxValue</c> does not fit an <c>int</c>.
    /// </summary>
    let widthInt (interval: Interval<int>) = int64 interval.Upper - int64 interval.Lower

    /// <summary>Returns the distance between the bounds. Never negative.</summary>
    let widthDecimal (interval: Interval<decimal>) = interval.Upper - interval.Lower

    /// <summary>Returns a refinement admitting values that lie within the interval.</summary>
    let refinement (interval: Interval<'value>) =
        Refinement.define (toConstraint interval) id id

/// <summary>An inclusive range of instants.</summary>
/// <remarks>
/// A name for the common case, not a separate type: every <c>Interval</c> operation
/// applies unchanged. Wire formats reading better as <c>start</c>/<c>end</c> get those
/// field names from <c>RefinedSchemas.dateRange</c> rather than from a duplicate type.
/// </remarks>
type DateRange = Interval<System.DateTimeOffset>
