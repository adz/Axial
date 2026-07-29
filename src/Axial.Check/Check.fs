namespace Axial.Check

open System
open System.Text.RegularExpressions

/// <summary>Describes the length requirement that a value check expected a text or collection value to satisfy.</summary>
type CheckLengthExpectation =
    /// <summary>The value was expected to have at least the supplied length.</summary>
    | MinimumLength of minimum: int
    /// <summary>The value was expected to have at most the supplied length.</summary>
    | MaximumLength of maximum: int
    /// <summary>The value was expected to have exactly the supplied length.</summary>
    | ExactLength of expected: int
    /// <summary>The value was expected to have a length inside the inclusive bounds.</summary>
    | LengthBetween of minimum: int * maximum: int

/// <summary>Describes the ordering requirement that a value check expected a comparable value to satisfy against a
/// caller-supplied bound.</summary>
type CheckRangeExpectation =
    /// <summary>The value was expected to be greater than the supplied exclusive lower bound.</summary>
    | GreaterThan of minimumExclusive: string
    /// <summary>The value was expected to be less than the supplied exclusive upper bound.</summary>
    | LessThan of maximumExclusive: string
    /// <summary>The value was expected to be greater than or equal to the supplied lower bound.</summary>
    | AtLeast of minimumInclusive: string
    /// <summary>The value was expected to be less than or equal to the supplied upper bound.</summary>
    | AtMost of maximumInclusive: string
    /// <summary>The value was expected to be between the supplied inclusive bounds.</summary>
    | Between of minimumInclusive: string * maximumInclusive: string
    /// <summary>The value was expected to be an integer multiple of the supplied divisor.</summary>
    | NotMultipleOf of divisor: string

/// <summary>Describes why an executable value check failed, without attaching source paths or structured data.</summary>
type CheckFailure =
    /// <summary>The value was not inhabited for its shape.</summary>
    | Blank
    /// <summary>The value did not match the expected format.</summary>
    | InvalidFormat of expected: string
    /// <summary>The value length did not match the expected length constraint.</summary>
    | InvalidLength of expectation: CheckLengthExpectation * actualLength: int option
    /// <summary>The value did not match the expected ordered range constraint.</summary>
    | OutOfRange of expectation: CheckRangeExpectation * actual: string option
    /// <summary>The value was not one of the expected choices.</summary>
    | NotOneOf of choices: string
    /// <summary>A duplicate value was found.</summary>
    | Duplicate
    /// <summary>A custom value check identified by an application-defined code failed.</summary>
    | Custom of code: string

/// <summary>
/// A localizable set of message templates for rendering <see cref="T:Axial.Check.CheckFailure" /> values.
/// </summary>
/// <remarks>
/// Each function receives only the already-formatted operand(s) (a length, a bound, an actual value) and
/// returns the complete phrase for that piece of the message. A translation supplies grammar and word order; it
/// never needs to reimplement the traversal over every <c>CheckFailure</c>/expectation case, since
/// <see cref="M:Axial.Check.CheckFailure.describeWith" /> owns that traversal and calls into these functions.
/// </remarks>
type CheckFailureResources =
    { /// <summary>Renders a length expectation, e.g. "at least 3 characters".</summary>
      Length: CheckLengthExpectation -> string
      /// <summary>Renders a range expectation, e.g. "greater than zero".</summary>
      Range: CheckRangeExpectation -> string
      /// <summary>Renders a blank-value failure.</summary>
      Blank: string
      /// <summary>Renders an invalid-format failure given the expected format name.</summary>
      InvalidFormat: string -> string
      /// <summary>Renders a length failure given the rendered expectation and the actual length, if known.</summary>
      LengthFailure: string -> int option -> string
      /// <summary>Renders a range failure given the rendered expectation and the actual value, if known.</summary>
      RangeFailure: string -> string option -> string
      /// <summary>Renders a not-one-of failure given the expected choices.</summary>
      NotOneOf: string -> string
      /// <summary>Renders a duplicate-value failure.</summary>
      Duplicate: string
      /// <summary>Renders a custom-code failure given the application-defined code.</summary>
      Custom: string -> string }

