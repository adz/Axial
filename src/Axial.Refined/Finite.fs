namespace Axial.Refined

open System
open Axial.Check

/// <summary>A double-precision float that is neither infinite nor <c>NaN</c>.</summary>
/// <remarks>
/// <para>
/// This type is not for arithmetic. Two finite doubles can sum to infinity, and threading
/// a <c>Result</c> through every step costs more than it returns — F# cannot propagate the
/// invariant through arithmetic the way a dependent type system would. Unwrap with
/// <c>value</c>, compute in plain <c>float</c>, and re-admit the answer. The aggregates
/// below do exactly that, failing once at the end rather than at every step.
/// </para>
/// <para>
/// What it guarantees is that aggregation means something. A single <c>NaN</c> or infinity
/// silently destroys a whole aggregate — the sum and the average of
/// <c>[ 12.5; 3.0; nan; 8.25 ]</c> are both <c>NaN</c>, with no exception and no obviously
/// wrong number. Admitting through this type localises the bad value to the boundary.
/// <c>NaN</c> also makes <c>List.contains</c> and <c>List.distinct</c> wrong, because both
/// use IEEE equality, under which <c>NaN</c> is not equal to itself.
/// </para>
/// <para>
/// It is <em>not</em> needed for sorting or for <c>Map</c>, <c>Set</c> and
/// <c>Dictionary</c> keys. F# generic comparison already orders <c>NaN</c> consistently —
/// <c>compare nan nan</c> is <c>0</c> and <c>NaN</c> sorts first — so those work on plain
/// <c>float</c>. What stays broken is a comparison hand-written with <c>&lt;</c> and
/// <c>&gt;</c>: it reports <c>NaN</c> equal to every value, which is intransitive and makes
/// <c>sortWith</c> return unsorted output without raising.
/// </para>
/// </remarks>
[<CustomEquality; CustomComparison>]
type FiniteFloat =
    private
    | FiniteFloat of float

    /// <summary>Returns the underlying value, which is always finite.</summary>
    member this.Value =
        let (FiniteFloat value) = this
        value

    override this.ToString() =
        string this.Value

    override this.Equals(other) =
        match other with
        | :? FiniteFloat as other -> this.Value = other.Value
        | _ -> false

    override this.GetHashCode() =
        this.Value.GetHashCode()

    interface IComparable<FiniteFloat> with
        member this.CompareTo(other) = compare this.Value other.Value

    interface IComparable with
        member this.CompareTo(other) =
            match other with
            | :? FiniteFloat as other -> compare this.Value other.Value
            | _ -> invalidArg (nameof other) "Cannot compare a FiniteFloat with a value of another type."

    interface IEquatable<FiniteFloat> with
        member this.Equals(other) = this.Value = other.Value

/// <summary>A single-precision float that is neither infinite nor <c>NaN</c>.</summary>
/// <remarks>
/// Carries the same guarantee as <see cref="T:Axial.Refined.FiniteFloat"/> — lawful
/// ordering — for code that stores single precision. It has no canonical wire schema,
/// because JSON has no single-precision number: widen with <c>toFiniteFloat</c> at a
/// boundary, or supply a schema explicitly.
/// </remarks>
[<CustomEquality; CustomComparison>]
type FiniteFloat32 =
    private
    | FiniteFloat32 of float32

    /// <summary>Returns the underlying value, which is always finite.</summary>
    member this.Value =
        let (FiniteFloat32 value) = this
        value

    override this.ToString() =
        string this.Value

    override this.Equals(other) =
        match other with
        | :? FiniteFloat32 as other -> this.Value = other.Value
        | _ -> false

    override this.GetHashCode() =
        this.Value.GetHashCode()

    interface IComparable<FiniteFloat32> with
        member this.CompareTo(other) = compare this.Value other.Value

    interface IComparable with
        member this.CompareTo(other) =
            match other with
            | :? FiniteFloat32 as other -> compare this.Value other.Value
            | _ -> invalidArg (nameof other) "Cannot compare a FiniteFloat32 with a value of another type."

    interface IEquatable<FiniteFloat32> with
        member this.Equals(other) = this.Value = other.Value

