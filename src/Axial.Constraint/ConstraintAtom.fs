namespace Axial.Constraint

/// <summary>What a presence rule expects of a value's shape.</summary>
/// <remarks>
/// <c>Present</c> and <c>Blank</c> are exact complements for every supported reference and container shape. Null
/// text, a null or empty list, array or map, <c>None</c>, <c>ValueNone</c>, an empty <c>Nullable</c>, and
/// whitespace-only text are all blank.
/// </remarks>
type Presence =
    /// <summary>The value must be inhabited according to its shape.</summary>
    | Present
    /// <summary>The value must be uninhabited according to its shape.</summary>
    | Blank

/// <summary>What a size rule expects of a text length or collection count.</summary>
/// <remarks>
/// Shape-neutral. An interpreter combines it with the schema shape to reach <c>maxLength</c>, <c>maxItems</c>, or
/// <c>maxProperties</c>. Text sizes count Unicode code points, not UTF-16 code units.
/// </remarks>
type Cardinality =
    /// <summary>Exactly the supplied size.</summary>
    | Exact of int
    /// <summary>At least the supplied size.</summary>
    | Minimum of int
    /// <summary>At most the supplied size.</summary>
    | Maximum of int
    /// <summary>A size inside the supplied inclusive bounds.</summary>
    | Between of minimum: int * maximum: int

/// <summary>The comparison a relation asserts between a value and an operand.</summary>
type RelationOperator =
    /// <summary>Values must be equal.</summary>
    | Equal
    /// <summary>Values must differ.</summary>
    | NotEqual
    /// <summary>The value must be strictly greater than the operand.</summary>
    | GreaterThan
    /// <summary>The value must be strictly less than the operand.</summary>
    | LessThan
    /// <summary>The value must be greater than or equal to the operand.</summary>
    | AtLeast
    /// <summary>The value must be less than or equal to the operand.</summary>
    | AtMost

/// <summary>What an ordering or equality rule expects.</summary>
type Relation =
    /// <summary>The value compares to the operand under the supplied operator.</summary>
    | Compared of RelationOperator * expected: ConstraintValue
    /// <summary>The value lies inside the supplied inclusive bounds.</summary>
    | Within of minimum: ConstraintValue * maximum: ConstraintValue

/// <summary>What a membership rule expects.</summary>
type Membership =
    /// <summary>The value equals one of the supplied choices.</summary>
    | OneOf of choices: ConstraintValue list
    /// <summary>The collection contains the supplied item.</summary>
    | Contains of item: ConstraintValue

/// <summary>The built-in text formats. Every case names one Axial-owned executable predicate.</summary>
/// <remarks>
/// A format never carries an author-supplied name: a name supplies no semantics a predicate can be generated from,
/// so it would either be unreachable or an annotation claiming interpreted logic. Open documentation formats are
/// <c>SchemaFormat</c>, and arbitrary predicates are <c>Constraint.custom</c>.
/// </remarks>
type Format =
    /// <summary>Axial's pragmatic email shape, <c>^[^@]+@[^@]+$</c>.</summary>
    | Email
    /// <summary>No leading or trailing whitespace.</summary>
    | Trimmed
    /// <summary>One or more ASCII digits.</summary>
    | Numeric
    /// <summary>One or more letters or digits.</summary>
    | Alphanumeric
    /// <summary>A match for the supplied .NET regular expression.</summary>
    | Pattern of pattern: string

/// <summary>What a numeric-property rule expects.</summary>
type Number =
    /// <summary>The value is an exact multiple of the supplied divisor under the value type's own arithmetic.</summary>
    | MultipleOf of divisor: ConstraintValue
    /// <summary>The value is neither infinite nor <c>NaN</c>.</summary>
    | Finite

/// <summary>A built-in operation that received an operand outside the portable value set.</summary>
/// <remarks>
/// The constraint still executes against its typed closure. Description, diagnostics, and export report the
/// operation honestly instead of approximating the operand. Message keys compose from the case and its operator,
/// for example <c>constraint.unsupportedOperand.relation.atLeast</c>.
/// </remarks>
[<RequireQualifiedAccess>]
type UnsupportedOperation =
    /// <summary>An ordering or equality comparison.</summary>
    | Relation of RelationOperator
    /// <summary>An inclusive range.</summary>
    | Within
    /// <summary>A collection containment test.</summary>
    | Contains
    /// <summary>A divisibility test.</summary>
    | MultipleOf