/// <summary>Renders <see cref="T:Axial.Check.CheckFailure" /> values as human-readable sentence fragments.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CheckFailure =
    /// <summary>The default English <see cref="T:Axial.Check.CheckFailureResources" />, used by
    /// <see cref="M:Axial.Check.CheckFailure.describe" /> and
    /// <see cref="M:Axial.Check.CheckFailure.describeAll" />.</summary>
    let english : CheckFailureResources =
        { Length =
            function
            | MinimumLength minimum -> $"at least {minimum} character(s)"
            | MaximumLength maximum -> $"at most {maximum} character(s)"
            | ExactLength expected -> $"exactly {expected} character(s)"
            | LengthBetween(minimum, maximum) -> $"between {minimum} and {maximum} characters"
          Range =
            function
            | GreaterThan minimum -> $"greater than {minimum}"
            | LessThan maximum -> $"less than {maximum}"
            | AtLeast minimum -> $"at least {minimum}"
            | AtMost maximum -> $"at most {maximum}"
            | Between(minimum, maximum) -> $"between {minimum} and {maximum}"
            | NotMultipleOf divisor -> $"a multiple of {divisor}"
          Blank = "value must be present"
          InvalidFormat = fun expected -> $"value must match the expected {expected} format"
          LengthFailure =
            fun expectation actual ->
                match actual with
                | Some length -> $"expected length {expectation}, but was {length}"
                | None -> $"expected length {expectation}"
          RangeFailure =
            fun expectation actual ->
                match actual with
                | Some value -> $"expected a value {expectation}, but was {value}"
                | None -> $"expected a value {expectation}"
          NotOneOf = fun choices -> $"expected one of: {choices}"
          Duplicate = "duplicate values are not allowed"
          Custom = fun code -> $"failed custom check '{code}'" }

    /// <summary>Renders a single check failure using the supplied <see cref="T:Axial.Check.CheckFailureResources" />,
    /// with no trailing punctuation, e.g. <c>"expected a value greater than zero, but was 0"</c>.</summary>
    let describeWith (resources: CheckFailureResources) (failure: CheckFailure) : string =
        match failure with
        | Blank -> resources.Blank
        | InvalidFormat expected -> resources.InvalidFormat expected
        | InvalidLength(expectation, actual) -> resources.LengthFailure (resources.Length expectation) actual
        | OutOfRange(expectation, actual) -> resources.RangeFailure (resources.Range expectation) actual
        | NotOneOf choices -> resources.NotOneOf choices
        | Duplicate -> resources.Duplicate
        | Custom code -> resources.Custom code

    /// <summary>Joins multiple check failures, rendered with the supplied resources, into one semicolon-separated message.</summary>
    let describeAllWith (resources: CheckFailureResources) (failures: CheckFailure list) : string =
        failures |> List.map (describeWith resources) |> String.concat "; "

    /// <summary>Renders a single check failure using <see cref="P:Axial.Check.CheckFailure.english" />.</summary>
    let describe (failure: CheckFailure) : string =
        describeWith english failure

    /// <summary>Joins multiple check failures into one semicolon-separated message using
    /// <see cref="P:Axial.Check.CheckFailure.english" />.</summary>
    let describeAll (failures: CheckFailure list) : string =
        describeAllWith english failures

/// <summary>
/// An executable value constraint over an already parsed value.
/// </summary>
/// <remarks>
/// A check succeeds with the original value (<c>Ok ()</c>, unchanged) or returns one or more structured
/// <see cref="T:Axial.Check.CheckFailure" /> values. A passing check never transforms its input — it never returns or replaces the checked value. Use <c>Axial.Result.Result.guard</c> when the original value must continue through a Result pipeline. Checks do not carry input paths, structured data, schema metadata, or refined-value
/// construction; keep those concerns in validation, parsing, schema, or refinement layers.
/// </remarks>
type Check<'value> = 'value -> Result<unit, CheckFailure list>

