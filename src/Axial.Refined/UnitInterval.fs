namespace Axial.Refined

open System
open Axial.Check

/// <summary>A finite double between zero and one inclusive.</summary>
/// <remarks>
/// The only type in this package closed under multiplication: a product of two values in
/// <c>[0, 1]</c> is always in <c>[0, 1]</c>, with no overflow to guard against. It is
/// <em>not</em> closed under addition — <c>0.7 + 0.7</c> leaves the range — so <c>add</c>
/// is deliberately absent in favour of <c>saturatingAdd</c> and <c>complement</c>.
/// </remarks>
type UnitInterval =
    private
    | UnitInterval of float

    /// <summary>Returns the underlying value, which always lies in <c>[0, 1]</c>.</summary>
    member this.Value =
        let (UnitInterval value) = this
        value

    override this.ToString() =
        string this.Value

/// Operations over proportions between zero and one.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module UnitInterval =

    /// <summary>The interval this type is confined to.</summary>
    let bounds = Interval.between 0.0 1.0

    /// <summary>The portable constraint admitting only values in <c>[0, 1]</c>.</summary>
    let constraint' : Constraint<float> = Constraint.between 0.0 1.0

    let refinement = Refinement.define constraint' UnitInterval _.Value

    /// <summary>Returns the underlying value.</summary>
    let value (input: UnitInterval) = input.Value

    /// <summary>Admits a proportion, rejecting anything outside <c>[0, 1]</c> including <c>NaN</c>.</summary>
    let create value = Refinement.create refinement value

    /// <summary>Zero, the lower bound.</summary>
    let zero = UnitInterval 0.0

    /// <summary>One, the upper bound and the multiplicative identity.</summary>
    let one = UnitInterval 1.0

    /// <summary>The midpoint.</summary>
    let half = UnitInterval 0.5

    /// <summary>
    /// Restricts any double into <c>[0, 1]</c>. Total, including for <c>NaN</c>, which
    /// clamps to zero because it has no meaningful position in the range.
    /// </summary>
    let clamp value =
        if Double.IsNaN value then zero
        elif value <= 0.0 then zero
        elif value >= 1.0 then one
        else UnitInterval value

    // Closed operations ----------------------------------------------------------------

    /// <summary>Returns the distance to one. Total and closed.</summary>
    /// <remarks>
    /// An involution only up to floating-point rounding: the round trip is exact for
    /// dyadic values such as <c>0.25</c>, but <c>1 - (1 - 0.3)</c> is not <c>0.3</c> in
    /// IEEE 754. Compare with a tolerance rather than for equality.
    /// </remarks>
    let complement (input: UnitInterval) = UnitInterval(1.0 - input.Value)

    /// <summary>
    /// Multiplies two proportions. Total and closed — this is the operation the type
    /// exists for, and the only closed multiplication in the package.
    /// </summary>
    let multiply (left: UnitInterval) (right: UnitInterval) = UnitInterval(left.Value * right.Value)

    /// <summary>Returns the smaller proportion. Total.</summary>
    let min (left: UnitInterval) (right: UnitInterval) = if right.Value < left.Value then right else left

    /// <summary>Returns the larger proportion. Total.</summary>
    let max (left: UnitInterval) (right: UnitInterval) = if right.Value > left.Value then right else left

    /// <summary>Adds, clamping at one. Total — the range is not closed under addition.</summary>
    let saturatingAdd (left: UnitInterval) (right: UnitInterval) = clamp (left.Value + right.Value)

    /// <summary>Subtracts, clamping at zero. Total.</summary>
    let saturatingSubtract (left: UnitInterval) (right: UnitInterval) = clamp (left.Value - right.Value)

    // Interpolation --------------------------------------------------------------------

    /// <summary>
    /// Interpolates between two values by this proportion. Total, and guaranteed to stay
    /// within the two endpoints because the proportion cannot leave <c>[0, 1]</c>.
    /// </summary>
    let lerp low high (proportion: UnitInterval) =
        low + (high - low) * proportion.Value

    /// <summary>Interpolates across an interval, landing inside it by construction.</summary>
    let lerpInterval (interval: Interval<float>) (proportion: UnitInterval) =
        lerp interval.Lower interval.Upper proportion

    /// <summary>
    /// Returns the proportion a value sits at between two bounds — the inverse of
    /// <c>lerp</c>. Clamped into range, so it is total. Degenerate bounds, where the two
    /// are equal, give zero rather than dividing by it.
    /// </summary>
    let inverseLerp low high value =
        if high = low then zero else clamp ((value - low) / (high - low))

    /// <summary>Returns the proportion a value sits at within an interval. Total.</summary>
    let inverseLerpInterval (interval: Interval<float>) value =
        inverseLerp interval.Lower interval.Upper value

    /// <summary>Widens to a finite double. Total — every proportion is finite.</summary>
    let toFiniteFloat (input: UnitInterval) =
        FiniteFloat.ofFloat input.Value |> Option.defaultValue FiniteFloat.zero
