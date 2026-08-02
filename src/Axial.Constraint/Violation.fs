namespace Axial.Constraint

/// <summary>A localizable message, addressed by key rather than rendered as English.</summary>
type MessageDescriptor =
    { /// <summary>The stable catalogue key, for example <c>constraint.cardinality.minimum</c>.</summary>
      Key: string
      /// <summary>The operands the message interpolates, named for the key's template.</summary>
      Arguments: Map<string, ConstraintValue> }

/// <summary>Why one indivisible constraint failed.</summary>
/// <remarks>
/// <para>
/// An interpreted constructor reports <c>Expected</c> carrying the very same <see cref="T:Axial.Constraint.ConstraintAtom" />
/// its description carries, so a consumer recovers the failing constraint's identity from the failure itself. No
/// code string is ever parsed to recover meaning.
/// </para>
/// <para>
/// <c>actual = None</c> means no actual value is available in portable form — either the value's type is outside
/// the portable set, or the rule could not compute one, as for the length of a null string. A portably-null actual
/// is <c>Some ConstraintValue.Null</c> and is distinct from both.
/// </para>
/// </remarks>
type AtomicViolation =
    /// <summary>An interpreted expectation was not met.</summary>
    | Expected of expectation: ConstraintAtom * actual: ConstraintValue option
    /// <summary>An opaque constraint failed, reported with its author-supplied prose.</summary>
    /// <remarks>
    /// The descriptor is the author's own catalogue key, supplied alongside the prose and used in preference to
    /// it when a message tree is projected. Axial never invents one — a key it made up would promise a lookup
    /// that cannot exist — but an author who has a resource catalogue can name the entry, which is the only way
    /// a custom rule's message can be translated at all. Absent a descriptor the prose passes through verbatim.
    /// </remarks>
    | Described of description: string * key: MessageDescriptor option
    /// <summary>A built-in rule failed whose operand has no portable representation.</summary>
    | UnsupportedOperand of operation: UnsupportedOperation

/// <summary>Why a value failed its constraint.</summary>
/// <remarks>
/// <para>
/// A diagnostic contract, not an application error union. Domain code maps a whole violation once with
/// <c>Result.mapError</c>; Schema adds the path at which it occurred.
/// </para>
/// <para>
/// Violations are plain comparable data. No closure and no constraint description is reachable from one, so
/// structural equality holds and a violation can be retained and compared long after the constraint that produced
/// it went out of scope. There is no promised wire format.
/// </para>
/// <para>
/// Axial-produced groups are never empty and never unary: a single failing child is returned directly rather than
/// wrapped. The <c>first * rest</c> shape encodes non-emptiness only; non-unarity is a normalization invariant.
/// </para>
/// </remarks>
type Violation =
    /// <summary>One indivisible failure.</summary>
    | Atomic of AtomicViolation
    /// <summary>Every listed failure occurred; the value failed several conjoined rules.</summary>
    | All of first: Violation * rest: Violation list
    /// <summary>No alternative succeeded; each listed failure is one rejected branch.</summary>
    | Any of first: Violation * rest: Violation list

/// <summary>One leaf of a projected message tree.</summary>
/// <remarks>
/// Author-supplied prose on an opaque constraint passes through verbatim unless the author also supplied their
/// own catalogue key. Axial never invents one: a key it made up would promise a lookup that cannot exist.
/// </remarks>
[<RequireQualifiedAccess>]
type MessageLeaf =
    /// <summary>A library failure addressed by catalogue key.</summary>
    | Localized of MessageDescriptor
    /// <summary>Author-supplied prose, passed through unchanged.</summary>
    | Verbatim of string

/// <summary>A violation projected for an external localization system, retaining its grouping.</summary>
[<RequireQualifiedAccess>]
type MessageTree =
    /// <summary>One message.</summary>
    | Leaf of MessageLeaf
    /// <summary>Messages for conjoined failures.</summary>
    | All of first: MessageTree * rest: MessageTree list
    /// <summary>Messages for rejected alternatives.</summary>
    | Any of first: MessageTree * rest: MessageTree list