/// <summary>
/// One interpreted primitive: the complete semantic identity of a built-in constraint.
/// </summary>
/// <remarks>
/// An interpreted constructor builds exactly one atom and places that same value in both its description and any
/// violation it produces, so a primitive's identity and its failure cannot drift. Atoms are shape-neutral; an
/// interpreter combines an atom with the surrounding schema shape to decide what it lowers to.
/// </remarks>
type ConstraintAtom =
    /// <summary>A presence rule.</summary>
    | PresenceAtom of Presence
    /// <summary>A text length or collection count rule.</summary>
    | CardinalityAtom of Cardinality
    /// <summary>An ordering or equality rule.</summary>
    | RelationAtom of Relation
    /// <summary>A membership rule.</summary>
    | MembershipAtom of Membership
    /// <summary>A no-duplicates rule. The duplicate itself appears as the violation's actual value.</summary>
    | UniquenessAtom
    /// <summary>A built-in text format rule.</summary>
    | FormatAtom of Format
    /// <summary>A numeric-property rule.</summary>
    | NumberAtom of Number

/// The one spelling of the six comparisons, shared by atom keys and unsupported-operand keys so a later solver
/// and every renderer read the same vocabulary.
[<AutoOpen>]
module internal RelationOperatorKeys =
    let relationOperatorKey =
        function
        | Equal -> "equal"
        | NotEqual -> "notEqual"
        | GreaterThan -> "greaterThan"
        | LessThan -> "lessThan"
        | AtLeast -> "atLeast"
        | AtMost -> "atMost"

