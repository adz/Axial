namespace Axial.Constraint

open System
open System.ComponentModel

/// <summary>
/// Internal helpers for comparing floating-point payloads without breaking structural self-equality.
/// </summary>
/// <remarks>
/// A raw <c>float</c> inside a union case makes the case unequal to itself when it holds <c>NaN</c>, and equal to
/// its own negation when it holds a signed zero. Both break the invariant that a <see cref="T:Axial.Constraint.Violation" />
/// carrying a value equals itself and can be compared for regression. The comparisons here treat <c>NaN</c> as
/// self-equal and keep <c>0.0</c> distinct from <c>-0.0</c>, using only arithmetic that Fable and NativeAOT both
/// support — <c>BitConverter</c> is deliberately avoided because it is unproven on those targets.
/// </remarks>
module internal FloatIdentity =
    let isNegativeZero (value: float) = value = 0.0 && 1.0 / value < 0.0

    let equals (left: float) (right: float) =
        if left <> left then right <> right
        elif left = 0.0 && right = 0.0 then isNegativeZero left = isNegativeZero right
        else left = right

    let hash (value: float) =
        if value <> value then 0
        elif value = 0.0 then (if isNegativeZero value then 1 else 2)
        else value.GetHashCode()

    let isNegativeZero32 (value: float32) = value = 0.0f && 1.0f / value < 0.0f

    let equals32 (left: float32) (right: float32) =
        if left <> left then right <> right
        elif left = 0.0f && right = 0.0f then isNegativeZero32 left = isNegativeZero32 right
        else left = right

    let hash32 (value: float32) =
        if value <> value then 0
        elif value = 0.0f then (if isNegativeZero32 value then 1 else 2)
        else value.GetHashCode()

/// <summary>An IEEE double retaining structural self-equality, including <c>NaN</c> and signed zero.</summary>
[<CustomEquality; NoComparison>]
type PortableFloat =
    { Value: float }

    override this.Equals(other) =
        match other with
        | :? PortableFloat as other -> FloatIdentity.equals this.Value other.Value
        | _ -> false

    override this.GetHashCode() = FloatIdentity.hash this.Value

/// <summary>An IEEE single retaining structural self-equality, including <c>NaN</c> and signed zero.</summary>
[<CustomEquality; NoComparison>]
type PortableFloat32 =
    { Value: float32 }

    override this.Equals(other) =
        match other with
        | :? PortableFloat32 as other -> FloatIdentity.equals32 this.Value other.Value
        | _ -> false

    override this.GetHashCode() = FloatIdentity.hash32 this.Value

/// <summary>
/// The closed set of operand and actual-value representations a constraint may carry across a description,
/// diagnostic, or localization boundary.
/// </summary>
/// <remarks>
/// <para>
/// A value is admitted only when the representation is lossless in the semantics Axial's runtime diagnostics and
/// exporters use. Semantic sorts keep their own case rather than being flattened into <c>Text</c> because their
/// wire rendering happens to be textual: an instant and the string spelling it are different facts, and an
/// interpreter that cannot tell them apart cannot decide whether wire equality substitutes for typed equality.
/// </para>
/// <para>
/// <c>Guid</c> and <c>TimeSpan</c> are reached through typed dispatch rather than a runtime type test. Fable
/// erases a <c>Guid</c> to a plain string and a <c>TimeSpan</c> to a number, so a boxed type test silently
/// labels them <c>Text</c> and <c>Integer</c> there while .NET labels them correctly — the same constraint
/// meaning two different things per platform. <c>ConstraintValue.ofOperand</c> resolves the overload at the
/// call site, where the type is still known, so both platforms agree.
/// </para>
/// <para>
/// Values outside this set are never boxed through the public surface. The constraint still executes against its
/// private typed closure; the atom describes and fails as <c>UnsupportedOperand</c> instead.
/// </para>
/// <para>
/// This is not a solver literal theory. A later proof phase declares its own numeric, date, and string sorts and
/// adds a translation from the cases here; nothing may be added to this type for the solver alone.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type ConstraintValue =
    /// <summary>Text.</summary>
    | Text of string
    /// <summary>A single character.</summary>
    | Char of char
    /// <summary>An integral value that fits a signed 64-bit integer.</summary>
    | Integer of int64
    /// <summary>An arbitrary-width integer.</summary>
    | BigInteger of bigint
    /// <summary>An exact base-10 value.</summary>
    | Decimal of decimal
    /// <summary>An IEEE double, retained without passing through <c>decimal</c>.</summary>
    | Float of PortableFloat
    /// <summary>An IEEE single, retained without passing through <c>decimal</c>.</summary>
    | Float32 of PortableFloat32
    /// <summary>A Boolean.</summary>
    | Boolean of bool
    /// <summary>A globally unique identifier, kept distinct from its textual spelling.</summary>
    | Guid of Guid
    /// <summary>A date and time without an offset.</summary>
    | DateTime of DateTime
    /// <summary>A date and time with an offset from UTC.</summary>
    | DateTimeOffset of DateTimeOffset
    /// <summary>A duration.</summary>
    | TimeSpan of TimeSpan
    /// <summary>An absent reference. Distinct from "no portable representation available".</summary>
    | Null
    /// <summary>An ordered collection of portable values.</summary>
    | List of ConstraintValue list

