namespace Axial.Schema

open Axial.Constraint

/// <summary>Whether boundary input for a field must be supplied.</summary>
/// <remarks>
/// Supply is evaluated before a typed value exists, so it is not a value constraint and has no place in the
/// <c>Constraint</c> vocabulary. It stays Schema-owned and is declared with <c>Schema.mustSupply</c> and
/// <c>Schema.mayOmit</c>.
/// </remarks>
[<RequireQualifiedAccess>]
type Supply =
    /// <summary>Boundary input must be supplied.</summary>
    | Supplied
    /// <summary>Boundary input may be omitted.</summary>
    | Omittable

/// <summary>
/// A constraint stored inside Schema's heterogeneous compiled plan, with its value type erased.
/// </summary>
/// <remarks>
/// Erasure is a storage detail, not a second constraint concept. The retained description is the same value
/// <c>Constraint.inspect</c> returns, so public Schema inspection exposes the unified read model rather than a
/// Schema-specific one.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type internal ErasedConstraint(check: obj -> Result<unit, Violation>, description: ConstraintDescription) =
    member _.Check = check
    member _.Description = description

/// One rule attached to a value schema or field, in declaration order.
type internal SchemaRule =
    /// A value constraint, executed against the typed value once admission produces one.
    | ValueRule of ErasedConstraint
    /// A boundary supply declaration, interpreted before a typed value exists.
    | SupplyRule of Supply

module internal ErasedConstraint =
    let erase (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')

        ErasedConstraint(
            (fun value -> Constraint.check constraint' (unbox<'value> value)),
            Constraint.inspect constraint'
        )

module internal SchemaRule =
    let ofConstraint constraint' = ValueRule(ErasedConstraint.erase constraint')

    let constraints rules =
        rules
        |> List.choose (function
            | ValueRule erased -> Some erased
            | SupplyRule _ -> None)

    let descriptions rules =
        rules |> constraints |> List.map (fun erased -> erased.Description)

    /// The last supply declaration wins, matching the general rule that a later declaration overrides an earlier one.
    let trySupply rules =
        rules
        |> List.fold
            (fun state rule ->
                match rule with
                | SupplyRule supply -> Some supply
                | ValueRule _ -> state)
            None
