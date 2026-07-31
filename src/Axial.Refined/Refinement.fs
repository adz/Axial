namespace Axial.Refined

open Axial.Check

/// Defines admission into an invariant-carrying value and its total reverse projection.
[<Sealed>]
type Refinement<'underlying, 'refined> internal
    (check: Check<'underlying>, constraints: Constraint<'underlying> list, construct: 'underlying -> 'refined, project: 'refined -> 'underlying) =
    member internal _.Check = check
    member internal _.Constraints = constraints
    member internal _.Construct = construct
    member internal _.Project = project

/// Creates and applies reusable refinement definitions.
[<RequireQualifiedAccess>]
module Refinement =
    let private ensureFunction name value = if isNull (box value) then nullArg name

    /// Defines a refinement from one portable constraint.
    let define (constraint': Constraint<'underlying>) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        if isNull constraint' then nullArg (nameof constraint')
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(Constraint.check constraint', [ constraint' ], construct, project)

    /// Defines a refinement from one or more portable constraints.
    let defineAll (constraints: Constraint<'underlying> list) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        if isNull (box constraints) then nullArg (nameof constraints)
        if List.isEmpty constraints then invalidArg (nameof constraints) "A refinement must contain at least one constraint."
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(Constraint.checkAll constraints, constraints, construct, project)

    /// Defines a metadata-free refinement from an executable check.
    let defineWithCheck (check: Check<'underlying>) (construct: 'underlying -> 'refined) (project: 'refined -> 'underlying) =
        ensureFunction (nameof check) check
        ensureFunction (nameof construct) construct
        ensureFunction (nameof project) project
        Refinement(check, [], construct, project)

    /// Constructs a refined value after its check succeeds.
    let create (refinement: Refinement<'underlying, 'refined>) (underlying: 'underlying) : Result<'refined, CheckFailure list> =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Check underlying |> Result.map (fun () -> refinement.Construct underlying)

    /// Returns the canonical underlying representation.
    let underlying (refinement: Refinement<'underlying, 'refined>) (value: 'refined) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Project value

    /// Returns portable constraints retained by the refinement.
    let constraints (refinement: Refinement<'underlying, 'refined>) =
        if isNull (box refinement) then nullArg (nameof refinement)
        refinement.Constraints