/// <summary>Inspects, traverses, and renders violations.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Violation =
    /// <summary>
    /// Groups failures as a conjunction, returning <c>None</c> for no failures and the single failure unchanged
    /// for one.
    /// </summary>
    /// <remarks>
    /// This is the normalization Axial itself applies, so an interpreter that accumulates failures from several
    /// constraints produces exactly the tree a single composed constraint would have produced. Axial-produced
    /// groups are therefore never empty and never unary.
    /// </remarks>
    /// <example><code>[ first; second ] |> Violation.conjoin // Some (All (first, [ second ]))</code></example>
    let conjoin violations =
        match violations with
        | [] -> None
        | [ single ] -> Some single
        | first :: rest -> Some(All(first, rest))

    /// <summary>
    /// Groups failures as rejected alternatives, returning <c>None</c> for no failures and the single failure
    /// unchanged for one.
    /// </summary>
    /// <example><code>[ first; second ] |> Violation.alternatives // Some (Any (first, [ second ]))</code></example>
    let alternatives violations =
        match violations with
        | [] -> None
        | [ single ] -> Some single
        | first :: rest -> Some(Any(first, rest))

    /// <summary>The immediate children of a group, or an empty list for an atomic violation.</summary>
    let children (violation: Violation) : Violation list =
        match violation with
        | Atomic _ -> []
        | All(first, rest)
        | Any(first, rest) -> first :: rest

    /// <summary>Every leaf of a violation tree, in report order.</summary>
    /// <example><code>violation |> Violation.flatten |> List.length</code></example>
    let flatten (violation: Violation) : AtomicViolation list =
        let rec collect violation acc =
            match violation with
            | Atomic atomic -> atomic :: acc
            | All(first, rest)
            | Any(first, rest) -> List.foldBack collect (first :: rest) acc

        collect violation []

    /// <summary>The failing constraint's identity, when the violation is a single interpreted leaf.</summary>
    let tryExpectation (violation: Violation) : ConstraintAtom option =
        match violation with
        | Atomic(Expected(expectation, _)) -> Some expectation
        | _ -> None

    /// <summary>The value that failed, when the violation is a single leaf carrying a portable one.</summary>
    let tryActual (violation: Violation) : ConstraintValue option =
        match violation with
        | Atomic(Expected(_, actual)) -> actual
        | _ -> None

    /// <summary>The author-supplied prose, when the violation is a single opaque leaf.</summary>
    let tryDescription (violation: Violation) : string option =
        match violation with
        | Atomic(Described(description, _)) -> Some description
        | _ -> None

    /// <summary>The author-supplied catalogue key, when the violation is a single opaque leaf carrying one.</summary>
    let tryDescriptionKey (violation: Violation) : MessageDescriptor option =
        match violation with
        | Atomic(Described(_, key)) -> key
        | _ -> None

    let private renderAtomic (atomic: AtomicViolation) =
        match atomic with
        | Expected(expectation, actual) ->
            let expected = ConstraintAtom.render expectation

            match actual with
            | Some actual -> $"{expected}, but was {ConstraintValue.render actual}"
            | None -> expected
        | Described(description, _) -> description
        | UnsupportedOperand operation -> UnsupportedOperation.render operation

    /// <summary>
    /// Renders a violation as an English sentence fragment with no trailing punctuation, keeping conjunction and
    /// alternative groups distinct.
    /// </summary>
    /// <example><code>Violation.render (Atomic (Expected (PresenceAtom Present, None)))
    /// // "value must be present"</code></example>
    let rec render (violation: Violation) : string =
        match violation with
        | Atomic atomic -> renderAtomic atomic
        | All(first, rest) -> first :: rest |> List.map render |> String.concat "; "
        | Any(first, rest) -> first :: rest |> List.map render |> String.concat ", or "

    /// <summary>
    /// Projects a violation for an external localization system, preserving its grouping so a translator renders
    /// conjunctions and alternatives in their own word order.
    /// </summary>
    /// <example><code>match Violation.toMessageTree violation with
    /// | MessageTree.Leaf (MessageLeaf.Localized descriptor) -> descriptor.Key
    /// | _ -> "constraint.group"</code></example>
    let rec toMessageTree (violation: Violation) : MessageTree =
        match violation with
        | Atomic(Expected(expectation, actual)) ->
            let arguments =
                match actual with
                | Some actual -> ConstraintAtom.arguments expectation |> Map.add "actual" actual
                | None -> ConstraintAtom.arguments expectation

            MessageTree.Leaf(MessageLeaf.Localized { Key = ConstraintAtom.key expectation; Arguments = arguments })
        | Atomic(Described(description, key)) ->
            match key with
            | Some descriptor -> MessageTree.Leaf(MessageLeaf.Localized descriptor)
            | None -> MessageTree.Leaf(MessageLeaf.Verbatim description)
        | Atomic(UnsupportedOperand operation) ->
            MessageTree.Leaf(MessageLeaf.Localized { Key = UnsupportedOperation.key operation; Arguments = Map.empty })
        | All(first, rest) -> MessageTree.All(toMessageTree first, rest |> List.map toMessageTree)
        | Any(first, rest) -> MessageTree.Any(toMessageTree first, rest |> List.map toMessageTree)

    /// <summary>
    /// Renders a violation through a caller-supplied lookup, keeping the same grouping and separators
    /// <c>render</c> uses.
    /// </summary>
    /// <remarks>
    /// The whole localization path in one call. <c>toMessageTree</c> remains available for a translator that
    /// needs to control word order across a group; this is for the common case, where a resource lookup per
    /// message is the entire job and matching the tree by hand is pure ceremony. Verbatim leaves — author prose
    /// with no catalogue key — are passed through untranslated, because there is nothing to look up.
    /// </remarks>
    /// <example><code>violation |> Violation.renderWith (fun descriptor -> resources.Format(descriptor.Key, descriptor.Arguments))</code></example>
    let renderWith (lookup: MessageDescriptor -> string) (violation: Violation) : string =
        let rec go tree =
            match tree with
            | MessageTree.Leaf(MessageLeaf.Localized descriptor) -> lookup descriptor
            | MessageTree.Leaf(MessageLeaf.Verbatim prose) -> prose
            | MessageTree.All(first, rest) -> first :: rest |> List.map go |> String.concat "; "
            | MessageTree.Any(first, rest) -> first :: rest |> List.map go |> String.concat ", or "

        violation |> toMessageTree |> go
