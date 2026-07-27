namespace Axial.Check

open System
open System.Globalization

/// A closed, structurally comparable value used in portable constraint metadata.
[<RequireQualifiedAccess>]
type ConstraintArgument =
    | Text of string
    | Integer of int64
    | Decimal of decimal
    | Boolean of bool
    | List of ConstraintArgument list

/// Stable metadata describing a portable value restriction.
type ConstraintDetails =
    { Code: string
      Arguments: Map<string, ConstraintArgument> }

/// An executable value restriction coupled to portable metadata.
[<Sealed; AllowNullLiteral>]
type Constraint<'value> internal (check: Check<'value>, details: ConstraintDetails) =
    member internal _.Check = check
    member internal _.Details = details

/// Creates and inspects portable value constraints.
[<RequireQualifiedAccess>]
module Constraint =
    let private builtInCodes =
        set [ "required"; "optional"; "minLength"; "maxLength"; "lengthBetween"; "email"; "trimmed"; "pattern"; "oneOf"
              "equalTo"; "notEqualTo"; "between"; "greaterThan"; "lessThan"; "atLeast"; "atMost"; "count"; "minCount"
              "maxCount"; "countBetween"; "distinct"; "contains"; "multipleOf" ]

    let private ensureName parameterName (value: string) =
        if isNull value then nullArg parameterName
        if String.IsNullOrWhiteSpace value then invalidArg parameterName "Constraint names must not be blank."

    let private ensureNonNegative parameterName value =
        if value < 0 then raise (ArgumentOutOfRangeException(parameterName, value, "Constraint bounds must be non-negative."))

    let private ensureBounds parameterName minimum maximum =
        if minimum > maximum then invalidArg parameterName "The minimum bound must not exceed the maximum bound."

    let private argument (value: 'value) =
        match box value with
        | null -> invalidArg (nameof value) "Portable constraint arguments cannot be null."
        | :? string as value -> ConstraintArgument.Text value
        | :? bool as value -> ConstraintArgument.Boolean value
        | :? int8 as value -> ConstraintArgument.Integer(int64 value)
        | :? uint8 as value -> ConstraintArgument.Integer(int64 value)
        | :? int16 as value -> ConstraintArgument.Integer(int64 value)
        | :? uint16 as value -> ConstraintArgument.Integer(int64 value)
        | :? int as value -> ConstraintArgument.Integer(int64 value)
        | :? uint32 as value -> ConstraintArgument.Integer(int64 value)
        | :? int64 as value -> ConstraintArgument.Integer value
        | :? decimal as value -> ConstraintArgument.Decimal value
        | :? float32 as value -> ConstraintArgument.Decimal(decimal value)
        | :? float as value -> ConstraintArgument.Decimal(decimal value)
        | :? Guid as value -> ConstraintArgument.Text(value.ToString("D"))
        | :? DateTime as value -> ConstraintArgument.Text(value.ToString("O", CultureInfo.InvariantCulture))
        | :? DateTimeOffset as value -> ConstraintArgument.Text(value.ToString("O", CultureInfo.InvariantCulture))
        | value -> invalidArg (nameof value) $"The value type '{value.GetType().FullName}' has no portable constraint representation."

    let private known code arguments check = Constraint(check, { Code = code; Arguments = Map arguments })

    let define code (arguments: (string * ConstraintArgument) seq) (check: Check<'value>) =
        ensureName (nameof code) code
        if builtInCodes.Contains code then invalidArg (nameof code) $"'{code}' is reserved for built-in constraints."
        if isNull (box arguments) then nullArg (nameof arguments)
        if isNull (box check) then nullArg (nameof check)
        let pairs = arguments |> Seq.toList
        pairs |> List.iter (fun (name, _) -> ensureName (nameof arguments) name)
        let names = pairs |> List.map fst
        if (names |> Set.ofList |> Set.count) <> names.Length then invalidArg (nameof arguments) "Constraint argument names must be unique."
        Constraint(check, { Code = code; Arguments = Map pairs })

    let check (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Check

    let details (constraint': Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Details

    let checkAll constraints = constraints |> List.map check |> Check.all

    let required : Constraint<string> = known "required" [] Check.String.present
    let optional<'value> : Constraint<'value option> = known "optional" [] (fun _ -> Ok ())
    let minLength minimum = ensureNonNegative (nameof minimum) minimum; known "minLength" [ "minimum", ConstraintArgument.Integer(int64 minimum) ] (Check.String.minLength minimum)
    let maxLength maximum = ensureNonNegative (nameof maximum) maximum; known "maxLength" [ "maximum", ConstraintArgument.Integer(int64 maximum) ] (Check.String.maxLength maximum)
    let lengthBetween minimum maximum = ensureNonNegative (nameof minimum) minimum; ensureNonNegative (nameof maximum) maximum; ensureBounds (nameof minimum) minimum maximum; known "lengthBetween" [ "minimum", ConstraintArgument.Integer(int64 minimum); "maximum", ConstraintArgument.Integer(int64 maximum) ] (Check.String.lengthBetween minimum maximum)
    let email = known "email" [] Check.String.email
    let trimmed = known "trimmed" [] (fun (value: string) -> if not (isNull value) && value.Trim() = value then Ok () else Error [ InvalidFormat "trimmed" ])
    let pattern pattern = ensureName (nameof pattern) pattern; known "pattern" [ "pattern", ConstraintArgument.Text pattern ] (Check.String.matches pattern)
    let oneOf choices = let choices = Seq.toList choices in known "oneOf" [ "choices", ConstraintArgument.List(List.map ConstraintArgument.Text choices) ] (Check.String.oneOf choices)
    let equalTo expected = known "equalTo" [ "expected", argument expected ] (Check.equalTo expected)
    let notEqualTo unexpected = known "notEqualTo" [ "unexpected", argument unexpected ] (Check.notEqualTo unexpected)
    let between minimum maximum = ensureBounds (nameof minimum) minimum maximum; known "between" [ "minimum", argument minimum; "maximum", argument maximum ] (Check.between minimum maximum)
    let greaterThan minimum = known "greaterThan" [ "minimum", argument minimum ] (Check.greaterThan minimum)
    let lessThan maximum = known "lessThan" [ "maximum", argument maximum ] (Check.lessThan maximum)
    let atLeast minimum = known "atLeast" [ "minimum", argument minimum ] (Check.atLeast minimum)
    let atMost maximum = known "atMost" [ "maximum", argument maximum ] (Check.atMost maximum)
    let count expected = ensureNonNegative (nameof expected) expected; known "count" [ "expected", ConstraintArgument.Integer(int64 expected) ] (Check.Seq.count expected)
    let minCount minimum = ensureNonNegative (nameof minimum) minimum; known "minCount" [ "minimum", ConstraintArgument.Integer(int64 minimum) ] (Check.Seq.minCount minimum)
    let maxCount maximum = ensureNonNegative (nameof maximum) maximum; known "maxCount" [ "maximum", ConstraintArgument.Integer(int64 maximum) ] (Check.Seq.maxCount maximum)
    let countBetween minimum maximum = ensureNonNegative (nameof minimum) minimum; ensureNonNegative (nameof maximum) maximum; ensureBounds (nameof minimum) minimum maximum; known "countBetween" [ "minimum", ConstraintArgument.Integer(int64 minimum); "maximum", ConstraintArgument.Integer(int64 maximum) ] (Check.Seq.countBetween minimum maximum)
    let distinct<'value when 'value: equality> : Constraint<seq<'value>> = known "distinct" [] Check.Seq.noDuplicates
    let contains item : Constraint<seq<'value>> = known "contains" [ "item", argument item ] (Check.Seq.contains item)
