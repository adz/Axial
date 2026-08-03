namespace Axial.Constraint

/// <summary>Why a constraint is invisible to export and proof.</summary>
/// <remarks>
/// Each case carries exactly what that kind of opacity needs, so a custom predicate cannot claim an inner tree and
/// a projection cannot lack one. Diagnostic prose lives here; the separate documentary <c>Description</c> field on
/// a description node belongs to <c>Constraint.describe</c> and never affects a violation.
/// </remarks>
[<RequireQualifiedAccess>]
type OpaqueConstraint =
    /// <summary>An arbitrary user predicate, reported with the supplied prose.</summary>
    | CustomPredicate of description: string
    /// <summary>A negation of an inner constraint. The inner tree is descriptive only.</summary>
    | RuntimeNegation of description: string * inner: ConstraintDescription
    /// <summary>An arbitrary user projection applied before the inner constraint.</summary>
    | RuntimeProjection of inner: ConstraintDescription
    /// <summary>A built-in operation whose operand has no portable representation.</summary>
    | UnsupportedOperand of operation: UnsupportedOperation

/// <summary>The logical form of a constraint.</summary>
and [<RequireQualifiedAccess>] ConstraintExpression =
    /// <summary>One interpreted primitive.</summary>
    | Atom of ConstraintAtom
    /// <summary>A conjunction. The empty list is the satisfied identity.</summary>
    | All of ConstraintDescription list
    /// <summary>A disjunction, which always has at least one branch.</summary>
    | Any of first: ConstraintDescription * rest: ConstraintDescription list
    /// <summary>A lift over an optional container: absence passes, presence delegates to the inner constraint.</summary>
    | Optional of ConstraintDescription
    /// <summary>A constraint that runs normally but cannot be exported or proved.</summary>
    | Opaque of OpaqueConstraint

/// <summary>
/// What a constraint says, as inspectable data.
/// </summary>
/// <remarks>
/// <para>
/// This is the read model <c>Constraint.inspect</c> returns and the source every interpreter reads. It is never
/// interpreted during execution: a constraint's closures are composed once at construction.
/// </para>
/// <para>
/// A description is contextual, not standalone. Atoms are shape-neutral, so an interpreter combines a description
/// with the surrounding schema shape — <c>Cardinality.Maximum 5</c> becomes <c>maxLength</c>, <c>maxItems</c>, or
/// <c>maxProperties</c> depending on what it is attached to.
/// </para>
/// <para>
/// An opaque child never erases its portable siblings or parents: the surrounding structure stays inspectable and
/// only the opaque node itself declines export.
/// </para>
/// </remarks>
and ConstraintDescription =
    { /// <summary>Non-diagnostic prose attached by <c>Constraint.describe</c>, for documentation and inspection.</summary>
      Description: string option
      /// <summary>The constraint's logical form.</summary>
      Expression: ConstraintExpression }

/// <summary>Builds and traverses constraint descriptions.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ConstraintDescription =
    /// <summary>A description with no attached prose.</summary>
    let internal ofExpression expression = { Description = None; Expression = expression }

    /// <summary>A description of one interpreted primitive.</summary>
    let internal ofAtom atom = ofExpression (ConstraintExpression.Atom atom)

    /// <summary>The immediate child descriptions of a node, in authored order.</summary>
    let children (description: ConstraintDescription) : ConstraintDescription list =
        match description.Expression with
        | ConstraintExpression.Atom _ -> []
        | ConstraintExpression.All children -> children
        | ConstraintExpression.Any(first, rest) -> first :: rest
        | ConstraintExpression.Optional inner -> [ inner ]
        | ConstraintExpression.Opaque(OpaqueConstraint.RuntimeNegation(_, inner))
        | ConstraintExpression.Opaque(OpaqueConstraint.RuntimeProjection inner) -> [ inner ]
        | ConstraintExpression.Opaque _ -> []

    /// <summary>True when the node itself declines export and proof. Its children may still be inspectable.</summary>
    let isOpaque (description: ConstraintDescription) =
        match description.Expression with
        | ConstraintExpression.Opaque _ -> true
        | _ -> false

    /// <summary>Every interpreted primitive reachable without crossing an opacity boundary, in authored order.</summary>
    /// <remarks>
    /// Use this only where dropping an unexportable sibling stays sound. Dropping a conjunct weakens a conjunction
    /// and dropping a disjunct strengthens a disjunction, so an interpreter that claims enforcement must consult
    /// the whole expression rather than this projection.
    /// </remarks>
    let rec atoms (description: ConstraintDescription) : ConstraintAtom list =
        match description.Expression with
        | ConstraintExpression.Atom atom -> [ atom ]
        | ConstraintExpression.All children -> children |> List.collect atoms
        | ConstraintExpression.Any(first, rest) -> first :: rest |> List.collect atoms
        | ConstraintExpression.Optional inner -> atoms inner
        | ConstraintExpression.Opaque _ -> []
