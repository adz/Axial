namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>Describes the length requirement that a value check expected a string-like value to satisfy.</summary>
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
/// caller-supplied bound. Zero-relative requirements (positive, negative, and their non-strict variants) are not
/// modeled here since they carry no bound to report; they are their own top-level
/// <see cref="T:Axial.ErrorHandling.CheckFailure" /> cases instead.</summary>
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

/// <summary>Describes the count requirement that a value check expected a sequence-shaped value to satisfy against a
/// caller-supplied count. Non-emptiness is not modeled here since it carries no count to report; it is its own
/// top-level <see cref="T:Axial.ErrorHandling.CheckFailure" /> case instead.</summary>
type CheckCountExpectation =
    /// <summary>The sequence was expected to contain at least the supplied count.</summary>
    | MinimumCount of minimum: int
    /// <summary>The sequence was expected to contain at most the supplied count.</summary>
    | MaximumCount of maximum: int
    /// <summary>The sequence was expected to contain exactly the supplied count.</summary>
    | ExactCount of expected: int
    /// <summary>The sequence was expected to contain a count inside the inclusive bounds.</summary>
    | CountBetween of minimum: int * maximum: int

/// <summary>Describes the equality requirement that a value check expected a value to satisfy.</summary>
type CheckEqualityExpectation =
    /// <summary>The value was expected to equal the supplied value description.</summary>
    | EqualTo of expected: string
    /// <summary>The value was expected not to equal the supplied value description.</summary>
    | NotEqualTo of unexpected: string

/// <summary>Describes why an executable value check failed, without attaching source paths or raw input.</summary>
type CheckFailure =
    /// <summary>A required value was missing.</summary>
    | Missing
    /// <summary>A required text value was blank.</summary>
    | Blank
    /// <summary>The value did not match the expected format.</summary>
    | InvalidFormat of expected: string
    /// <summary>The value length did not match the expected length constraint.</summary>
    | Length of expectation: CheckLengthExpectation * actualLength: int option
    /// <summary>The value was expected to be greater than zero.</summary>
    | Positive of actual: string option
    /// <summary>The value was expected to be greater than or equal to zero.</summary>
    | NonNegative of actual: string option
    /// <summary>The value was expected to be less than zero.</summary>
    | Negative of actual: string option
    /// <summary>The value was expected to be less than or equal to zero.</summary>
    | NonPositive of actual: string option
    /// <summary>The value did not match the expected ordered range constraint.</summary>
    | Range of expectation: CheckRangeExpectation * actual: string option
    /// <summary>The sequence was expected to contain at least one item.</summary>
    | NonEmpty of actualCount: int option
    /// <summary>The sequence count did not match the expected count constraint.</summary>
    | Count of expectation: CheckCountExpectation * actualCount: int option
    /// <summary>The value did not match the expected equality constraint.</summary>
    | Equality of expectation: CheckEqualityExpectation * actual: string option
    /// <summary>A custom value check identified by an application-defined code failed.</summary>
    | CustomCode of code: string