/// Operations over floats that are known to be neither infinite nor <c>NaN</c>.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiniteFloat =

    /// <summary>Reported when an exact result leaves the finite range.</summary>
    let private notFinite = [ CheckFailure.InvalidFormat "finite" ]

    /// <remarks>Spelled out rather than <c>Double.IsFinite</c>, which Fable does not support.</remarks>
    let private isFinite (value: float) =
        not (Double.IsNaN value || Double.IsInfinity value)

    /// <summary>The portable constraint admitting only finite doubles.</summary>
    let constraint' : Constraint<float> = Constraint.finite

    let refinement = Refinement.define constraint' FiniteFloat _.Value

    /// <summary>Returns the underlying value.</summary>
    let value (input: FiniteFloat) = input.Value

    /// <summary>Admits a finite double, rejecting infinities and <c>NaN</c>.</summary>
    let create value = Refinement.create refinement value

    /// <summary>Returns the finite value, or <c>None</c> when it is infinite or <c>NaN</c>.</summary>
    let ofFloat value =
        if isFinite value then Some(FiniteFloat value) else None

    /// <summary>The additive identity.</summary>
    let zero = FiniteFloat 0.0

    /// <summary>The multiplicative identity.</summary>
    let one = FiniteFloat 1.0

    /// <summary>Rewraps a raw result, failing when the operation left the finite range.</summary>
    let private ofComputed value =
        if isFinite value then Ok(FiniteFloat value) else Error notFinite

    // Closed operations ----------------------------------------------------------------

    /// <summary>Negates the value. Total — negation cannot leave the finite range.</summary>
    let negate (input: FiniteFloat) = FiniteFloat(-input.Value)

    /// <summary>Returns the magnitude. Total — <c>abs</c> cannot leave the finite range.</summary>
    let abs (input: FiniteFloat) = FiniteFloat(Math.Abs input.Value)

    /// <summary>Returns the smaller value. Total, and meaningful because ordering is lawful.</summary>
    let min (left: FiniteFloat) (right: FiniteFloat) = if right.Value < left.Value then right else left

    /// <summary>Returns the larger value. Total, and meaningful because ordering is lawful.</summary>
    let max (left: FiniteFloat) (right: FiniteFloat) = if right.Value > left.Value then right else left

    /// <summary>Compares two finite doubles. Total and lawful, unlike comparison on <c>float</c>.</summary>
    let compare (left: FiniteFloat) (right: FiniteFloat) = Operators.compare left.Value right.Value

    // Aggregates ------------------------------------------------------------------------
    //
    // One Result at the end rather than one per step: this is the shape that stays usable.

    /// <summary>
    /// Returns the arithmetic mean. Computed by dividing before summing, so a list whose
    /// total would overflow still averages successfully.
    /// </summary>
    let average (values: NonEmptyList<FiniteFloat>) =
        let count = float (NonEmptyList.length values)
        values
        |> NonEmptyList.fold (fun state value -> state + (value.Value / count)) 0.0
        |> ofComputed

    /// <summary>Totals a non-empty list, failing when the total leaves the finite range.</summary>
    let sum (values: NonEmptyList<FiniteFloat>) =
        values |> NonEmptyList.fold (fun state value -> state + value.Value) 0.0 |> ofComputed

    /// <summary>Returns the smallest value. Total, because ordering is lawful.</summary>
    let minimum (values: NonEmptyList<FiniteFloat>) = NonEmptyList.reduce min values

    /// <summary>Returns the largest value. Total, because ordering is lawful.</summary>
    let maximum (values: NonEmptyList<FiniteFloat>) = NonEmptyList.reduce max values

/// Operations over single-precision floats that are known to be finite.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiniteFloat32 =

    /// <remarks>Spelled out rather than <c>Single.IsFinite</c>, which Fable does not support.</remarks>
    let private isFinite (value: float32) =
        not (Single.IsNaN value || Single.IsInfinity value)

    /// <summary>The portable constraint admitting only finite single-precision floats.</summary>
    let constraint' : Constraint<float32> = Constraint.finite32

    let refinement = Refinement.define constraint' FiniteFloat32 _.Value

    /// <summary>Returns the underlying value.</summary>
    let value (input: FiniteFloat32) = input.Value

    /// <summary>Admits a finite single-precision float.</summary>
    let create value = Refinement.create refinement value

    /// <summary>Returns the finite value, or <c>None</c> when it is infinite or <c>NaN</c>.</summary>
    let ofFloat32 value =
        if isFinite value then Some(FiniteFloat32 value) else None

    /// <summary>Negates the value. Total.</summary>
    let negate (input: FiniteFloat32) = FiniteFloat32(-input.Value)

    /// <summary>Returns the magnitude. Total.</summary>
    let abs (input: FiniteFloat32) = FiniteFloat32(Math.Abs input.Value)

    /// <summary>Returns the smaller value. Total.</summary>
    let min (left: FiniteFloat32) (right: FiniteFloat32) = if right.Value < left.Value then right else left

    /// <summary>Returns the larger value. Total.</summary>
    let max (left: FiniteFloat32) (right: FiniteFloat32) = if right.Value > left.Value then right else left

    /// <summary>Widens to double precision. Total — every finite single is a finite double.</summary>
    let toFiniteFloat (input: FiniteFloat32) =
        FiniteFloat.ofFloat (float input.Value) |> Option.defaultWith (fun () -> FiniteFloat.zero)

    /// <summary>The additive identity.</summary>
    let zero = FiniteFloat32 0.0f

    /// <summary>The multiplicative identity.</summary>
    let one = FiniteFloat32 1.0f

    /// <summary>Compares two finite singles. Total and lawful, unlike comparison on <c>float32</c>.</summary>
    let compare (left: FiniteFloat32) (right: FiniteFloat32) = Operators.compare left.Value right.Value

    let private ofComputed value =
        if isFinite value then
            Ok(FiniteFloat32 value)
        else
            Error [ CheckFailure.InvalidFormat "finite" ]

    /// <summary>Totals a non-empty list, failing when the total leaves the finite range.</summary>
    let sum (values: NonEmptyList<FiniteFloat32>) =
        values |> NonEmptyList.fold (fun state value -> state + value.Value) 0.0f |> ofComputed

    /// <summary>Returns the smallest value. Total, because ordering is lawful.</summary>
    let minimum (values: NonEmptyList<FiniteFloat32>) = NonEmptyList.reduce min values

    /// <summary>Returns the largest value. Total, because ordering is lawful.</summary>
    let maximum (values: NonEmptyList<FiniteFloat32>) = NonEmptyList.reduce max values
