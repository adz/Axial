namespace Axial.Constraint

open System
open System.ComponentModel

/// <summary>
/// A reusable description of valid values, coupled to the closures that execute it.
/// </summary>
/// <remarks>
/// <para>
/// One constraint value serves direct checking, refined-value admission, Schema, documentation, and export. There
/// is no separate check type: <c>check</c> is the operation, <c>Constraint</c> is the noun.
/// </para>
/// <para>
/// Both closures are retained deliberately. They are not duplicates of one rule: <c>test</c> over a conjunction may
/// stop at the first failing child, while <c>check</c> must run every child to accumulate. Interpreted atoms and
/// <c>custom</c> predicates therefore have a Boolean path that does no violation work, and combinators preserve
/// that property when every child has it. A <c>customWith</c> constraint supplies only a violation-returning
/// callback, so its <c>test</c> runs that callback and discards the error.
/// </para>
/// <para>
/// The description is never interpreted during execution. Closures are composed once, at construction.
/// </para>
/// </remarks>
[<Sealed; AllowNullLiteral>]
type Constraint<'value>
    internal (test: 'value -> bool, check: 'value -> Result<unit, Violation>, description: ConstraintDescription) =
    member internal _.TestValue = test
    member internal _.CheckValue = check
    member internal _.DescriptionValue = description

