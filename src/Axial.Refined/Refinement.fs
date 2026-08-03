namespace Axial.Refined

open Axial.Constraint

/// <summary>Admission into an invariant-carrying value, and its total reverse projection.</summary>
/// <remarks>
/// A refinement stores exactly one <see cref="T:Axial.Constraint.Constraint`1" /> over the raw representation,
/// the constructor that stamps the invariant into the type, and the projection back to the raw value. The stored
/// constraint is the same value a caller can check, inspect, or attach to a schema directly: the raw-to-refined
/// projection is a known representation boundary, not an opaque one, so Schema lowers the constraint unchanged
/// in raw-schema context.
/// </remarks>
[<Sealed>]
type Refinement<'underlying, 'refined>
    internal
    (
        constraint': Constraint<'underlying>,
        construct: 'underlying -> 'refined,
        project: 'refined -> 'underlying
    ) =
    member internal _.Constraint = constraint'
    member internal _.Construct = construct
    member internal _.Project = project

/// <summary>Creates and applies reusable refinement definitions.</summary>
[<RequireQualifiedAccess>]
module Refinement =
    let private ensureFunction name value =
        if isNull (box value) then nullArg name

    /// <summary>Defines a refinement from one constraint, a constructor, and the reverse projection.</summary>
    /// <remarks>
    /// Compose several rules with <c>Constraint.all</c> before defining, and reach for <c>Constraint.custom</c>
    /// when the rule is an arbitrary predicate. Both produce an ordinary constraint, so there is no separate
    /// plural or check-taking constructor.
    /// </remarks>
    /// <example><code>let retryCount =
    ///     Refinement.define (Constraint.between 0 10) RetryCount _.Value</code></example>
    let define
        (constraint': Constraint<'underlying>)
        (construct: 'underlying -> 'refined)
        (project: 'refined -> 'underlying)
        =
        if isNull constraint' then nullArg (nameof constraint')
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(constraint', construct, project)

    /// <summary>Constructs a refined value, reporting why the raw value was not admitted.</summary>
    /// <example><code>value |> Refinement.create retryCount |> Result.mapError InvalidRetryCount</code></example>
    let create (refinement: Refinement<'underlying, 'refined>) (underlying: 'underlying) : Result<'refined, Violation> =
        if isNull (box refinement) then nullArg (nameof refinement)

        Constraint.check refinement.Constraint underlying
        |> Result.map (fun () -> refinement.Construct underlying)

    /// <summary>Returns the canonical underlying representation of a refined value.</summary>
    /// <example><code>RetryCount 3 |> Refinement.underlying retryCount // 3</code></example>
    let underlying (refinement: Refinement<'underlying, 'refined>) (value: 'refined) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Project value

    /// <summary>Returns the constraint the refinement admits by.</summary>
    /// <example><code>retryCount |> Refinement.constraint' |> Constraint.inspect</code></example>
    let constraint' (refinement: Refinement<'underlying, 'refined>) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Constraint
