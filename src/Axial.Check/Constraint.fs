namespace Axial.Check

open System
open System.Globalization
open System.ComponentModel
open System.Collections

/// A closed value used when constraint metadata must cross a serialization boundary.
[<RequireQualifiedAccess>]
type ConstraintArgument =
    /// Text that can cross a metadata serialization boundary.
    | Text of string
    /// An integral value represented as a signed 64-bit integer.
    | Integer of int64
    /// A numeric value represented as a decimal.
    | Decimal of decimal
    /// A Boolean value.
    | Boolean of bool
    /// An ordered collection of portable arguments.
    | List of ConstraintArgument list

/// The inspectable meaning of an executable value constraint.
[<RequireQualifiedAccess>]
type ConstraintMetadata =
    /// Text must contain at least one non-whitespace character.
    | Present
    /// Text must contain at least the supplied number of characters.
    | MinLength of minimum: int
    /// Text must contain at most the supplied number of characters.
    | MaxLength of maximum: int
    /// Text length must lie inside the supplied inclusive bounds.
    | LengthBetween of minimum: int * maximum: int
    /// Text must use the supported email format.
    | Email
    /// Text must have no leading or trailing whitespace.
    | Trimmed
    /// Text must match the supplied regular expression.
    | Pattern of pattern: string
    /// Text must equal one of the supplied choices.
    | OneOf of choices: string list
    /// A value must equal the supplied operand.
    | EqualTo of expected: obj
    /// A value must differ from the supplied operand.
    | NotEqualTo of unexpected: obj
    /// A value must lie inside the supplied inclusive bounds.
    | Between of minimum: obj * maximum: obj
    /// A value must be greater than the supplied exclusive lower bound.
    | GreaterThan of minimum: obj
    /// A value must be less than the supplied exclusive upper bound.
    | LessThan of maximum: obj
    /// A value must be greater than or equal to the supplied lower bound.
    | AtLeast of minimum: obj
    /// A value must be less than or equal to the supplied upper bound.
    | AtMost of maximum: obj
    /// A collection must contain exactly the supplied number of items.
    | Count of expected: int
    /// A collection must contain at least the supplied number of items.
    | MinCount of minimum: int
    /// A collection must contain at most the supplied number of items.
    | MaxCount of maximum: int
    /// A collection count must lie inside the supplied inclusive bounds.
    | CountBetween of minimum: int * maximum: int
    /// A collection must contain no duplicate items.
    | Distinct
    /// A collection must contain the supplied item.
    | Contains of item: obj
    /// A numeric value must be an exact multiple of the supplied divisor.
    | MultipleOf of divisor: obj
    /// An application-defined constraint with a stable external code and inspectable operands.
    | Custom of code: string * arguments: Map<string, obj>

/// Stable external details projected from a constraint's typed metadata.
type ConstraintDetails =
    { Code: string
      Arguments: Map<string, obj> }

/// An executable value restriction coupled to inspectable metadata.
[<Sealed; AllowNullLiteral>]
type Constraint<'value> internal (check: Check<'value>, metadata: ConstraintMetadata) =
    member internal _.Check = check
    member internal _.Metadata = metadata

