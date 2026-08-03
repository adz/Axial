namespace Axial.Refined

open Axial.Constraint

/// <summary>A value paired with the inclusive interval it is known to lie within.</summary>
/// <remarks>
/// The bounds are carried at run time rather than in type parameters. F# has no
/// type-level naturals, so a <c>Bounded&lt;'value, 'min, 'max&gt;</c> would need Peano-encoded
/// phantom types — unreadable inference errors, and nothing Fable can compile. Runtime
/// bounds also let <c>clamp</c> and <c>normalize</c> fall out of
/// <see cref="T:Axial.Refined.Interval`1"/> instead of duplicating a second bounds concept.
/// </remarks>
type Bounded<'value when 'value: comparison> =
    private {
        BoundedValue: 'value
        BoundsValue: Interval<'value>
    }

    /// <summary>Returns the underlying value, which always lies within the bounds.</summary>
    member this.Value =
        this.BoundedValue

    /// <summary>Returns the interval the value is constrained to.</summary>
    member this.Bounds =
        this.BoundsValue

    override this.ToString() =
        $"{this.BoundedValue} in {this.BoundsValue}"

/// Operations over values that carry their permitted range.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Bounded =

    /// <summary>
    /// Restricts a value into the bounds. Total — this is the constructor to reach for
    /// first, because an out-of-range input has an obvious correct answer.
    /// </summary>
    let clamp bounds value =
        { BoundedValue = Interval.clamp value bounds
          BoundsValue = bounds }

    /// <summary>Admits a value only when it already lies within the bounds.</summary>
    let create bounds value : Result<Bounded<'value>, Violation> =
        value
        |> Constraint.check (Interval.toConstraint bounds)
        |> Result.map (fun () -> { BoundedValue = value; BoundsValue = bounds })

    /// <summary>Returns a refinement admitting values within the supplied bounds.</summary>
    let refinement (bounds: Interval<'value>) =
        Refinement.define (Interval.toConstraint bounds) (fun value -> { BoundedValue = value; BoundsValue = bounds }) _.Value

    /// <summary>Returns the underlying value.</summary>
    let value (input: Bounded<'value>) = input.Value

    /// <summary>Returns the interval the value is constrained to.</summary>
    let bounds (input: Bounded<'value>) = input.Bounds

    /// <summary>Returns whether the value sits on either bound.</summary>
    let isAtBound (input: Bounded<'value>) =
        input.Value = input.Bounds.Lower || input.Value = input.Bounds.Upper

    /// <summary>
    /// Applies a mapping and re-clamps into the same bounds, so the invariant survives
    /// a mapping that would otherwise leave the range.
    /// </summary>
    let map mapping (input: Bounded<'value>) = clamp input.Bounds (mapping input.Value)

    /// <summary>Moves a value into different bounds, clamping as needed.</summary>
    let withBounds newBounds (input: Bounded<'value>) = clamp newBounds input.Value

/// Operations over bounded values whose bounds carry a numeric width.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module BoundedFloat =

    /// <summary>
    /// Returns the value's position within its bounds as a fraction from zero to one.
    /// Degenerate bounds — where lower equals upper — normalize to zero rather than
    /// dividing by it.
    /// </summary>
    let normalize (input: Bounded<float>) =
        let lower = input.Bounds.Lower
        let upper = input.Bounds.Upper
        if upper = lower then 0.0 else (input.Value - lower) / (upper - lower)

    /// <summary>Returns the value at the given fraction of the interval.</summary>
    let denormalize (bounds: Interval<float>) fraction =
        Bounded.clamp bounds (bounds.Lower + (bounds.Upper - bounds.Lower) * fraction)