/// <summary>
/// Typed value-check programs for local structural facts.
/// </summary>
/// <remarks>
/// Top-level <c>Check.*</c> helpers return structured results, not booleans. Direct modules such as
/// <c>Check.String</c>, <c>Check.Number</c>, <c>Check.Seq</c>, <c>Check.Option</c>, <c>Check.ValueOption</c>,
/// <c>Check.Nullable</c>, and <c>Check.Result</c> contain the type-specific implementations. Top-level helpers such
/// as <c>lengthBetween</c>, <c>between</c>, and <c>countBetween</c> forward to those single implementations, while
/// <c>present</c>, <c>empty</c>, and <c>notEmpty</c> are the type-directed facade that dispatches across all of them.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    /// <summary>Executable value checks for already parsed strings.</summary>
    module String =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualLength (value: string) =
            if isNull value then None else Some value.Length

        /// <summary>Requires an already parsed string value to be non-null and contain at least one non-whitespace character.</summary>
        let present : Check<string> =
            fun value ->
                if isNull value then fail Blank
                elif value.IsBlank then fail Blank
                else Ok ()

        /// <summary>Requires an already parsed string value to be exactly empty. Null fails as a missing value.</summary>
        let empty : Check<string> =
            fun value ->
                if isNull value then fail Blank
                elif value.IsEmpty then Ok ()
                else fail (InvalidLength(ExactLength 0, actualLength value))

        /// <summary>Requires an already parsed string value to contain at least one character. Whitespace counts as present text.</summary>
        let notEmpty : Check<string> =
            fun value ->
                if isNull value then fail Blank
                elif value.IsNotEmpty then Ok ()
                else fail (InvalidLength(MinimumLength 1, Some 0))

        /// <summary>Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.</summary>
        let minLength (minimum: int) : Check<string> =
            fun value ->
                if value.HasMinLength minimum then Ok ()
                else fail (InvalidLength(MinimumLength minimum, actualLength value))

        /// <summary>Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.</summary>
        let maxLength (maximum: int) : Check<string> =
            fun value ->
                if value.HasMaxLength maximum then Ok ()
                else fail (InvalidLength(MaximumLength maximum, actualLength value))

        /// <summary>Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.</summary>
        let lengthBetween (minimum: int) (maximum: int) : Check<string> =
            fun value ->
                if value.HasLengthBetween(minimum, maximum) then Ok ()
                else fail (InvalidLength(LengthBetween(minimum, maximum), actualLength value))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let length (expected: int) : Check<string> =
            fun value ->
                if value.HasLength expected then Ok ()
                else fail (InvalidLength(ExactLength expected, actualLength value))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let exactLength (expected: int) : Check<string> =
            length expected

        /// <summary>Requires an already parsed string value to match Axial's pragmatic email format.</summary>
        let email : Check<string> =
            fun value ->
                if value.IsEmail then Ok ()
                else fail (InvalidFormat "email")

        /// <summary>Requires an already parsed string value to match the supplied regular expression pattern.</summary>
        let matches (pattern: string) : Check<string> =
            fun value ->
                if value.MatchesPattern pattern then Ok ()
                else fail (InvalidFormat pattern)

        /// <summary>Requires an already parsed string value to contain one or more numeric characters.</summary>
        let numeric : Check<string> =
            fun value ->
                if value.IsNumeric then Ok ()
                else fail (InvalidFormat "numeric")

        /// <summary>Requires an already parsed string value to contain one or more letter or digit characters.</summary>
        let alphaNumeric : Check<string> =
            fun value ->
                if value.IsAlphaNumeric then Ok ()
                else fail (InvalidFormat "alphaNumeric")

        /// <summary>Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.</summary>
        let oneOf (choices: string seq) : Check<string> =
            let choices = choices |> Seq.toList
            let expected = System.String.Join("|", choices)

            fun value ->
                if not (isNull value) && List.contains value choices then Ok ()
                else fail (NotOneOf expected)

    /// <summary>Executable value checks for already parsed ordered values.</summary>
    module Number =
        /// <summary>Requires a value to lie inside the supplied inclusive bounds.</summary>
        let inline between minimum maximum : Check<'value> =
            fun value ->
                if Predicate.Number.between minimum maximum value then Ok ()
                else Error [ OutOfRange(Between(string minimum, string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than the supplied exclusive lower bound.</summary>
        let inline greaterThan minimum : Check<'value> =
            fun value ->
                if Predicate.Number.greaterThan minimum value then Ok ()
                else Error [ OutOfRange(GreaterThan(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than the supplied exclusive upper bound.</summary>
        let inline lessThan maximum : Check<'value> =
            fun value ->
                if Predicate.Number.lessThan maximum value then Ok ()
                else Error [ OutOfRange(LessThan(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than or equal to the supplied lower bound.</summary>
        let inline atLeast minimum : Check<'value> =
            fun value ->
                if Predicate.Number.atLeast minimum value then Ok ()
                else Error [ OutOfRange(AtLeast(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than or equal to the supplied upper bound.</summary>
        let inline atMost maximum : Check<'value> =
            fun value ->
                if Predicate.Number.atMost maximum value then Ok ()
                else Error [ OutOfRange(AtMost(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than zero.</summary>
        let inline positive (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.positive value then Ok ()
            else Error [ OutOfRange(GreaterThan "0", Some(string value)) ]

        /// <summary>Requires a value to be greater than or equal to zero.</summary>
        let inline nonNegative (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.nonNegative value then Ok ()
            else Error [ OutOfRange(AtLeast "0", Some(string value)) ]

        /// <summary>Requires a value to be less than zero.</summary>
        let inline negative (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.negative value then Ok ()
            else Error [ OutOfRange(LessThan "0", Some(string value)) ]

        /// <summary>Requires a value to be less than or equal to zero.</summary>
        let inline nonPositive (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.nonPositive value then Ok ()
            else Error [ OutOfRange(AtMost "0", Some(string value)) ]

    /// <summary>Executable value checks for already parsed sequence-shaped values.</summary>
    /// <remarks>
    /// Use <c>Check.Seq</c> for sequence-shaped checks. The earlier <c>Check.Collection</c> surface is intentionally not
    /// retained in this pre-1.0 API.
    /// </remarks>
    module Seq =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualCount (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then None
            else Some(Microsoft.FSharp.Collections.Seq.length values)

        /// <summary>Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.</summary>
        let notEmpty : Check<#seq<'value>> =
            fun values ->
                if values.HasItems then Ok ()
                else fail (InvalidLength(MinimumLength 1, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.</summary>
        let empty : Check<#seq<'value>> =
            fun values ->
                if values.HasNoItems then Ok ()
                else fail (InvalidLength(ExactLength 0, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.</summary>
        let count (expected: int) : Check<#seq<'value>> =
            fun values ->
                if values.HasCount expected then Ok ()
                else fail (InvalidLength(ExactLength expected, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.</summary>
        let minCount (minimum: int) : Check<#seq<'value>> =
            fun values ->
                if values.HasMinCount minimum then Ok ()
                else fail (InvalidLength(MinimumLength minimum, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.</summary>
        let maxCount (maximum: int) : Check<#seq<'value>> =
            fun values ->
                if values.HasMaxCount maximum then Ok ()
                else fail (InvalidLength(MaximumLength maximum, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.</summary>
        let countBetween (minimum: int) (maximum: int) : Check<#seq<'value>> =
            fun values ->
                if values.HasCountBetween(minimum, maximum) then Ok ()
                else fail (InvalidLength(LengthBetween(minimum, maximum), actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain no duplicate values.</summary>
        let noDuplicates : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then fail Blank
                elif values.IsDistinct then Ok ()
                else fail Duplicate

        /// <summary>Requires an already parsed sequence-shaped value to contain the supplied value.</summary>
        let contains (expected: 'value) : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then fail Blank
                elif values.HasItem expected then Ok ()
                else fail (NotOneOf(string expected))

        /// <summary>Requires an already parsed sequence-shaped value to contain exactly one item.</summary>
        let single (values: #seq<'value>) : Result<unit, CheckFailure list> =
            count 1 values

        /// <summary>Requires an already parsed sequence-shaped value to contain zero or one item.</summary>
        let atMostOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            maxCount 1 values

        /// <summary>Requires an already parsed sequence-shaped value to contain at least one item.</summary>
        let atLeastOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            notEmpty values

        /// <summary>Requires an already parsed sequence-shaped value to contain more than one item.</summary>
        let moreThanOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            minCount 2 values

    /// <summary>Executable value checks for already parsed optional values.</summary>
    module Option =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires an option to contain a value.</summary>
        let some : Check<'value option> =
            fun value ->
                match value with
                | Some _ -> Ok ()
                | None -> fail Blank

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let present : Check<'value option> =
            some

        /// <summary>Requires an option to contain no value.</summary>
        let none : Check<'value option> =
            fun value ->
                match value with
                | None -> Ok ()
                | Some _ -> fail (NotOneOf "None")

        /// <summary>Alias for <c>none</c>; requires an option to contain no value.</summary>
        let empty : Check<'value option> =
            none

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let notEmpty : Check<'value option> =
            some

    /// <summary>Executable value checks for already parsed value option values.</summary>
    module ValueOption =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a value option to contain a value.</summary>
        let some : Check<'value voption> =
            fun value ->
                match value with
                | ValueSome _ -> Ok ()
                | ValueNone -> fail Blank

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let present : Check<'value voption> =
            some

        /// <summary>Requires a value option to contain no value.</summary>
        let none : Check<'value voption> =
            fun value ->
                match value with
                | ValueNone -> Ok ()
                | ValueSome _ -> fail (NotOneOf "ValueNone")

        /// <summary>Alias for <c>none</c>; requires a value option to contain no value.</summary>
        let empty : Check<'value voption> =
            none

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let notEmpty : Check<'value voption> =
            some

    /// <summary>Executable value checks for already parsed nullable values.</summary>
    module Nullable =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a nullable value to contain a value.</summary>
        let hasValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then Ok () else fail Blank

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let present : Check<System.Nullable<'value>> =
            hasValue

        /// <summary>Requires a nullable value to contain no value.</summary>
        let hasNoValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then fail (NotOneOf "null") else Ok ()

        /// <summary>Alias for <c>hasNoValue</c>; requires a nullable value to contain no value.</summary>
        let empty : Check<System.Nullable<'value>> =
            hasNoValue

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let notEmpty : Check<System.Nullable<'value>> =
            hasValue

    /// <summary>Executable value checks for result values.</summary>
    module Result =
        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a result to contain a successful value.</summary>
        let ok : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Ok _ -> Ok ()
                | Error _ -> fail (NotOneOf "Ok")

        /// <summary>Requires a result to contain an error value.</summary>
        let error : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Error _ -> Ok ()
                | Ok _ -> fail (NotOneOf "Error")

    let private fail failure : Result<unit, CheckFailure list> =
        Error [ failure ]

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Length =
        static member Create(_: string, expected: int) : Check<string> = String.length expected
        static member Create(_: 'value list, expected: int) : Check<'value list> = fun values -> Seq.count expected values
        static member Create(_: 'value array, expected: int) : Check<'value array> = fun values -> Seq.count expected values
        static member Create(_: Map<string, 'value>, expected: int) : Check<Map<string, 'value>> =
            fun values ->
                let actual = if Object.ReferenceEquals(values, null) then None else Some values.Count
                if actual = Some expected then Ok () else fail (InvalidLength(ExactLength expected, actual))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type MinLength =
        static member Create(_: string, minimum: int) : Check<string> = String.minLength minimum
        static member Create(_: 'value list, minimum: int) : Check<'value list> = fun values -> Seq.minCount minimum values
        static member Create(_: 'value array, minimum: int) : Check<'value array> = fun values -> Seq.minCount minimum values
        static member Create(_: Map<string, 'value>, minimum: int) : Check<Map<string, 'value>> =
            fun values ->
                let actual = if Object.ReferenceEquals(values, null) then None else Some values.Count
                if actual |> Option.exists (fun count -> count >= minimum) then Ok ()
                else fail (InvalidLength(MinimumLength minimum, actual))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type MaxLength =
        static member Create(_: string, maximum: int) : Check<string> = String.maxLength maximum
        static member Create(_: 'value list, maximum: int) : Check<'value list> = fun values -> Seq.maxCount maximum values
        static member Create(_: 'value array, maximum: int) : Check<'value array> = fun values -> Seq.maxCount maximum values
        static member Create(_: Map<string, 'value>, maximum: int) : Check<Map<string, 'value>> =
            fun values ->
                let actual = if Object.ReferenceEquals(values, null) then None else Some values.Count
                if actual |> Option.exists (fun count -> count <= maximum) then Ok ()
                else fail (InvalidLength(MaximumLength maximum, actual))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type LengthBetween =
        static member Create(_: string, minimum: int, maximum: int) : Check<string> = String.lengthBetween minimum maximum
        static member Create(_: 'value list, minimum: int, maximum: int) : Check<'value list> = fun values -> Seq.countBetween minimum maximum values
        static member Create(_: 'value array, minimum: int, maximum: int) : Check<'value array> = fun values -> Seq.countBetween minimum maximum values
        static member Create(_: Map<string, 'value>, minimum: int, maximum: int) : Check<Map<string, 'value>> =
            fun values ->
                let actual = if Object.ReferenceEquals(values, null) then None else Some values.Count
                if actual |> Option.exists (fun count -> count >= minimum && count <= maximum) then Ok ()
                else fail (InvalidLength(CheckLengthExpectation.LengthBetween(minimum, maximum), actual))

    /// <summary>Returns a type-directed text or concrete-collection check requiring exactly the supplied length.</summary>
    let inline length< ^value when (^value or Length) : (static member Create: ^value * int -> Check<^value>)> (expected: int) : Check<^value> =
        ((^value or Length) : (static member Create: ^value * int -> Check<^value>) (Unchecked.defaultof<^value>, expected))

    /// <summary>Returns a type-directed text or concrete-collection check requiring at least the supplied length.</summary>
    let inline minLength< ^value when (^value or MinLength) : (static member Create: ^value * int -> Check<^value>)> (minimum: int) : Check<^value> =
        ((^value or MinLength) : (static member Create: ^value * int -> Check<^value>) (Unchecked.defaultof<^value>, minimum))

    /// <summary>Returns a type-directed text or concrete-collection check requiring at most the supplied length.</summary>
    let inline maxLength< ^value when (^value or MaxLength) : (static member Create: ^value * int -> Check<^value>)> (maximum: int) : Check<^value> =
        ((^value or MaxLength) : (static member Create: ^value * int -> Check<^value>) (Unchecked.defaultof<^value>, maximum))

    /// <summary>Returns a type-directed text or concrete-collection check requiring a length inside inclusive bounds.</summary>
    let inline lengthBetween< ^value when (^value or LengthBetween) : (static member Create: ^value * int * int -> Check<^value>)> (minimum: int) (maximum: int) : Check<^value> =
        ((^value or LengthBetween) : (static member Create: ^value * int * int -> Check<^value>)
            (Unchecked.defaultof<^value>, minimum, maximum))

    /// <summary>Runs Axial's pragmatic email-format check against an already parsed string value.</summary>
    let email (value: string) : Result<unit, CheckFailure list> =
        String.email value

    /// <summary>Returns a string check requiring a match for the supplied regular expression pattern.</summary>
    let matches (pattern: string) : Check<string> =
        String.matches pattern

    /// <summary>Returns a string check requiring equality with one of the supplied choices.</summary>
    let oneOf (choices: string seq) : Check<string> =
        String.oneOf choices

    /// <summary>Returns an ordered-value check requiring a value inside the supplied inclusive bounds.</summary>
    let inline between minimum maximum : Check<'value> =
        Number.between minimum maximum

    /// <summary>Returns an ordered-value check requiring a value greater than the supplied exclusive lower bound.</summary>
    let inline greaterThan minimum : Check<'value> =
        Number.greaterThan minimum

    /// <summary>Returns an ordered-value check requiring a value less than the supplied exclusive upper bound.</summary>
    let inline lessThan maximum : Check<'value> =
        Number.lessThan maximum

    /// <summary>Returns an ordered-value check requiring a value greater than or equal to the supplied lower bound.</summary>
    let inline atLeast minimum : Check<'value> =
        Number.atLeast minimum

    /// <summary>Returns an ordered-value check requiring a value less than or equal to the supplied upper bound.</summary>
    let inline atMost maximum : Check<'value> =
        Number.atMost maximum

    /// <summary>Runs an ordered-value check requiring a value greater than zero.</summary>
    let inline positive value =
        Number.positive value

    /// <summary>Runs an ordered-value check requiring a value greater than or equal to zero.</summary>
    let inline nonNegative value =
        Number.nonNegative value

    /// <summary>Runs an ordered-value check requiring a value less than zero.</summary>
    let inline negative value =
        Number.negative value

    /// <summary>Runs an ordered-value check requiring a value less than or equal to zero.</summary>
    let inline nonPositive value =
        Number.nonPositive value

    /// <summary>Runs a sequence-shaped check requiring no duplicate values.</summary>
    let distinct (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.noDuplicates values

    /// <summary>Returns a sequence-shaped check requiring the supplied value to be present.</summary>
    let contains (expected: 'value) : Check<#seq<'value>> =
        fun values -> Seq.contains expected values

    /// <summary>Runs a sequence-shaped check requiring exactly one item.</summary>
    let single (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.single values

    /// <summary>Runs a sequence-shaped check requiring zero or one item.</summary>
    let atMostOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.atMostOne values

    /// <summary>Runs a sequence-shaped check requiring at least one item.</summary>
    let atLeastOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.atLeastOne values

    /// <summary>Runs a sequence-shaped check requiring more than one item.</summary>
    let moreThanOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.moreThanOne values

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Present =
        static member inline Invoke(value: ^value) : Result<unit, CheckFailure list> =
            let inline call (method: ^method, input: ^input, output: ^output) =
                ((^method or ^input or ^output): (static member Apply: _ * _ * _ -> _) (input, output, method))

            call (Unchecked.defaultof<Present>, value, Unchecked.defaultof<Result<unit, CheckFailure list>>)

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Empty =
        static member inline Invoke(value: ^value) : Result<unit, CheckFailure list> =
            let inline call (method: ^method, input: ^input, output: ^output) =
                ((^method or ^input or ^output): (static member Apply: _ * _ * _ -> _) (input, output, method))

            call (Unchecked.defaultof<Empty>, value, Unchecked.defaultof<Result<unit, CheckFailure list>>)

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type NotEmpty =
        static member inline Invoke(value: ^value) : Result<unit, CheckFailure list> =
            let inline call (method: ^method, input: ^input, output: ^output) =
                ((^method or ^input or ^output): (static member Apply: _ * _ * _ -> _) (input, output, method))

            call (Unchecked.defaultof<NotEmpty>, value, Unchecked.defaultof<Result<unit, CheckFailure list>>)

    /// <summary>Runs the type-directed presence check for an already parsed optional, nullable, text, or sequence-shaped value.</summary>
    let inline present (value: ^value) : Result<unit, CheckFailure list> =
        Present.Invoke value

    /// <summary>
    /// Runs the type-directed empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
    /// </summary>
    let inline empty (value: ^value) : Result<unit, CheckFailure list> =
        Empty.Invoke value

    /// <summary>
    /// Runs the type-directed non-empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
    /// </summary>
    let inline notEmpty (value: ^value) : Result<unit, CheckFailure list> =
        NotEmpty.Invoke value

    // These overloads must remain after the inline facades. Otherwise F# visits the first concrete overload while
    // compiling the facade and fixes the supposedly generic function to that overload's type.
    type Present with
        static member Apply(value: string, _: Result<unit, CheckFailure list>, _: Present) = String.present value
        static member Apply(value: 'value option, _: Result<unit, CheckFailure list>, _: Present) = Option.present value
        static member Apply(value: 'value voption, _: Result<unit, CheckFailure list>, _: Present) = ValueOption.present value
        static member Apply(value: System.Nullable<'value>, _: Result<unit, CheckFailure list>, _: Present) = Nullable.present value
        static member Apply(value: 'value list, _: Result<unit, CheckFailure list>, _: Present) = Seq.notEmpty value
        static member Apply(value: 'value array, _: Result<unit, CheckFailure list>, _: Present) = Seq.notEmpty value
        static member Apply(value: Map<string, 'value>, _: Result<unit, CheckFailure list>, _: Present) =
            if Object.ReferenceEquals(value, null) || Map.isEmpty value then Error [ Blank ] else Ok ()

    type Empty with
        static member Apply(value: string, _: Result<unit, CheckFailure list>, _: Empty) = String.empty value
        static member Apply(value: 'value option, _: Result<unit, CheckFailure list>, _: Empty) = Option.empty value
        static member Apply(value: 'value voption, _: Result<unit, CheckFailure list>, _: Empty) = ValueOption.empty value
        static member Apply(value: System.Nullable<'value>, _: Result<unit, CheckFailure list>, _: Empty) = Nullable.empty value
        static member Apply(value: 'value list, _: Result<unit, CheckFailure list>, _: Empty) = Seq.empty value
        static member Apply(value: 'value array, _: Result<unit, CheckFailure list>, _: Empty) = Seq.empty value

    type NotEmpty with
        static member Apply(value: string, _: Result<unit, CheckFailure list>, _: NotEmpty) = String.notEmpty value
        static member Apply(value: 'value option, _: Result<unit, CheckFailure list>, _: NotEmpty) = Option.notEmpty value
        static member Apply(value: 'value voption, _: Result<unit, CheckFailure list>, _: NotEmpty) = ValueOption.notEmpty value
        static member Apply(value: System.Nullable<'value>, _: Result<unit, CheckFailure list>, _: NotEmpty) = Nullable.notEmpty value
        static member Apply(value: 'value list, _: Result<unit, CheckFailure list>, _: NotEmpty) = Seq.notEmpty value
        static member Apply(value: 'value array, _: Result<unit, CheckFailure list>, _: NotEmpty) = Seq.notEmpty value

    /// <summary>Returns a value check requiring equality with the supplied expected value.</summary>
    let equalTo (expected: 'value) : Check<'value> =
        fun actual ->
            if actual = expected then Ok ()
            else fail (NotOneOf(string expected))

    /// <summary>Returns a value check requiring inequality with the supplied unexpected value.</summary>
    let notEqualTo (unexpected: 'value) : Check<'value> =
        fun actual ->
            if actual <> unexpected then Ok ()
            else fail (Custom(sprintf "notEqualTo:%O" unexpected))

    /// <summary>Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.</summary>
    /// <remarks>
    /// F# visits list elements from left to right. When the first check is a type-directed inline check such as
    /// <c>Check.present</c>, declare the composed program's value type so the compiler can select its SRTP overload:
    /// <c>let requiredName : Check&lt;string&gt; = Check.all [ Check.present; Check.lengthBetween 2 40 ]</c>.
    /// </remarks>
    let all (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let failures =
                checks
                |> List.collect (fun check ->
                    match check value with
                    | Ok _ -> []
                    | Error failures -> failures)

            if List.isEmpty failures then Ok () else Error failures

    /// <summary>Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.</summary>
    /// <remarks>
    /// As with <c>Check.all</c>, declare the composed program's value type when its first list element is a
    /// type-directed inline check; F# visits that first check before later elements can constrain its SRTP overload.
    /// </remarks>
    let any (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let rec loop failures remaining =
                match remaining with
                | [] -> Error(List.rev failures)
                | check :: rest ->
                    match check value with
                    | Ok _ -> Ok ()
                    | Error nextFailures -> loop (List.rev nextFailures @ failures) rest

            loop [] checks

    /// <summary>Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.</summary>
    let ``not`` (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok _ -> Error [ Custom "check.not" ]
            | Error _ -> Ok ()

    /// <summary>Maps every failure produced by a check.</summary>
    let mapFailure (mapper: CheckFailure -> CheckFailure) (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok _ as ok -> ok
            | Error failures -> Error(List.map mapper failures)