/// Creates, executes, and inspects value constraints.
[<RequireQualifiedAccess>]
module Constraint =
    let private ensureName parameterName (value: string) =
        if isNull value then nullArg parameterName
        if String.IsNullOrWhiteSpace value then invalidArg parameterName "Constraint names must not be blank."

    let private ensureNonNegative parameterName value =
        if value < 0 then Platform.argumentOutOfRange parameterName value "Constraint bounds must be non-negative."

    let private ensureBounds parameterName minimum maximum =
        if minimum > maximum then invalidArg parameterName "The minimum bound must not exceed the maximum bound."

    let private known metadata check = Constraint(check, metadata)

    let private metadataCode = function
        | ConstraintMetadata.Present -> "required"
        | ConstraintMetadata.MinLength _ -> "minLength"
        | ConstraintMetadata.MaxLength _ -> "maxLength"
        | ConstraintMetadata.LengthBetween _ -> "lengthBetween"
        | ConstraintMetadata.Email -> "email"
        | ConstraintMetadata.Trimmed -> "trimmed"
        | ConstraintMetadata.Pattern _ -> "pattern"
        | ConstraintMetadata.OneOf _ -> "oneOf"
        | ConstraintMetadata.EqualTo _ -> "equalTo"
        | ConstraintMetadata.NotEqualTo _ -> "notEqualTo"
        | ConstraintMetadata.Between _ -> "between"
        | ConstraintMetadata.GreaterThan _ -> "greaterThan"
        | ConstraintMetadata.LessThan _ -> "lessThan"
        | ConstraintMetadata.AtLeast _ -> "atLeast"
        | ConstraintMetadata.AtMost _ -> "atMost"
        | ConstraintMetadata.Count _ -> "count"
        | ConstraintMetadata.MinCount _ -> "minCount"
        | ConstraintMetadata.MaxCount _ -> "maxCount"
        | ConstraintMetadata.CountBetween _ -> "countBetween"
        | ConstraintMetadata.Distinct -> "distinct"
        | ConstraintMetadata.Contains _ -> "contains"
        | ConstraintMetadata.MultipleOf _ -> "multipleOf"
        | ConstraintMetadata.Custom(code, _) -> code

    let private metadataArguments = function
        | ConstraintMetadata.MinLength minimum -> Map [ "minimum", box minimum ]
        | ConstraintMetadata.MaxLength maximum -> Map [ "maximum", box maximum ]
        | ConstraintMetadata.LengthBetween(minimum, maximum) -> Map [ "minimum", box minimum; "maximum", box maximum ]
        | ConstraintMetadata.Pattern pattern -> Map [ "pattern", box pattern ]
        | ConstraintMetadata.OneOf choices -> Map [ "choices", box choices ]
        | ConstraintMetadata.EqualTo expected -> Map [ "expected", expected ]
        | ConstraintMetadata.NotEqualTo unexpected -> Map [ "unexpected", unexpected ]
        | ConstraintMetadata.Between(minimum, maximum) -> Map [ "minimum", minimum; "maximum", maximum ]
        | ConstraintMetadata.GreaterThan minimum -> Map [ "minimum", minimum ]
        | ConstraintMetadata.LessThan maximum -> Map [ "maximum", maximum ]
        | ConstraintMetadata.AtLeast minimum -> Map [ "minimum", minimum ]
        | ConstraintMetadata.AtMost maximum -> Map [ "maximum", maximum ]
        | ConstraintMetadata.Count expected -> Map [ "expected", box expected ]
        | ConstraintMetadata.MinCount minimum -> Map [ "minimum", box minimum ]
        | ConstraintMetadata.MaxCount maximum -> Map [ "maximum", box maximum ]
        | ConstraintMetadata.CountBetween(minimum, maximum) -> Map [ "minimum", box minimum; "maximum", box maximum ]
        | ConstraintMetadata.Contains item -> Map [ "item", item ]
        | ConstraintMetadata.MultipleOf divisor -> Map [ "divisor", divisor ]
        | ConstraintMetadata.Custom(_, arguments) -> arguments
        | _ -> Map.empty

    let private builtInCodes =
        [ ConstraintMetadata.Present; ConstraintMetadata.MinLength 0; ConstraintMetadata.MaxLength 0
          ConstraintMetadata.LengthBetween(0, 0); ConstraintMetadata.Email; ConstraintMetadata.Trimmed
          ConstraintMetadata.Pattern "x"; ConstraintMetadata.OneOf []; ConstraintMetadata.EqualTo(box 0)
          ConstraintMetadata.NotEqualTo(box 0); ConstraintMetadata.Between(box 0, box 0)
          ConstraintMetadata.GreaterThan(box 0); ConstraintMetadata.LessThan(box 0)
          ConstraintMetadata.AtLeast(box 0); ConstraintMetadata.AtMost(box 0); ConstraintMetadata.Count 0
          ConstraintMetadata.MinCount 0; ConstraintMetadata.MaxCount 0; ConstraintMetadata.CountBetween(0, 0)
          ConstraintMetadata.Distinct; ConstraintMetadata.Contains(box 0); ConstraintMetadata.MultipleOf(box 1) ]
        |> List.map metadataCode
        |> Set.ofList

    /// Defines a complete application constraint from metadata and executable checking behavior.
    let define code (arguments: (string * obj) seq) (check: Check<'value>) =
        ensureName (nameof code) code
        if builtInCodes.Contains code then invalidArg (nameof code) $"'{code}' is reserved for built-in constraints."
        if isNull (box arguments) then nullArg (nameof arguments)
        if isNull (box check) then nullArg (nameof check)
        let pairs = arguments |> Seq.toList
        pairs |> List.iter (fun (name, _) -> ensureName (nameof arguments) name)
        let names = pairs |> List.map fst
        if (names |> Set.ofList |> Set.count) <> names.Length then invalidArg (nameof arguments) "Constraint argument names must be unique."
        known (ConstraintMetadata.Custom(code, Map pairs)) check

    let check (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Check

    let metadata (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Metadata

    let code constraint' = constraint' |> metadata |> metadataCode

    let arguments constraint' = constraint' |> metadata |> metadataArguments

    let details constraint' =
        { Code = code constraint'
          Arguments = arguments constraint' }

    let rec private tryPortableArgument (value: obj) =
        match value with
        | null -> None
        | :? string as value -> Some(ConstraintArgument.Text value)
        | :? bool as value -> Some(ConstraintArgument.Boolean value)
        | :? int8 as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? uint8 as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? int16 as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? uint16 as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? int as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? uint32 as value -> Some(ConstraintArgument.Integer(int64 value))
        | :? int64 as value -> Some(ConstraintArgument.Integer value)
        | :? decimal as value -> Some(ConstraintArgument.Decimal value)
        | :? float32 as value -> Some(ConstraintArgument.Decimal(decimal value))
        | :? float as value -> Some(ConstraintArgument.Decimal(decimal value))
        | :? Guid as value -> Some(ConstraintArgument.Text(value.ToString("D")))
        | :? DateTime as value -> Some(ConstraintArgument.Text(value.ToString("O", CultureInfo.InvariantCulture)))
        | :? DateTimeOffset as value -> Some(ConstraintArgument.Text(value.ToString("O", CultureInfo.InvariantCulture)))
        | :? (ConstraintArgument list) as values -> Some(ConstraintArgument.List values)
        | :? IEnumerable as values ->
            values
            |> Seq.cast<obj>
            |> Seq.fold (fun state value ->
                state
                |> Option.bind (fun portableValues ->
                    tryPortableArgument value
                    |> Option.map (fun portable -> portable :: portableValues))) (Some [])
            |> Option.map (List.rev >> ConstraintArgument.List)
        | _ -> None

    /// Projects arguments to their portable representation when every value is supported.
    let tryPortableArguments constraint' =
        arguments constraint'
        |> Map.fold (fun state name value ->
            state |> Option.bind (fun collected -> tryPortableArgument value |> Option.map (fun portable -> Map.add name portable collected))) (Some Map.empty)

    let checkAll constraints = constraints |> List.map check |> Check.all

    /// Adapts a constraint to another input type while preserving its executable meaning and metadata.
    let contramap (project: 'input -> 'value) (constraint': Constraint<'value>) : Constraint<'input> =
        if isNull (box project) then nullArg (nameof project)
        known (metadata constraint') (project >> check constraint')

    let required : Constraint<string> = known ConstraintMetadata.Present Check.String.present
    let minLength minimum = ensureNonNegative (nameof minimum) minimum; known (ConstraintMetadata.MinLength minimum) (Check.String.minLength minimum)
    let maxLength maximum = ensureNonNegative (nameof maximum) maximum; known (ConstraintMetadata.MaxLength maximum) (Check.String.maxLength maximum)
    let lengthBetween minimum maximum = ensureNonNegative (nameof minimum) minimum; ensureNonNegative (nameof maximum) maximum; ensureBounds (nameof minimum) minimum maximum; known (ConstraintMetadata.LengthBetween(minimum, maximum)) (Check.String.lengthBetween minimum maximum)
    let email = known ConstraintMetadata.Email Check.String.email
    let trimmed = known ConstraintMetadata.Trimmed (fun (value: string) -> if not (isNull value) && value.Trim() = value then Ok () else Error [ InvalidFormat "trimmed" ])
    let pattern pattern = ensureName (nameof pattern) pattern; known (ConstraintMetadata.Pattern pattern) (Check.String.matches pattern)
    let oneOf choices = let choices = Seq.toList choices in known (ConstraintMetadata.OneOf choices) (Check.String.oneOf choices)
    let equalTo expected = known (ConstraintMetadata.EqualTo(box expected)) (Check.equalTo expected)
    let notEqualTo unexpected = known (ConstraintMetadata.NotEqualTo(box unexpected)) (Check.notEqualTo unexpected)
    let between minimum maximum = ensureBounds (nameof minimum) minimum maximum; known (ConstraintMetadata.Between(box minimum, box maximum)) (Check.between minimum maximum)
    let greaterThan minimum = known (ConstraintMetadata.GreaterThan(box minimum)) (Check.greaterThan minimum)
    let lessThan maximum = known (ConstraintMetadata.LessThan(box maximum)) (Check.lessThan maximum)
    let atLeast minimum = known (ConstraintMetadata.AtLeast(box minimum)) (Check.atLeast minimum)
    let atMost maximum = known (ConstraintMetadata.AtMost(box maximum)) (Check.atMost maximum)
    let count expected = ensureNonNegative (nameof expected) expected; known (ConstraintMetadata.Count expected) (Check.Seq.count expected)
    let minCount minimum = ensureNonNegative (nameof minimum) minimum; known (ConstraintMetadata.MinCount minimum) (Check.Seq.minCount minimum)
    let maxCount maximum = ensureNonNegative (nameof maximum) maximum; known (ConstraintMetadata.MaxCount maximum) (Check.Seq.maxCount maximum)
    let countBetween minimum maximum = ensureNonNegative (nameof minimum) minimum; ensureNonNegative (nameof maximum) maximum; ensureBounds (nameof minimum) minimum maximum; known (ConstraintMetadata.CountBetween(minimum, maximum)) (Check.Seq.countBetween minimum maximum)
    let distinct<'value when 'value: equality> : Constraint<seq<'value>> = known ConstraintMetadata.Distinct Check.Seq.noDuplicates
    let contains item : Constraint<seq<'value>> = known (ConstraintMetadata.Contains(box item)) (Check.Seq.contains item)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type MultipleOfDispatcher =
        static member Create(divisor: int) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0 then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])
        static member Create(divisor: int64) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0L then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])
        static member Create(divisor: decimal) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0M then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])
        static member Create(divisor: float) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0.0 then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])
        static member Create(divisor: float32) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0.0f then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])
        static member Create(divisor: bigint) =
            known (ConstraintMetadata.MultipleOf(box divisor)) (fun value -> if value % divisor = 0I then Ok () else Error [ OutOfRange(NotMultipleOf(string divisor), Some(string value)) ])

    let inline multipleOf (divisor: ^value) : Constraint<^value> =
        ((^value or MultipleOfDispatcher) : (static member Create : ^value -> Constraint<^value>) divisor)

    /// Adapts a sequence constraint to lists without changing its metadata.
    let forList (constraint': Constraint<seq<'value>>) : Constraint<'value list> =
        known (metadata constraint') (fun values -> check constraint' values)