/// <summary>The boxed-value projection behind <c>tryCreate</c>. Not part of the supported surface.</summary>
/// <remarks>Public only so the inline operand projection can reach it; prefer <c>ConstraintValue.ofOperand</c>.</remarks>
[<EditorBrowsable(EditorBrowsableState.Never)>]
module ConstraintValueInternals =
    let rec tryOfObj (value: obj) : ConstraintValue option =
        match value with
        | null -> Some ConstraintValue.Null
        | :? ConstraintValue as value -> Some value
        | :? string as value -> Some(ConstraintValue.Text value)
        | :? char as value -> Some(ConstraintValue.Char value)
        | :? bool as value -> Some(ConstraintValue.Boolean value)
        | :? int8 as value -> Some(ConstraintValue.Integer(int64 value))
        | :? uint8 as value -> Some(ConstraintValue.Integer(int64 value))
        | :? int16 as value -> Some(ConstraintValue.Integer(int64 value))
        | :? uint16 as value -> Some(ConstraintValue.Integer(int64 value))
        | :? int as value -> Some(ConstraintValue.Integer(int64 value))
        | :? uint32 as value -> Some(ConstraintValue.Integer(int64 value))
        | :? int64 as value -> Some(ConstraintValue.Integer value)
        | :? bigint as value -> Some(ConstraintValue.BigInteger value)
        | :? decimal as value -> Some(ConstraintValue.Decimal value)
        | :? float as value -> Some(ConstraintValue.Float { Value = value })
        | :? float32 as value -> Some(ConstraintValue.Float32 { Value = value })
        | :? Guid as value -> Some(ConstraintValue.Guid value)
        | :? TimeSpan as value -> Some(ConstraintValue.TimeSpan value)
        | :? DateTime as value -> Some(ConstraintValue.DateTime value)
        | :? DateTimeOffset as value -> Some(ConstraintValue.DateTimeOffset value)
        | :? System.Collections.IEnumerable as values ->
            (Some [], values |> Seq.cast<obj>)
            ||> Seq.fold (fun state item ->
                state |> Option.bind (fun collected -> tryOfObj item |> Option.map (fun item -> item :: collected)))
            |> Option.map (List.rev >> ConstraintValue.List)
        | _ -> None

/// <summary>Builds and renders portable constraint values.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ConstraintValue =
    /// <summary>Wraps an IEEE double.</summary>
    let ofFloat (value: float) = ConstraintValue.Float { Value = value }

    /// <summary>Wraps an IEEE single.</summary>
    let ofFloat32 (value: float32) = ConstraintValue.Float32 { Value = value }

    /// <summary>
    /// Projects a runtime value to its portable representation, or <c>None</c> when the type is outside the closed
    /// set. This never throws, including for <c>NaN</c>, infinities, and values no numeric case can hold.
    /// </summary>
    /// <example><code>ConstraintValue.tryCreate 3 = Some (ConstraintValue.Integer 3L)</code></example>
    let tryCreate (value: 'value) : ConstraintValue option = ConstraintValueInternals.tryOfObj (box value)

    /// <summary>
    /// Projects an operand to its portable representation, resolving ambiguous types at the call site.
    /// </summary>
    /// <remarks>
    /// Prefer this over <c>tryCreate</c> for constructor operands. <c>tryCreate</c> inspects a boxed value, which
    /// cannot recover a <c>Guid</c> or <c>TimeSpan</c> on Fable; this resolves the overload while the type is
    /// still known, so the same constraint describes itself identically on every platform.
    /// </remarks>
    /// <example><code>ConstraintValue.ofOperand instant // Some (ConstraintValue.DateTimeOffset instant)</code></example>
    let inline ofOperand (value: 'value) : ConstraintValue option =
        // `typeof<'value>` is baked in at the call site, where the type is still concrete. A boxed type test
        // cannot do this: Fable erases a Guid to a plain string and a TimeSpan to a number, so `:? Guid` there
        // silently labels the operand `Text` while .NET labels it `Guid` — one constraint, two meanings.
        if typeof<'value> = typeof<Guid> then
            Some(ConstraintValue.Guid(unbox<Guid> (box value)))
        elif typeof<'value> = typeof<TimeSpan> then
            Some(ConstraintValue.TimeSpan(unbox<TimeSpan> (box value)))
        else
            ConstraintValueInternals.tryOfObj (box value)

    /// <summary>Renders a portable value for a default English message. Not a wire format.</summary>
    let rec render (value: ConstraintValue) : string =
        match value with
        | ConstraintValue.Text value -> value
        | ConstraintValue.Char value -> string value
        | ConstraintValue.Integer value -> string value
        | ConstraintValue.BigInteger value -> string value
        | ConstraintValue.Decimal value -> string value
        | ConstraintValue.Float value -> string value.Value
        | ConstraintValue.Float32 value -> string value.Value
        | ConstraintValue.Boolean value -> if value then "true" else "false"
        | ConstraintValue.Guid value -> value.ToString "D"
        | ConstraintValue.TimeSpan value -> string value
        | ConstraintValue.DateTime value -> value.ToString("O", Globalization.CultureInfo.InvariantCulture)
        | ConstraintValue.DateTimeOffset value -> value.ToString("O", Globalization.CultureInfo.InvariantCulture)
        | ConstraintValue.Null -> "null"
        | ConstraintValue.List values -> values |> List.map render |> String.concat ", "