/// <summary>Creates, executes, composes, and inspects constraints.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Constraint =
    let private ensureConstraint name (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg name

    let private ensureFunction name value =
        if isNull (box value) then nullArg name

    let private ensureProse name (value: string) =
        if isNull value then nullArg name
        if String.IsNullOrWhiteSpace value then invalidArg name "Constraint prose must not be blank."

    let private ensureNonNegative name value =
        if value < 0 then Platform.argumentOutOfRange name value "Constraint bounds must be non-negative."

    let private ensureBounds name minimum maximum =
        if minimum > maximum then invalidArg name "The minimum bound must not exceed the maximum bound."

    // -- Private construction -------------------------------------------------------------------------------

    /// Builds both closures and the description from one immutable atom, so a primitive's identity and its
    /// failure are the same value rather than two hand-maintained copies.
    let internal atomic (atom: ConstraintAtom) (predicate: 'value -> bool) (actual: 'value -> ConstraintValue option) =
        Constraint<'value>(
            predicate,
            (fun value -> if predicate value then Ok() else Error(Atomic(Expected(atom, actual value)))),
            ConstraintDescription.ofAtom atom
        )

    /// Builds a constraint whose operand has no portable representation. It still executes against its typed
    /// closure; only its description and its failure decline to name the operand.
    let internal unsupported (operation: UnsupportedOperation) (predicate: 'value -> bool) =
        Constraint<'value>(
            predicate,
            (fun value -> if predicate value then Ok() else Error(Atomic(UnsupportedOperand operation))),
            ConstraintDescription.ofExpression (
                ConstraintExpression.Opaque(OpaqueConstraint.UnsupportedOperand operation)
            )
        )

    /// Rebuilds a constraint over an equivalent representation, retaining its logical description. Library-owned
    /// only: an arbitrary projection changes the proposition and must go through `contramap` instead.
    let internal adapt (project: 'input -> 'value) (constraint': Constraint<'value>) : Constraint<'input> =
        Constraint<'input>(
            project >> constraint'.TestValue,
            project >> constraint'.CheckValue,
            constraint'.DescriptionValue
        )

    let private portable value = ConstraintValue.tryCreate value

    // -- Execution ------------------------------------------------------------------------------------------

    /// <summary>Answers whether a value satisfies a constraint, without building a violation.</summary>
    /// <example><code>let retryCount = Constraint.between 0 10
    /// 3 |> Constraint.test retryCount // true</code></example>
    let test (constraint': Constraint<'value>) (value: 'value) : bool =
        ensureConstraint (nameof constraint') constraint'
        constraint'.TestValue value

    /// <summary>Runs a constraint, returning why the value failed.</summary>
    /// <example><code>let retryCount = Constraint.between 0 10
    /// 42 |> Constraint.check retryCount |> Result.mapError Violation.render</code></example>
    let check (constraint': Constraint<'value>) (value: 'value) : Result<unit, Violation> =
        ensureConstraint (nameof constraint') constraint'
        constraint'.CheckValue value

    /// <summary>Runs a constraint and returns the unchanged value after success.</summary>
    /// <example><code>value |> Constraint.guard requiredName |> Result.mapError NameRejected</code></example>
    let guard (constraint': Constraint<'value>) (value: 'value) : Result<'value, Violation> =
        check constraint' value |> Result.map (fun () -> value)

    /// <summary>Returns the constraint's inspectable description.</summary>
    /// <example><code>(Constraint.inspect requiredName).Expression</code></example>
    let inspect (constraint': Constraint<'value>) : ConstraintDescription =
        ensureConstraint (nameof constraint') constraint'
        constraint'.DescriptionValue

    // -- Composition ----------------------------------------------------------------------------------------

    /// <summary>
    /// Requires every constraint to hold, evaluating each in declaration order and accumulating failures. The
    /// empty list is the satisfied identity.
    /// </summary>
    /// <remarks>
    /// F# visits list elements left to right, so annotate the binding when the first element is a type-directed
    /// value: <c>let requiredName : Constraint&lt;string&gt; = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]</c>.
    /// </remarks>
    /// <example><code>let requiredName : Constraint&lt;string&gt; =
    ///     Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]</code></example>
    let all (constraints: Constraint<'value> list) : Constraint<'value> =
        if isNull (box constraints) then nullArg (nameof constraints)
        constraints |> List.iter (ensureConstraint (nameof constraints))

        let tests = constraints |> List.map (fun item -> item.TestValue)
        let checks = constraints |> List.map (fun item -> item.CheckValue)

        Constraint<'value>(
            (fun value -> tests |> List.forall (fun test -> test value)),
            (fun value ->
                let failures =
                    checks
                    |> List.choose (fun check ->
                        match check value with
                        | Ok() -> None
                        | Error violation -> Some violation)

                match Violation.conjoin failures with
                | None -> Ok()
                | Some violation -> Error violation),
            ConstraintDescription.ofExpression (
                ConstraintExpression.All(constraints |> List.map (fun item -> item.DescriptionValue))
            )
        )

    /// <summary>
    /// Requires at least one alternative to hold, evaluating left to right and stopping at the first success. When
    /// none succeeds, every rejected branch is reported.
    /// </summary>
    /// <remarks>
    /// Taking the first branch separately keeps an unsatisfiable empty disjunction unrepresentable, so this never
    /// throws. Alternatives among rules are what neither <c>oneOf</c> (alternatives among literals) nor a range
    /// (one contiguous region) can express — a valid set with a hole in it, such as a wire value that is either a
    /// sentinel or a duration.
    /// </remarks>
    /// <example><code>let ttl : Constraint&lt;int&gt; =
    ///     Constraint.any (Constraint.equalTo -1) [ Constraint.atLeast 1 ]</code></example>
    let any (first: Constraint<'value>) (rest: Constraint<'value> list) : Constraint<'value> =
        ensureConstraint (nameof first) first
        if isNull (box rest) then nullArg (nameof rest)
        rest |> List.iter (ensureConstraint (nameof rest))

        let branches = first :: rest

        Constraint<'value>(
            (fun value -> branches |> List.exists (fun branch -> branch.TestValue value)),
            (fun value ->
                let rec loop failures remaining =
                    match remaining with
                    | [] ->
                        match List.rev failures with
                        | [] -> Ok()
                        | [ single ] -> Error single
                        | first :: rest -> Error(Any(first, rest))
                    | (branch: Constraint<'value>) :: remaining ->
                        match branch.CheckValue value with
                        | Ok() -> Ok()
                        | Error violation -> loop (violation :: failures) remaining

                loop [] branches),
            ConstraintDescription.ofExpression (
                ConstraintExpression.Any(first.DescriptionValue, rest |> List.map (fun item -> item.DescriptionValue))
            )
        )

    /// <summary>
    /// Negates a constraint. The result is opaque: it runs normally but cannot be exported or proved, and reports
    /// the supplied prose.
    /// </summary>
    /// <remarks>
    /// The prose is required because there is no honest general complement to derive one from. Membership, format,
    /// uniqueness, and numeric families have no complement inside their family; float comparisons are not
    /// complementable under <c>NaN</c>, where both <c>x &gt; y</c> and <c>x &lt;= y</c> are false; and a cardinality
    /// complement would need bounds this catalogue rejects, such as a maximum of -1. An operation that is
    /// sometimes interpreted, sometimes needs prose, and sometimes cannot be constructed is worse than one that is
    /// honestly opaque.
    /// </remarks>
    /// <example><code>Constraint.notWith "must not be a reserved name" (Constraint.oneOf [ "admin"; "root" ])</code></example>
    let notWith (description: string) (constraint': Constraint<'value>) : Constraint<'value> =
        ensureProse (nameof description) description
        ensureConstraint (nameof constraint') constraint'

        let negated value = not (constraint'.TestValue value)

        Constraint<'value>(
            negated,
            (fun value -> if negated value then Ok() else Error(Atomic(Described description))),
            ConstraintDescription.ofExpression (
                ConstraintExpression.Opaque(OpaqueConstraint.RuntimeNegation(description, constraint'.DescriptionValue))
            )
        )

    /// <summary>Runs an arbitrary predicate, reporting the supplied prose when it fails.</summary>
    /// <remarks>
    /// Opaque by construction. It executes and composes normally, may appear in Schema and refinements, and is
    /// documented honestly by exporters — but it is invisible to export enforcement and to proof, because an
    /// arbitrary host-language closure has no logical meaning to translate. No authored code or argument may claim
    /// inspectable logic.
    /// </remarks>
    /// <example><code>Constraint.custom "must be a valid ISBN" isValidIsbn</code></example>
    let custom (description: string) (predicate: 'value -> bool) : Constraint<'value> =
        ensureProse (nameof description) description
        ensureFunction (nameof predicate) predicate

        Constraint<'value>(
            predicate,
            (fun value -> if predicate value then Ok() else Error(Atomic(Described description))),
            ConstraintDescription.ofExpression (ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate description))
        )

    /// <summary>Runs an arbitrary callback that reports its own violation.</summary>
    /// <remarks>
    /// Use this when the failure deserves a structured reason a bare predicate cannot give. Because the callback
    /// supplies only a violation-returning function, <c>test</c> runs it and discards the error, so a failing test
    /// costs whatever the callback allocates. Returning an <c>Expected</c> leaf makes no false portable claim: the
    /// enclosing description is still opaque.
    /// </remarks>
    /// <example><code>Constraint.customWith "must be a supported currency" (fun code ->
    ///     if supported.Contains code then Ok ()
    ///     else Error (Atomic (Expected (MembershipAtom (OneOf choices), ConstraintValue.tryCreate code))))</code></example>
    let customWith (description: string) (check: 'value -> Result<unit, Violation>) : Constraint<'value> =
        ensureProse (nameof description) description
        ensureFunction (nameof check) check

        Constraint<'value>(
            (fun value ->
                match check value with
                | Ok() -> true
                | Error _ -> false),
            check,
            ConstraintDescription.ofExpression (ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate description))
        )

    /// <summary>Applies a constraint to a projection of a larger value.</summary>
    /// <remarks>
    /// Opaque: an arbitrary projection changes the proposition in a way no description can express. The inner
    /// description is retained beneath the opacity boundary so documentation stays readable.
    /// </remarks>
    /// <example><code>Constraint.present |> Constraint.contramap (fun order -> order.Reference)</code></example>
    let contramap (project: 'input -> 'value) (constraint': Constraint<'value>) : Constraint<'input> =
        ensureFunction (nameof project) project
        ensureConstraint (nameof constraint') constraint'

        Constraint<'input>(
            project >> constraint'.TestValue,
            project >> constraint'.CheckValue,
            ConstraintDescription.ofExpression (
                ConstraintExpression.Opaque(OpaqueConstraint.RuntimeProjection constraint'.DescriptionValue)
            )
        )

    /// <summary>Attaches documentary prose to a constraint.</summary>
    /// <remarks>
    /// Non-diagnostic: it reaches inspection, documentation, and generated schema prose, but never a violation and
    /// never the constraint's logical meaning. Use <c>custom</c> or <c>customWith</c> to change what a failure says.
    /// </remarks>
    /// <example><code>Constraint.between 0 10 |> Constraint.describe "Retries before the call is abandoned."</code></example>
    let describe (description: string) (constraint': Constraint<'value>) : Constraint<'value> =
        ensureProse (nameof description) description
        ensureConstraint (nameof constraint') constraint'

        Constraint<'value>(
            constraint'.TestValue,
            constraint'.CheckValue,
            { constraint'.DescriptionValue with Description = Some description }
        )

    // -- Presence -------------------------------------------------------------------------------------------

    let private presenceOf (atom: Presence) (inhabited: 'value -> bool) =
        let predicate value = if atom = Present then inhabited value else not (inhabited value)
        atomic (PresenceAtom atom) predicate (fun _ -> None)

    let private textInhabited (value: string) = not (Predicates.isBlankText value)
    let private seqInhabited (values: #seq<'value>) = not (Predicates.isNullSeq values) && not (Seq.isEmpty values)
    let private mapInhabited (values: Map<string, 'value>) = not (isNull (box values)) && not (Map.isEmpty values)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type PresentDispatcher =
        static member Create(_: string) : Constraint<string> = presenceOf Present textInhabited
        static member Create(_: 'value option) : Constraint<'value option> = presenceOf Present Option.isSome
        static member Create(_: 'value voption) : Constraint<'value voption> = presenceOf Present ValueOption.isSome

        static member Create(_: Nullable<'value>) : Constraint<Nullable<'value>> =
            presenceOf Present (fun (value: Nullable<'value>) -> value.HasValue)

        static member Create(_: 'value list) : Constraint<'value list> = presenceOf Present seqInhabited
        static member Create(_: 'value array) : Constraint<'value array> = presenceOf Present seqInhabited

        static member Create(_: Map<string, 'value>) : Constraint<Map<string, 'value>> =
            presenceOf Present mapInhabited

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type BlankDispatcher =
        static member Create(_: string) : Constraint<string> = presenceOf Blank textInhabited
        static member Create(_: 'value option) : Constraint<'value option> = presenceOf Blank Option.isSome
        static member Create(_: 'value voption) : Constraint<'value voption> = presenceOf Blank ValueOption.isSome

        static member Create(_: Nullable<'value>) : Constraint<Nullable<'value>> =
            presenceOf Blank (fun (value: Nullable<'value>) -> value.HasValue)

        static member Create(_: 'value list) : Constraint<'value list> = presenceOf Blank seqInhabited
        static member Create(_: 'value array) : Constraint<'value array> = presenceOf Blank seqInhabited

        static member Create(_: Map<string, 'value>) : Constraint<Map<string, 'value>> = presenceOf Blank mapInhabited

    /// <summary>Requires a value to be inhabited according to its shape.</summary>
    /// <remarks>
    /// Whitespace-only text is blank, as are null text, a null or empty collection or map, <c>None</c>,
    /// <c>ValueNone</c>, and an empty <c>Nullable</c>. Annotate the binding so the compiler can select the shape:
    /// <c>let requiredName : Constraint&lt;string&gt; = Constraint.present</c>.
    /// </remarks>
    /// <example><code>let requiredName : Constraint&lt;string&gt; = Constraint.present</code></example>
    let inline present< ^value when (^value or PresentDispatcher): (static member Create: ^value -> Constraint< ^value >)>
        : Constraint< ^value > =
        ((^value or PresentDispatcher): (static member Create: ^value -> Constraint< ^value >) Unchecked.defaultof< ^value >)

    /// <summary>Requires a value to be uninhabited according to its shape; the exact complement of <c>present</c>.</summary>
    /// <remarks>
    /// This <em>requires</em> absence. To permit it, use <c>Constraint.optional</c>; to allow a property to be
    /// omitted from an input, use Schema's <c>mayOmit</c>.
    /// </remarks>
    /// <example><code>let mustBeUnset : Constraint&lt;string&gt; = Constraint.blank</code></example>
    let inline blank< ^value when (^value or BlankDispatcher): (static member Create: ^value -> Constraint< ^value >)>
        : Constraint< ^value > =
        ((^value or BlankDispatcher): (static member Create: ^value -> Constraint< ^value >) Unchecked.defaultof< ^value >)

    // -- Optionality ----------------------------------------------------------------------------------------

    let private optionalOf (isAbsent: 'container -> bool) (project: 'container -> 'value) (inner: Constraint<'value>) =
        ensureConstraint (nameof inner) inner

        Constraint<'container>(
            (fun container -> isAbsent container || inner.TestValue(project container)),
            (fun container ->
                if isAbsent container then
                    Ok()
                else
                    inner.CheckValue(project container)),
            ConstraintDescription.ofExpression (ConstraintExpression.Optional inner.DescriptionValue)
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type OptionalDispatcher =
        static member Create(_: 'value option, inner: Constraint<'value>) : Constraint<'value option> =
            optionalOf Option.isNone Option.get inner

        static member Create(_: 'value voption, inner: Constraint<'value>) : Constraint<'value voption> =
            optionalOf ValueOption.isNone ValueOption.get inner

        static member Create(_: Nullable<'value>, inner: Constraint<'value>) : Constraint<Nullable<'value>> =
            optionalOf (fun (container: Nullable<'value>) -> not container.HasValue) (fun container -> container.Value) inner

    /// <summary>Lifts a constraint over an optional container: absence passes, presence runs the inner constraint.</summary>
    /// <remarks>
    /// Orthogonal to <c>present</c> and <c>blank</c>, which respectively require inhabitation and require absence.
    /// This one permits absence.
    /// </remarks>
    /// <example><code>let nickname : Constraint&lt;string option&gt; =
    ///     Constraint.optional (Constraint.lengthBetween 2 40)</code></example>
    let inline optional< ^container, 'value when (^container or OptionalDispatcher): (static member Create:
        ^container * Constraint<'value> -> Constraint< ^container >)>
        (inner: Constraint<'value>)
        : Constraint< ^container > =
        ((^container or OptionalDispatcher): (static member Create: ^container * Constraint<'value> -> Constraint< ^container >) (Unchecked.defaultof< ^container >,
                                                                                                                                 inner))

    // -- Cardinality ----------------------------------------------------------------------------------------

    let private satisfiesCardinality cardinality count =
        match cardinality with
        | Exact expected -> count = expected
        | Cardinality.Minimum minimum -> count >= minimum
        | Cardinality.Maximum maximum -> count <= maximum
        | Cardinality.Between(minimum, maximum) -> count >= minimum && count <= maximum

    /// A null reference has no size to report, so it fails the rule with no actual value rather than being
    /// silently treated as empty. Presence is the constraint that speaks about inhabitation.
    let private cardinalityOf (cardinality: Cardinality) (count: 'value -> int option) =
        let atom = CardinalityAtom cardinality

        Constraint<'value>(
            (fun value -> count value |> Option.exists (satisfiesCardinality cardinality)),
            (fun value ->
                match count value with
                | Some actual when satisfiesCardinality cardinality actual -> Ok()
                | Some actual -> Error(Atomic(Expected(atom, Some(ConstraintValue.Integer(int64 actual)))))
                | None -> Error(Atomic(Expected(atom, None)))),
            ConstraintDescription.ofAtom atom
        )

    let private textCount (value: string) =
        if isNull value then None else Some(Predicates.textLength value)

    let private seqCount (values: #seq<'value>) =
        if Predicates.isNullSeq values then None else Some(Predicates.seqCount values)

    let private mapCount (values: Map<string, 'value>) =
        if isNull (box values) then None else Some values.Count

    let private validateCardinality (cardinality: Cardinality) =
        match cardinality with
        | Exact expected -> ensureNonNegative (nameof expected) expected
        | Cardinality.Minimum minimum -> ensureNonNegative (nameof minimum) minimum
        | Cardinality.Maximum maximum -> ensureNonNegative (nameof maximum) maximum
        | Cardinality.Between(minimum, maximum) ->
            ensureNonNegative (nameof minimum) minimum
            ensureNonNegative (nameof maximum) maximum
            ensureBounds (nameof minimum) minimum maximum

        cardinality

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type CardinalityDispatcher =
        static member Create(_: string, cardinality: Cardinality) : Constraint<string> =
            cardinalityOf (validateCardinality cardinality) textCount

        static member Create(_: 'value list, cardinality: Cardinality) : Constraint<'value list> =
            cardinalityOf (validateCardinality cardinality) seqCount

        static member Create(_: 'value array, cardinality: Cardinality) : Constraint<'value array> =
            cardinalityOf (validateCardinality cardinality) seqCount

        static member Create(_: Map<string, 'value>, cardinality: Cardinality) : Constraint<Map<string, 'value>> =
            cardinalityOf (validateCardinality cardinality) mapCount

    let inline private cardinality< ^value when (^value or CardinalityDispatcher): (static member Create:
        ^value * Cardinality -> Constraint< ^value >)>
        (expectation: Cardinality)
        : Constraint< ^value > =
        ((^value or CardinalityDispatcher): (static member Create: ^value * Cardinality -> Constraint< ^value >) (Unchecked.defaultof< ^value >,
                                                                                                                 expectation))

    /// <summary>Requires text or a collection to have exactly the supplied size.</summary>
    /// <remarks>Text sizes count Unicode code points, so one emoji counts once even though it is two UTF-16 units.</remarks>
    /// <example><code>let code : Constraint&lt;string&gt; = Constraint.length 6</code></example>
    let inline length< ^value when (^value or CardinalityDispatcher): (static member Create: ^value * Cardinality -> Constraint< ^value >)> expected : Constraint< ^value > =
        cardinality (Exact expected)

    /// <summary>Requires text or a collection to have at least the supplied size.</summary>
    /// <remarks>Literal size, so a single space satisfies <c>minLength 1</c>. Use <c>present</c> to reject whitespace.</remarks>
    /// <example><code>let tags : Constraint&lt;string list&gt; = Constraint.minLength 1</code></example>
    let inline minLength< ^value when (^value or CardinalityDispatcher): (static member Create: ^value * Cardinality -> Constraint< ^value >)> minimum : Constraint< ^value > =
        cardinality (Cardinality.Minimum minimum)

    /// <summary>Requires text or a collection to have at most the supplied size.</summary>
    /// <example><code>let summary : Constraint&lt;string&gt; = Constraint.maxLength 280</code></example>
    let inline maxLength< ^value when (^value or CardinalityDispatcher): (static member Create: ^value * Cardinality -> Constraint< ^value >)> maximum : Constraint< ^value > =
        cardinality (Cardinality.Maximum maximum)

    /// <summary>Requires a text or collection size inside the supplied inclusive bounds.</summary>
    /// <example><code>let name : Constraint&lt;string&gt; = Constraint.lengthBetween 2 40</code></example>
    let inline lengthBetween< ^value when (^value or CardinalityDispatcher): (static member Create: ^value * Cardinality -> Constraint< ^value >)> minimum maximum : Constraint< ^value > =
        cardinality (Cardinality.Between(minimum, maximum))

    // -- Formats --------------------------------------------------------------------------------------------

    /// <summary>The exact regular expression <c>Constraint.email</c> runs.</summary>
    /// <remarks>
    /// Published so an exporter can lower the rule rather than approximate it. The pattern contains no letters,
    /// so the compiled case-insensitivity is inert and the same source text means the same thing under ECMA-262.
    /// </remarks>
    let emailPattern = Predicates.emailPattern

    /// <summary>The exact regular expression <c>Constraint.numeric</c> runs.</summary>
    /// <remarks>
    /// ASCII digits, so the pattern means the same thing in .NET and ECMA-262. A <c>\d</c> rule would not: .NET
    /// matches any Unicode decimal digit while ECMA-262 matches <c>[0-9]</c>.
    /// </remarks>
    let numericPattern = Predicates.numericPattern

    let private format expectation (predicate: string -> bool) =
        atomic (FormatAtom expectation) predicate (fun (value: string) ->
            if isNull (box value) then Some ConstraintValue.Null else Some(ConstraintValue.Text value))

    /// <summary>Requires text to match Axial's pragmatic email shape, <c>^[^@]+@[^@]+$</c>.</summary>
    /// <example><code>let contact : Constraint&lt;string&gt; = Constraint.email</code></example>
    let email: Constraint<string> = format Email Predicates.isEmail

    /// <summary>Requires text to have no leading or trailing whitespace.</summary>
    /// <example><code>let slug : Constraint&lt;string&gt; = Constraint.trimmed</code></example>
    let trimmed: Constraint<string> = format Trimmed Predicates.isTrimmed

    /// <summary>Requires text to be one or more ASCII digits.</summary>
    /// <remarks>
    /// ASCII rather than <c>\d</c>. .NET's <c>\d</c> matches any Unicode decimal digit while ECMA-262's matches
    /// <c>[0-9]</c>, so a Unicode rule could not be exported to JSON Schema without the exported schema rejecting
    /// values the library accepts.
    /// </remarks>
    /// <example><code>let pin : Constraint&lt;string&gt; = Constraint.numeric</code></example>
    let numeric: Constraint<string> = format Numeric Predicates.isNumeric

    /// <summary>Requires text to be one or more letters or digits.</summary>
    /// <example><code>let handle : Constraint&lt;string&gt; = Constraint.alphanumeric</code></example>
    let alphanumeric: Constraint<string> = format Alphanumeric Predicates.isAlphanumeric

    /// <summary>Requires text to match the supplied .NET regular expression.</summary>
    /// <remarks>
    /// The pattern is inspectable and portable as a string, but its <em>meaning</em> is the .NET dialect, which is
    /// not ECMA-262. Exporters therefore retain an arbitrary pattern as runtime-only metadata unless it is proven
    /// to lie in the common subset.
    /// </remarks>
    /// <example><code>let reference : Constraint&lt;string&gt; = Constraint.pattern @"^[A-Z]{3}-\d{4}$"</code></example>
    let pattern (expression: string) : Constraint<string> =
        ensureProse (nameof expression) expression
        format (Pattern expression) (Predicates.matchesPattern expression)

    // -- Relations ------------------------------------------------------------------------------------------

    let private relation operator (expected: 'value) (predicate: 'value -> bool) =
        match portable expected with
        | Some expected -> atomic (RelationAtom(Compared(operator, expected))) predicate portable
        | None -> unsupported (UnsupportedOperation.Relation operator) predicate

    /// <summary>Requires equality with the supplied value, under F# structural equality.</summary>
    /// <example><code>let mustBeDraft : Constraint&lt;Status&gt; = Constraint.equalTo Status.Draft</code></example>
    let equalTo (expected: 'value) : Constraint<'value> =
        relation Equal expected (fun actual -> actual = expected)

    /// <summary>Requires inequality with the supplied value, under F# structural equality.</summary>
    /// <example><code>let notReserved : Constraint&lt;string&gt; = Constraint.notEqualTo "admin"</code></example>
    let notEqualTo (unexpected: 'value) : Constraint<'value> =
        relation NotEqual unexpected (fun actual -> actual <> unexpected)

    /// <summary>Requires a value strictly greater than the supplied bound.</summary>
    /// <example><code>let quantity : Constraint&lt;int&gt; = Constraint.greaterThan 0</code></example>
    let greaterThan (minimum: 'value) : Constraint<'value> =
        relation GreaterThan minimum (fun actual -> actual > minimum)

    /// <summary>Requires a value strictly less than the supplied bound.</summary>
    /// <example><code>let discount : Constraint&lt;decimal&gt; = Constraint.lessThan 1.0M</code></example>
    let lessThan (maximum: 'value) : Constraint<'value> =
        relation LessThan maximum (fun actual -> actual < maximum)

    /// <summary>Requires a value greater than or equal to the supplied bound.</summary>
    /// <example><code>let age : Constraint&lt;int&gt; = Constraint.atLeast 13</code></example>
    let atLeast (minimum: 'value) : Constraint<'value> =
        relation AtLeast minimum (fun actual -> actual >= minimum)

    /// <summary>Requires a value less than or equal to the supplied bound.</summary>
    /// <example><code>let weight : Constraint&lt;int&gt; = Constraint.atMost 100</code></example>
    let atMost (maximum: 'value) : Constraint<'value> =
        relation AtMost maximum (fun actual -> actual <= maximum)

    /// <summary>Requires a value inside the supplied inclusive bounds.</summary>
    /// <example><code>let retryCount : Constraint&lt;int&gt; = Constraint.between 0 10</code></example>
    let between (minimum: 'value) (maximum: 'value) : Constraint<'value> =
        ensureBounds (nameof minimum) minimum maximum
        let predicate actual = actual >= minimum && actual <= maximum

        match portable minimum, portable maximum with
        | Some minimum, Some maximum -> atomic (RelationAtom(Within(minimum, maximum))) predicate portable
        | _ -> unsupported UnsupportedOperation.Within predicate

    // -- Membership -----------------------------------------------------------------------------------------

    /// <summary>Requires the value to equal one of the supplied choices.</summary>
    /// <example><code>let currency : Constraint&lt;string&gt; = Constraint.oneOf [ "AUD"; "NZD" ]</code></example>
    let oneOf (choices: 'value seq) : Constraint<'value> =
        if isNull (box choices) then nullArg (nameof choices)
        let choices = choices |> Seq.toList
        let predicate actual = choices |> List.contains actual

        let portableChoices =
            (Some [], choices)
            ||> List.fold (fun state choice ->
                state |> Option.bind (fun collected -> portable choice |> Option.map (fun choice -> choice :: collected)))
            |> Option.map List.rev

        match portableChoices with
        | Some portableChoices -> atomic (MembershipAtom(OneOf portableChoices)) predicate portable
        | None -> unsupported (UnsupportedOperation.Relation Equal) predicate

    let private containsOf (expected: 'value) (values: #seq<'value> -> bool) =
        match portable expected with
        | Some expected -> atomic (MembershipAtom(Membership.Contains expected)) values (fun _ -> None)
        | None -> unsupported UnsupportedOperation.Contains values

    let private uniquenessOf (duplicate: #seq<'value> -> 'value option) =
        let atom = UniquenessAtom

        Constraint<#seq<'value>>(
            (fun values -> not (Predicates.isNullSeq values) && (duplicate values) |> Option.isNone),
            (fun values ->
                if Predicates.isNullSeq values then
                    Error(Atomic(Expected(atom, None)))
                else
                    match duplicate values with
                    | None -> Ok()
                    | Some duplicate -> Error(Atomic(Expected(atom, portable duplicate)))),
            ConstraintDescription.ofAtom atom
        )

    let private containsIn (expected: 'value) (values: #seq<'value>) =
        not (Predicates.isNullSeq values) && Predicates.seqContains expected values

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type ContainsDispatcher =
        static member Create(_: 'value list, expected: 'value) : Constraint<'value list> = containsOf expected (containsIn expected)
        static member Create(_: 'value array, expected: 'value) : Constraint<'value array> = containsOf expected (containsIn expected)
        static member Create(_: 'value seq, expected: 'value) : Constraint<'value seq> = containsOf expected (containsIn expected)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type DistinctDispatcher =
        static member Create(_: 'value list) : Constraint<'value list> = uniquenessOf Predicates.tryFirstDuplicate
        static member Create(_: 'value array) : Constraint<'value array> = uniquenessOf Predicates.tryFirstDuplicate
        static member Create(_: 'value seq) : Constraint<'value seq> = uniquenessOf Predicates.tryFirstDuplicate

    /// <summary>Requires a collection to contain the supplied item.</summary>
    /// <example><code>let mustIncludeAdmin : Constraint&lt;string list&gt; = Constraint.contains "admin"</code></example>
    let inline contains< ^container, 'value when (^container or ContainsDispatcher): (static member Create:
        ^container * 'value -> Constraint< ^container >)>
        (expected: 'value)
        : Constraint< ^container > =
        ((^container or ContainsDispatcher): (static member Create: ^container * 'value -> Constraint< ^container >) (Unchecked.defaultof< ^container >,
                                                                                                                     expected))

    /// <summary>Requires a collection to hold no duplicates. The first repeat is reported as the actual value.</summary>
    /// <example><code>let tags : Constraint&lt;string list&gt; = Constraint.distinct</code></example>
    let inline distinct< ^container when (^container or DistinctDispatcher): (static member Create:
        ^container -> Constraint< ^container >)>
        : Constraint< ^container > =
        ((^container or DistinctDispatcher): (static member Create: ^container -> Constraint< ^container >) Unchecked.defaultof< ^container >)

    // -- Numeric properties ---------------------------------------------------------------------------------

    let private multipleOfWith (divisor: 'divisor) (predicate: 'value -> bool) =
        match portable divisor with
        | Some divisor -> atomic (NumberAtom(MultipleOf divisor)) predicate portable
        | None -> unsupported UnsupportedOperation.MultipleOf predicate

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type MultipleOfDispatcher =
        static member Create(divisor: int) = multipleOfWith divisor (fun value -> value % divisor = 0)
        static member Create(divisor: int64) = multipleOfWith divisor (fun value -> value % divisor = 0L)
        static member Create(divisor: decimal) = multipleOfWith divisor (fun value -> value % divisor = 0M)
        static member Create(divisor: float) = multipleOfWith divisor (fun value -> value % divisor = 0.0)
        static member Create(divisor: float32) = multipleOfWith divisor (fun value -> value % divisor = 0.0f)
        static member Create(divisor: bigint) = multipleOfWith divisor (fun value -> value % divisor = 0I)

    /// <summary>Requires an exact multiple of the supplied divisor, under the value type's own arithmetic.</summary>
    /// <remarks>
    /// IEEE remainders are not the mathematical ones: <c>0.3 % 0.1</c> is not zero, so a float rule rejects values
    /// a mathematical reading accepts. Exporters therefore lower only integral and decimal divisors.
    /// </remarks>
    /// <example><code>let batchSize : Constraint&lt;int&gt; = Constraint.multipleOf 10</code></example>
    let inline multipleOf (divisor: ^value) : Constraint< ^value > =
        ((^value or MultipleOfDispatcher): (static member Create: ^value -> Constraint< ^value >) divisor)

    /// <summary>Requires a double to be neither infinite nor <c>NaN</c>.</summary>
    /// <remarks>
    /// <c>NaN</c> compares false against every value including itself, which silently corrupts sorting and makes a
    /// value unusable as a dictionary key. Excluding it is what makes ordering lawful.
    /// </remarks>
    /// <example><code>let ratio : Constraint&lt;float&gt; = Constraint.finite</code></example>
    let finite: Constraint<float> =
        atomic (NumberAtom Finite) (fun value -> not (Double.IsNaN value || Double.IsInfinity value)) portable

    /// <summary>Requires a single-precision float to be neither infinite nor <c>NaN</c>.</summary>
    /// <example><code>let ratio : Constraint&lt;float32&gt; = Constraint.finite32</code></example>
    let finite32: Constraint<float32> =
        atomic (NumberAtom Finite) (fun value -> not (Single.IsNaN value || Single.IsInfinity value)) portable