/// <summary>
/// A localizable set of message templates for rendering <see cref="T:Axial.ErrorHandling.CheckFailure" /> values.
/// </summary>
/// <remarks>
/// Each function receives only the already-formatted operand(s) (a length, a count, a bound, an actual value) and
/// returns the complete phrase for that piece of the message. A translation supplies grammar and word order; it
/// never needs to reimplement the traversal over every <c>CheckFailure</c>/expectation case, since
/// <see cref="M:Axial.ErrorHandling.CheckFailure.describeWith" /> owns that traversal and calls into these functions.
/// </remarks>
type CheckFailureResources =
    { /// <summary>Renders a length expectation, e.g. "at least 3 characters".</summary>
      Length: CheckLengthExpectation -> string
      /// <summary>Renders a range expectation, e.g. "greater than zero".</summary>
      Range: CheckRangeExpectation -> string
      /// <summary>Renders a count expectation, e.g. "at least one item".</summary>
      Count: CheckCountExpectation -> string
      /// <summary>Renders an equality expectation, e.g. "equal to expected".</summary>
      Equality: CheckEqualityExpectation -> string
      /// <summary>Renders a missing-value failure.</summary>
      Missing: string
      /// <summary>Renders a blank-value failure.</summary>
      Blank: string
      /// <summary>Renders an invalid-format failure given the expected format name.</summary>
      InvalidFormat: string -> string
      /// <summary>Renders a length failure given the rendered expectation and the actual length, if known.</summary>
      LengthFailure: string -> int option -> string
      /// <summary>Renders a "must be positive" failure given the actual value, if known.</summary>
      PositiveFailure: string option -> string
      /// <summary>Renders a "must be zero or greater" failure given the actual value, if known.</summary>
      NonNegativeFailure: string option -> string
      /// <summary>Renders a "must be negative" failure given the actual value, if known.</summary>
      NegativeFailure: string option -> string
      /// <summary>Renders a "must be zero or less" failure given the actual value, if known.</summary>
      NonPositiveFailure: string option -> string
      /// <summary>Renders a range failure given the rendered expectation and the actual value, if known.</summary>
      RangeFailure: string -> string option -> string
      /// <summary>Renders a "must not be empty" failure given the actual count, if known.</summary>
      NonEmptyFailure: int option -> string
      /// <summary>Renders a count failure given the rendered expectation and the actual count, if known.</summary>
      CountFailure: string -> int option -> string
      /// <summary>Renders an equality failure given the rendered expectation and the actual value, if known.</summary>
      EqualityFailure: string -> string option -> string
      /// <summary>Renders a custom-code failure given the application-defined code.</summary>
      CustomCode: string -> string }

/// <summary>Renders <see cref="T:Axial.ErrorHandling.CheckFailure" /> values as human-readable sentence fragments.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CheckFailure =
    /// <summary>The default English <see cref="T:Axial.ErrorHandling.CheckFailureResources" />, used by
    /// <see cref="M:Axial.ErrorHandling.CheckFailure.describe" /> and
    /// <see cref="M:Axial.ErrorHandling.CheckFailure.describeAll" />.</summary>
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
          Count =
            function
            | MinimumCount minimum -> $"at least {minimum} item(s)"
            | MaximumCount maximum -> $"at most {maximum} item(s)"
            | ExactCount expected -> $"exactly {expected} item(s)"
            | CountBetween(minimum, maximum) -> $"between {minimum} and {maximum} items"
          Equality =
            function
            | EqualTo expected -> $"equal to {expected}"
            | NotEqualTo unexpected -> $"not equal to {unexpected}"
          Missing = "value is required"
          Blank = "value must not be blank"
          InvalidFormat = fun expected -> $"value must match the expected {expected} format"
          LengthFailure =
            fun expectation actual ->
                match actual with
                | Some length -> $"expected length {expectation}, but was {length}"
                | None -> $"expected length {expectation}"
          PositiveFailure =
            function
            | Some value -> $"expected a value greater than zero, but was {value}"
            | None -> "expected a value greater than zero"
          NonNegativeFailure =
            function
            | Some value -> $"expected a value zero or greater, but was {value}"
            | None -> "expected a value zero or greater"
          NegativeFailure =
            function
            | Some value -> $"expected a value less than zero, but was {value}"
            | None -> "expected a value less than zero"
          NonPositiveFailure =
            function
            | Some value -> $"expected a value zero or less, but was {value}"
            | None -> "expected a value zero or less"
          RangeFailure =
            fun expectation actual ->
                match actual with
                | Some value -> $"expected a value {expectation}, but was {value}"
                | None -> $"expected a value {expectation}"
          NonEmptyFailure =
            function
            | Some count -> $"expected at least one item, but found {count}"
            | None -> "expected at least one item"
          CountFailure =
            fun expectation actual ->
                match actual with
                | Some count -> $"expected {expectation}, but found {count}"
                | None -> $"expected {expectation}"
          EqualityFailure =
            fun expectation actual ->
                match actual with
                | Some value -> $"expected a value {expectation}, but was {value}"
                | None -> $"expected a value {expectation}"
          CustomCode = fun code -> $"failed custom check '{code}'" }

    /// <summary>Renders a single check failure using the supplied <see cref="T:Axial.ErrorHandling.CheckFailureResources" />,
    /// with no trailing punctuation, e.g. <c>"expected a value greater than zero, but was 0"</c>.</summary>
    let describeWith (resources: CheckFailureResources) (failure: CheckFailure) : string =
        match failure with
        | Missing -> resources.Missing
        | Blank -> resources.Blank
        | InvalidFormat expected -> resources.InvalidFormat expected
        | Length(expectation, actual) -> resources.LengthFailure (resources.Length expectation) actual
        | Positive actual -> resources.PositiveFailure actual
        | NonNegative actual -> resources.NonNegativeFailure actual
        | Negative actual -> resources.NegativeFailure actual
        | NonPositive actual -> resources.NonPositiveFailure actual
        | Range(expectation, actual) -> resources.RangeFailure (resources.Range expectation) actual
        | NonEmpty actual -> resources.NonEmptyFailure actual
        | Count(expectation, actual) -> resources.CountFailure (resources.Count expectation) actual
        | Equality(expectation, actual) -> resources.EqualityFailure (resources.Equality expectation) actual
        | CustomCode code -> resources.CustomCode code

    /// <summary>Joins multiple check failures, rendered with the supplied resources, into one semicolon-separated message.</summary>
    let describeAllWith (resources: CheckFailureResources) (failures: CheckFailure list) : string =
        failures |> List.map (describeWith resources) |> String.concat "; "

    /// <summary>Renders a single check failure using <see cref="P:Axial.ErrorHandling.CheckFailure.english" />.</summary>
    let describe (failure: CheckFailure) : string =
        describeWith english failure

    /// <summary>Joins multiple check failures into one semicolon-separated message using
    /// <see cref="P:Axial.ErrorHandling.CheckFailure.english" />.</summary>
    let describeAll (failures: CheckFailure list) : string =
        describeAllWith english failures