/// <summary>Projects message keys and default English phrases from expectation values.</summary>
/// <remarks>
/// <c>Violation.render</c> and <c>Violation.toMessageTree</c> both derive from this one catalogue, so a default
/// message and its localization key can never develop separate semantic switches.
/// </remarks>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ConstraintAtom =
    /// <summary>The stable message key for an atom, derived mechanically from its case.</summary>
    /// <example><code>ConstraintAtom.key (RelationAtom (Compared (AtLeast, ConstraintValue.Integer 3L)))
    /// // "constraint.relation.atLeast"</code></example>
    let key (atom: ConstraintAtom) : string =
        match atom with
        | PresenceAtom Present -> "constraint.presence.present"
        | PresenceAtom Blank -> "constraint.presence.blank"
        | CardinalityAtom(Exact _) -> "constraint.cardinality.exact"
        | CardinalityAtom(Minimum _) -> "constraint.cardinality.minimum"
        | CardinalityAtom(Maximum _) -> "constraint.cardinality.maximum"
        | CardinalityAtom(Between _) -> "constraint.cardinality.between"
        | RelationAtom(Compared(operator, _)) -> "constraint.relation." + relationOperatorKey operator
        | RelationAtom(Within _) -> "constraint.relation.within"
        | MembershipAtom(OneOf _) -> "constraint.membership.oneOf"
        | MembershipAtom(Contains _) -> "constraint.membership.contains"
        | UniquenessAtom -> "constraint.uniqueness"
        | FormatAtom Email -> "constraint.format.email"
        | FormatAtom Trimmed -> "constraint.format.trimmed"
        | FormatAtom Numeric -> "constraint.format.numeric"
        | FormatAtom Alphanumeric -> "constraint.format.alphanumeric"
        | FormatAtom(Pattern _) -> "constraint.format.pattern"
        | NumberAtom(MultipleOf _) -> "constraint.number.multipleOf"
        | NumberAtom Finite -> "constraint.number.finite"

    /// <summary>The expectation operands an atom carries, named for message interpolation.</summary>
    let arguments (atom: ConstraintAtom) : Map<string, ConstraintValue> =
        match atom with
        | PresenceAtom _ -> Map.empty
        | CardinalityAtom(Exact expected) -> Map [ "expected", ConstraintValue.Integer(int64 expected) ]
        | CardinalityAtom(Minimum minimum) -> Map [ "minimum", ConstraintValue.Integer(int64 minimum) ]
        | CardinalityAtom(Maximum maximum) -> Map [ "maximum", ConstraintValue.Integer(int64 maximum) ]
        | CardinalityAtom(Between(minimum, maximum)) ->
            Map [ "minimum", ConstraintValue.Integer(int64 minimum); "maximum", ConstraintValue.Integer(int64 maximum) ]
        | RelationAtom(Compared(_, expected)) -> Map [ "expected", expected ]
        | RelationAtom(Within(minimum, maximum)) -> Map [ "minimum", minimum; "maximum", maximum ]
        | MembershipAtom(OneOf choices) -> Map [ "choices", ConstraintValue.List choices ]
        | MembershipAtom(Contains item) -> Map [ "item", item ]
        | UniquenessAtom -> Map.empty
        | FormatAtom(Pattern pattern) -> Map [ "pattern", ConstraintValue.Text pattern ]
        | FormatAtom _ -> Map.empty
        | NumberAtom(MultipleOf divisor) -> Map [ "divisor", divisor ]
        | NumberAtom Finite -> Map.empty

    /// <summary>The default English phrase describing what an atom expected.</summary>
    let render (atom: ConstraintAtom) : string =
        let value = ConstraintValue.render

        match atom with
        | PresenceAtom Present -> "value must be present"
        | PresenceAtom Blank -> "value must be blank"
        | CardinalityAtom(Exact expected) -> $"expected a size of exactly {expected}"
        | CardinalityAtom(Minimum minimum) -> $"expected a size of at least {minimum}"
        | CardinalityAtom(Maximum maximum) -> $"expected a size of at most {maximum}"
        | CardinalityAtom(Between(minimum, maximum)) -> $"expected a size between {minimum} and {maximum}"
        | RelationAtom(Compared(Equal, expected)) -> $"expected {value expected}"
        | RelationAtom(Compared(NotEqual, expected)) -> $"expected a value other than {value expected}"
        | RelationAtom(Compared(GreaterThan, expected)) -> $"expected a value greater than {value expected}"
        | RelationAtom(Compared(LessThan, expected)) -> $"expected a value less than {value expected}"
        | RelationAtom(Compared(AtLeast, expected)) -> $"expected a value at least {value expected}"
        | RelationAtom(Compared(AtMost, expected)) -> $"expected a value at most {value expected}"
        | RelationAtom(Within(minimum, maximum)) -> $"expected a value between {value minimum} and {value maximum}"
        | MembershipAtom(OneOf choices) ->
            let choices = choices |> List.map value |> String.concat ", "
            $"expected one of: {choices}"
        | MembershipAtom(Contains item) -> $"expected the collection to contain {value item}"
        | UniquenessAtom -> "duplicate values are not allowed"
        | FormatAtom Email -> "expected an email address"
        | FormatAtom Trimmed -> "expected no leading or trailing whitespace"
        | FormatAtom Numeric -> "expected digits only"
        | FormatAtom Alphanumeric -> "expected letters and digits only"
        | FormatAtom(Pattern pattern) -> $"expected a match for {pattern}"
        | NumberAtom(MultipleOf divisor) -> $"expected a multiple of {value divisor}"
        | NumberAtom Finite -> "expected a finite number"

/// <summary>Projects message keys and default English phrases from unsupported-operand reports.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UnsupportedOperation =
    /// <summary>The stable message key for an unsupported operation.</summary>
    /// <example><code>UnsupportedOperation.key (Relation AtLeast)
    /// // "constraint.unsupportedOperand.relation.atLeast"</code></example>
    let key (operation: UnsupportedOperation) : string =
        let prefix = "constraint.unsupportedOperand."

        match operation with
        | UnsupportedOperation.Relation operator -> prefix + "relation." + relationOperatorKey operator
        | UnsupportedOperation.Within -> prefix + "within"
        | UnsupportedOperation.Contains -> prefix + "contains"
        | UnsupportedOperation.MultipleOf -> prefix + "multipleOf"

    /// <summary>The default English phrase for an unsupported operation.</summary>
    let render (operation: UnsupportedOperation) : string =
        let name =
            match operation with
            | UnsupportedOperation.Relation Equal -> "equality"
            | UnsupportedOperation.Relation NotEqual -> "inequality"
            | UnsupportedOperation.Relation GreaterThan -> "greater-than"
            | UnsupportedOperation.Relation LessThan -> "less-than"
            | UnsupportedOperation.Relation AtLeast -> "at-least"
            | UnsupportedOperation.Relation AtMost -> "at-most"
            | UnsupportedOperation.Within -> "range"
            | UnsupportedOperation.Contains -> "containment"
            | UnsupportedOperation.MultipleOf -> "multiple-of"

        $"failed a {name} rule whose operand has no portable representation"