/// <summary>
/// An executable, path-free value constraint over an already parsed value.
/// </summary>
/// <remarks>
/// A check succeeds with <c>Ok ()</c> or returns one or more structured <see cref="T:Axial.ErrorHandling.CheckFailure" />
/// values. Checks do not carry input paths, raw input, schema metadata, or refined-value construction; keep those concerns
/// in validation, parsing, schema, or refinement layers.
/// </remarks>
type Check<'value> = 'value -> Result<unit, CheckFailure list>

/// <summary>
/// Typed value-check programs for local structural facts.
/// </summary>
/// <remarks>
/// Top-level <c>Check.*</c> helpers return structured results, not booleans. Direct modules such as
/// <c>Check.String</c>, <c>Check.Number</c>, <c>Check.Seq</c>, <c>Check.Option</c>, <c>Check.ValueOption</c>,
/// <c>Check.Nullable</c>, and <c>Check.Result</c> contain the type-specific implementations. Top-level helpers such
/// as <c>lengthBetween</c>, <c>between</c>, and <c>countBetween</c> are aliases for common single-target checks, while
/// <c>present</c>, <c>empty</c>, and <c>notEmpty</c> are the small type-directed facade.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    /// <summary>Executable, path-free value checks for already parsed strings.</summary>
    module String =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualLength (value: string) =
            if isNull value then None else Some value.Length

        let private actualString (value: string) =
            if isNull value then None else Some value

        /// <summary>Requires an already parsed string value to be non-null and contain at least one non-whitespace character.</summary>
        let present : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif Predicate.String.blank value then fail Blank
                else pass

        /// <summary>Requires an already parsed string value to be exactly empty. Null fails as a missing value.</summary>
        let empty : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif Predicate.String.empty value then pass
                else fail (Length(ExactLength 0, actualLength value))

        /// <summary>Requires an already parsed string value to contain at least one character. Whitespace counts as present text.</summary>
        let notEmpty : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif Predicate.String.notEmpty value then pass
                else fail (Length(MinimumLength 1, Some 0))

        /// <summary>Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.</summary>
        let minLength (minimum: int) : Check<string> =
            fun value ->
                if Predicate.String.minLength minimum value then pass
                else fail (Length(MinimumLength minimum, actualLength value))

        /// <summary>Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.</summary>
        let maxLength (maximum: int) : Check<string> =
            fun value ->
                if Predicate.String.maxLength maximum value then pass
                else fail (Length(MaximumLength maximum, actualLength value))

        /// <summary>Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.</summary>
        let lengthBetween (minimum: int) (maximum: int) : Check<string> =
            fun value ->
                if Predicate.String.lengthBetween minimum maximum value then pass
                else fail (Length(LengthBetween(minimum, maximum), actualLength value))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let length (expected: int) : Check<string> =
            fun value ->
                if Predicate.String.length expected value then pass
                else fail (Length(ExactLength expected, actualLength value))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let exactLength (expected: int) : Check<string> =
            length expected

        /// <summary>Requires an already parsed string value to match Axial's pragmatic email format.</summary>
        let email : Check<string> =
            fun value ->
                if Predicate.String.email value then pass
                else fail (InvalidFormat "email")

        /// <summary>Requires an already parsed string value to match the supplied regular expression pattern.</summary>
        let matches (pattern: string) : Check<string> =
            fun value ->
                if Predicate.String.matches pattern value then pass
                else fail (InvalidFormat pattern)

        /// <summary>Requires an already parsed string value to contain one or more numeric characters.</summary>
        let numeric : Check<string> =
            fun value ->
                if Predicate.String.numeric value then pass
                else fail (InvalidFormat "numeric")

        /// <summary>Requires an already parsed string value to contain one or more letter or digit characters.</summary>
        let alphaNumeric : Check<string> =
            fun value ->
                if Predicate.String.alphaNumeric value then pass
                else fail (InvalidFormat "alphaNumeric")

        /// <summary>Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.</summary>
        let oneOf (choices: string seq) : Check<string> =
            let choices = choices |> Seq.toList
            let expected = System.String.Join("|", choices)

            fun value ->
                if not (isNull value) && List.contains value choices then pass
                else fail (Equality(EqualTo expected, actualString value))

    /// <summary>Executable, path-free value checks for already parsed ordered values.</summary>
    module Number =
        /// <summary>Requires a value to lie inside the supplied inclusive bounds.</summary>
        let inline between minimum maximum : Check<'value> =
            fun value ->
                if Predicate.Number.between minimum maximum value then Ok ()
                else Error [ Range(Between(string minimum, string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than the supplied exclusive lower bound.</summary>
        let inline greaterThan minimum : Check<'value> =
            fun value ->
                if Predicate.Number.greaterThan minimum value then Ok ()
                else Error [ Range(GreaterThan(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than the supplied exclusive upper bound.</summary>
        let inline lessThan maximum : Check<'value> =
            fun value ->
                if Predicate.Number.lessThan maximum value then Ok ()
                else Error [ Range(LessThan(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than or equal to the supplied lower bound.</summary>
        let inline atLeast minimum : Check<'value> =
            fun value ->
                if Predicate.Number.atLeast minimum value then Ok ()
                else Error [ Range(AtLeast(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than or equal to the supplied upper bound.</summary>
        let inline atMost maximum : Check<'value> =
            fun value ->
                if Predicate.Number.atMost maximum value then Ok ()
                else Error [ Range(AtMost(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than zero.</summary>
        let inline positive (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.positive value then Ok ()
            else Error [ Positive(Some(string value)) ]

        /// <summary>Requires a value to be greater than or equal to zero.</summary>
        let inline nonNegative (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.nonNegative value then Ok ()
            else Error [ NonNegative(Some(string value)) ]

        /// <summary>Requires a value to be less than zero.</summary>
        let inline negative (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.negative value then Ok ()
            else Error [ Negative(Some(string value)) ]

        /// <summary>Requires a value to be less than or equal to zero.</summary>
        let inline nonPositive (value: 'value) : Result<unit, CheckFailure list> =
            if Predicate.Number.nonPositive value then Ok ()
            else Error [ NonPositive(Some(string value)) ]

    /// <summary>Executable, path-free value checks for already parsed sequence-shaped values.</summary>
    /// <remarks>
    /// Use <c>Check.Seq</c> for sequence-shaped checks. The earlier <c>Check.Collection</c> surface is intentionally not
    /// retained in this pre-1.0 API.
    /// </remarks>
    module Seq =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualCount (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then None
            else Some(Microsoft.FSharp.Collections.Seq.length values)

        /// <summary>Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.</summary>
        let notEmpty : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.notEmpty values then pass
                else fail (NonEmpty(actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.</summary>
        let empty : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.empty values then pass
                else fail (Count(ExactCount 0, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.</summary>
        let count (expected: int) : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.count expected values then pass
                else fail (Count(ExactCount expected, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.</summary>
        let minCount (minimum: int) : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.minCount minimum values then pass
                else fail (Count(MinimumCount minimum, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.</summary>
        let maxCount (maximum: int) : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.maxCount maximum values then pass
                else fail (Count(MaximumCount maximum, actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.</summary>
        let countBetween (minimum: int) (maximum: int) : Check<#seq<'value>> =
            fun values ->
                if Predicate.Seq.countBetween minimum maximum values then pass
                else fail (Count(CountBetween(minimum, maximum), actualCount values))

        /// <summary>Requires an already parsed sequence-shaped value to contain no duplicate values.</summary>
        let noDuplicates : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then fail Missing
                elif Predicate.Seq.distinct values then pass
                else fail (CustomCode "seq.distinct")

        /// <summary>Requires an already parsed sequence-shaped value to contain the supplied value.</summary>
        let contains (expected: 'value) : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then fail Missing
                elif Predicate.Seq.contains expected values then pass
                else fail (Equality(EqualTo(string expected), None))

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

    /// <summary>Executable, path-free value checks for already parsed optional values.</summary>
    module Option =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires an option to contain a value.</summary>
        let some : Check<'value option> =
            fun value ->
                match value with
                | Some _ -> pass
                | None -> fail Missing

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let present : Check<'value option> =
            some

        /// <summary>Requires an option to contain no value.</summary>
        let none : Check<'value option> =
            fun value ->
                match value with
                | None -> pass
                | Some _ -> fail (Equality(EqualTo "None", Some "Some"))

        /// <summary>Alias for <c>none</c>; requires an option to contain no value.</summary>
        let empty : Check<'value option> =
            none

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let notEmpty : Check<'value option> =
            some

    /// <summary>Executable, path-free value checks for already parsed value option values.</summary>
    module ValueOption =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a value option to contain a value.</summary>
        let some : Check<'value voption> =
            fun value ->
                match value with
                | ValueSome _ -> pass
                | ValueNone -> fail Missing

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let present : Check<'value voption> =
            some

        /// <summary>Requires a value option to contain no value.</summary>
        let none : Check<'value voption> =
            fun value ->
                match value with
                | ValueNone -> pass
                | ValueSome _ -> fail (Equality(EqualTo "ValueNone", Some "ValueSome"))

        /// <summary>Alias for <c>none</c>; requires a value option to contain no value.</summary>
        let empty : Check<'value voption> =
            none

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let notEmpty : Check<'value voption> =
            some

    /// <summary>Executable, path-free value checks for already parsed nullable values.</summary>
    module Nullable =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a nullable value to contain a value.</summary>
        let hasValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then pass else fail Missing

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let present : Check<System.Nullable<'value>> =
            hasValue

        /// <summary>Requires a nullable value to contain no value.</summary>
        let hasNoValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then fail (Equality(EqualTo "null", Some "value")) else pass

        /// <summary>Alias for <c>hasNoValue</c>; requires a nullable value to contain no value.</summary>
        let empty : Check<System.Nullable<'value>> =
            hasNoValue

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let notEmpty : Check<System.Nullable<'value>> =
            hasValue

    /// <summary>Executable, path-free value checks for result values.</summary>
    module Result =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a result to contain a successful value.</summary>
        let ok : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Ok _ -> pass
                | Error _ -> fail (Equality(EqualTo "Ok", Some "Error"))

        /// <summary>Requires a result to contain an error value.</summary>
        let error : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Error _ -> pass
                | Ok _ -> fail (Equality(EqualTo "Error", Some "Ok"))

    let private pass : Result<unit, CheckFailure list> = Ok ()

    let private fail failure : Result<unit, CheckFailure list> =
        Error [ failure ]

    let private actualValue value =
        if Object.ReferenceEquals(box value, null) then None else Some(string value)

    /// <summary>Returns a string check requiring exactly the supplied length.</summary>
    let length (expected: int) : Check<string> =
        String.length expected

    /// <summary>Returns a string check requiring at least the supplied length.</summary>
    let minLength (minimum: int) : Check<string> =
        String.minLength minimum

    /// <summary>Returns a string check requiring at most the supplied length.</summary>
    let maxLength (maximum: int) : Check<string> =
        String.maxLength maximum

    /// <summary>Returns a string check requiring a length inside the supplied inclusive bounds.</summary>
    let lengthBetween (minimum: int) (maximum: int) : Check<string> =
        String.lengthBetween minimum maximum

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

    /// <summary>Returns a sequence-shaped check requiring exactly the supplied count.</summary>
    let count (expected: int) : Check<#seq<'value>> =
        Seq.count expected

    /// <summary>Returns a sequence-shaped check requiring at least the supplied count.</summary>
    let minCount (minimum: int) : Check<#seq<'value>> =
        Seq.minCount minimum

    /// <summary>Returns a sequence-shaped check requiring at most the supplied count.</summary>
    let maxCount (maximum: int) : Check<#seq<'value>> =
        Seq.maxCount maximum

    /// <summary>Returns a sequence-shaped check requiring a count inside the supplied inclusive bounds.</summary>
    let countBetween (minimum: int) (maximum: int) : Check<#seq<'value>> =
        Seq.countBetween minimum maximum

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
        static member Apply(value: string, _: 'result, _: Present) : 'result =
            String.present value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Present) : 'result =
            Option.present value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Present) : 'result =
            ValueOption.present value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Present) : 'result =
            Nullable.present value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Present): (static member Apply: ^value * 'result * Present -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Present>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Empty =
        static member Apply(value: string, _: 'result, _: Empty) : 'result =
            String.empty value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Empty) : 'result =
            Option.empty value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Empty) : 'result =
            ValueOption.empty value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Empty) : 'result =
            Nullable.empty value |> box :?> 'result

        static member Apply(value: 'value list, _: 'result, _: Empty) : 'result =
            Seq.empty value |> box :?> 'result

        static member Apply(value: 'value array, _: 'result, _: Empty) : 'result =
            Seq.empty value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Empty): (static member Apply: ^value * 'result * Empty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Empty>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type NotEmpty =
        static member Apply(value: string, _: 'result, _: NotEmpty) : 'result =
            String.notEmpty value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: NotEmpty) : 'result =
            Option.notEmpty value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: NotEmpty) : 'result =
            ValueOption.notEmpty value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: NotEmpty) : 'result =
            Nullable.notEmpty value |> box :?> 'result

        static member Apply(value: 'value list, _: 'result, _: NotEmpty) : 'result =
            Seq.notEmpty value |> box :?> 'result

        static member Apply(value: 'value array, _: 'result, _: NotEmpty) : 'result =
            Seq.notEmpty value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or NotEmpty): (static member Apply: ^value * 'result * NotEmpty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<NotEmpty>))

    /// <summary>Runs the type-directed presence check for an already parsed optional, nullable, or text value.</summary>
    let inline present value : Result<unit, CheckFailure list> =
        Present.Invoke value

    /// <summary>
    /// Runs the type-directed empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
    /// </summary>
    let inline empty value : Result<unit, CheckFailure list> =
        Empty.Invoke value

    /// <summary>
    /// Runs the type-directed non-empty check for an already parsed optional, nullable, text, or supported sequence-shaped value.
    /// </summary>
    let inline notEmpty value : Result<unit, CheckFailure list> =
        NotEmpty.Invoke value

    /// <summary>Returns a value check requiring equality with the supplied expected value.</summary>
    let equalTo (expected: 'value) : Check<'value> =
        fun actual ->
            if actual = expected then pass
            else fail (Equality(EqualTo(string expected), actualValue actual))

    /// <summary>Returns a value check requiring inequality with the supplied unexpected value.</summary>
    let notEqualTo (unexpected: 'value) : Check<'value> =
        fun actual ->
            if actual <> unexpected then pass
            else fail (Equality(NotEqualTo(string unexpected), actualValue actual))

    /// <summary>Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.</summary>
    let all (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let failures =
                checks
                |> List.collect (fun check ->
                    match check value with
                    | Ok () -> []
                    | Error failures -> failures)

            if List.isEmpty failures then Ok () else Error failures

    /// <summary>Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.</summary>
    let any (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let rec loop failures remaining =
                match remaining with
                | [] -> Error(List.rev failures)
                | check :: rest ->
                    match check value with
                    | Ok () -> Ok ()
                    | Error nextFailures -> loop (List.rev nextFailures @ failures) rest

            loop [] checks

    /// <summary>Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.</summary>
    let ``not`` (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok () -> Error [ CustomCode "check.not" ]
            | Error _ -> Ok ()

    /// <summary>Maps every failure produced by a check.</summary>
    let mapFailure (mapper: CheckFailure -> CheckFailure) (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok () -> Ok ()
            | Error failures -> Error(List.map mapper failures)
