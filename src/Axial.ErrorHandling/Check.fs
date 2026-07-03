namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>Describes an expected string length for a failed check.</summary>
type CheckLengthExpectation =
    /// <summary>The value was expected to have at least the supplied length.</summary>
    | MinimumLength of minimum: int
    /// <summary>The value was expected to have at most the supplied length.</summary>
    | MaximumLength of maximum: int
    /// <summary>The value was expected to have exactly the supplied length.</summary>
    | ExactLength of expected: int
    /// <summary>The value was expected to have a length inside the inclusive bounds.</summary>
    | LengthBetween of minimum: int * maximum: int

/// <summary>Describes an expected ordered range for a failed check.</summary>
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

/// <summary>Describes an expected collection count for a failed check.</summary>
type CheckCountExpectation =
    /// <summary>The collection was expected to contain at least the supplied count.</summary>
    | MinimumCount of minimum: int
    /// <summary>The collection was expected to contain at most the supplied count.</summary>
    | MaximumCount of maximum: int
    /// <summary>The collection was expected to contain exactly the supplied count.</summary>
    | ExactCount of expected: int
    /// <summary>The collection was expected to contain a count inside the inclusive bounds.</summary>
    | CountBetween of minimum: int * maximum: int

/// <summary>Describes an equality expectation for a failed check.</summary>
type CheckEqualityExpectation =
    /// <summary>The value was expected to equal the supplied value description.</summary>
    | EqualTo of expected: string
    /// <summary>The value was expected not to equal the supplied value description.</summary>
    | NotEqualTo of unexpected: string

/// <summary>Describes a failed value check without attaching source paths or raw input.</summary>
type CheckFailure =
    /// <summary>A required value was missing.</summary>
    | Missing
    /// <summary>A required text value was blank.</summary>
    | Blank
    /// <summary>The value did not match the expected format.</summary>
    | InvalidFormat of expected: string
    /// <summary>The value length did not match the expected length constraint.</summary>
    | Length of expectation: CheckLengthExpectation * actualLength: int option
    /// <summary>The value did not match the expected ordered range constraint.</summary>
    | Range of expectation: CheckRangeExpectation * actual: string option
    /// <summary>The collection count did not match the expected count constraint.</summary>
    | Count of expectation: CheckCountExpectation * actualCount: int option
    /// <summary>The value did not match the expected equality constraint.</summary>
    | Equality of expectation: CheckEqualityExpectation * actual: string option
    /// <summary>A custom check identified by an application-defined code failed.</summary>
    | CustomCode of code: string

/// <summary>A typed value check that succeeds with <c>unit</c> or returns one or more check failures.</summary>
type Check<'value> = 'value -> Result<unit, CheckFailure list>

/// <summary>Typed value checks and pure, null-safe predicates for common structural checks.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    let private emailRegex =
        Regex(@"^[^@]+@[^@]+$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private numericRegex =
        Regex(@"^\d+$", RegexOptions.Compiled)

    let private stringLength (value: string) =
        if isNull value then 0 else value.Length

    /// <summary>Returns true when the option is <c>Some</c>.</summary>
    let isSome (value: 'value option) : bool =
        Option.isSome value

    /// <summary>Returns true when the option is <c>None</c>.</summary>
    let isNone (value: 'value option) : bool =
        Option.isNone value

    /// <summary>Returns true when the value option is <c>ValueSome</c>.</summary>
    let isValueSome (value: 'value voption) : bool =
        match value with
        | ValueSome _ -> true
        | ValueNone -> false

    /// <summary>Returns true when the value option is <c>ValueNone</c>.</summary>
    let isValueNone (value: 'value voption) : bool =
        match value with
        | ValueNone -> true
        | ValueSome _ -> false

    /// <summary>Returns true when the nullable contains a value.</summary>
    let hasValue (value: Nullable<'value>) : bool =
        value.HasValue

    /// <summary>Returns true when the nullable is empty.</summary>
    let hasNoValue (value: Nullable<'value>) : bool =
        not value.HasValue

    /// <summary>Returns true when the reference is null.</summary>
    let isNull (value: 'value when 'value: null) : bool =
        Object.ReferenceEquals(value, null)

    /// <summary>Returns true when the reference is not null.</summary>
    let notNull (value: 'value when 'value: null) : bool =
        not (isNull value)

    /// <summary>Returns true when the result is <c>Ok</c>.</summary>
    let isOk (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> true
        | Error _ -> false

    /// <summary>Returns true when the result is <c>Error</c>.</summary>
    let isError (result: Result<'value, 'error>) : bool =
        match result with
        | Error _ -> true
        | Ok _ -> false

    /// <summary>Returns true when the string is not null or empty.</summary>
    let notNullOrEmpty (value: string) : bool =
        not (String.IsNullOrEmpty value)

    /// <summary>Returns true when the string is null or empty.</summary>
    let nullOrEmpty (value: string) : bool =
        String.IsNullOrEmpty value

    /// <summary>Returns true when the string is exactly empty and not null.</summary>
    let emptyString (value: string) : bool =
        not (isNull value) && value.Length = 0

    /// <summary>Returns true when the string has at least one character and is not null.</summary>
    let notEmptyString (value: string) : bool =
        not (isNull value) && value.Length > 0

    /// <summary>Returns true when the string is not null, empty, or whitespace.</summary>
    let notBlank (value: string) : bool =
        not (String.IsNullOrWhiteSpace value)

    /// <summary>Returns true when the string is null, empty, or whitespace.</summary>
    let blank (value: string) : bool =
        String.IsNullOrWhiteSpace value

    /// <summary>Returns true when the string length is at least the supplied minimum.</summary>
    let hasMinLength (minimum: int) (value: string) : bool =
        not (isNull value) && value.Length >= minimum

    /// <summary>Returns true when the string length is at most the supplied maximum.</summary>
    let hasMaxLength (maximum: int) (value: string) : bool =
        stringLength value <= maximum

    /// <summary>Returns true when the string length equals the supplied expected length.</summary>
    let hasExactLength (expected: int) (value: string) : bool =
        not (isNull value) && value.Length = expected

    /// <summary>Returns true when the string matches the supplied regular expression pattern.</summary>
    let matchesRegex (pattern: string) (value: string) : bool =
        not (isNull value) && Regex.IsMatch(value, pattern)

    /// <summary>Returns true when the string matches a pragmatic email pattern.</summary>
    let isEmail (value: string) : bool =
        not (isNull value) && emailRegex.IsMatch value

    /// <summary>Returns true when the string contains only numeric characters.</summary>
    let isNumeric (value: string) : bool =
        not (isNull value) && numericRegex.IsMatch value

    /// <summary>Returns true when the string contains only letter or digit characters.</summary>
    let isAlphaNumeric (value: string) : bool =
        not (isNull value) && value |> Seq.forall Char.IsLetterOrDigit

    /// <summary>Returns true when the actual value equals the expected value.</summary>
    let equalTo (expected: 'value) (actual: 'value) : bool =
        actual = expected

    /// <summary>Returns true when the actual value does not equal the expected value.</summary>
    let notEqualTo (expected: 'value) (actual: 'value) : bool =
        actual <> expected

    /// <summary>Returns true when the value is greater than the supplied exclusive lower bound.</summary>
    let inline greaterThan minimum value =
        value > minimum

    /// <summary>Returns true when the value is less than the supplied exclusive upper bound.</summary>
    let inline lessThan maximum value =
        value < maximum

    /// <summary>Returns true when the value is greater than or equal to the supplied lower bound.</summary>
    let inline atLeast minimum value =
        value >= minimum

    /// <summary>Returns true when the value is less than or equal to the supplied upper bound.</summary>
    let inline atMost maximum value =
        value <= maximum

    /// <summary>Returns true when the value lies between the supplied inclusive bounds.</summary>
    let inline between minimum maximum value =
        value >= minimum && value <= maximum

    /// <summary>Returns true when the numeric value is greater than zero.</summary>
    let inline positive value =
        value > LanguagePrimitives.GenericZero

    /// <summary>Returns true when the numeric value is greater than or equal to zero.</summary>
    let inline nonNegative value =
        value >= LanguagePrimitives.GenericZero

    /// <summary>Returns true when the numeric value is less than zero.</summary>
    let inline negative value =
        value < LanguagePrimitives.GenericZero

    /// <summary>Returns true when the numeric value is less than or equal to zero.</summary>
    let inline nonPositive value =
        value <= LanguagePrimitives.GenericZero

    /// <summary>Returns true when the sequence is empty.</summary>
    let isEmpty (values: seq<'value>) : bool =
        Seq.isEmpty values

    /// <summary>Returns true when the sequence contains at least one item.</summary>
    let notEmpty (values: seq<'value>) : bool =
        not (Seq.isEmpty values)

    /// <summary>Returns true when the sequence contains the expected value.</summary>
    let contains (expected: 'value) (values: seq<'value>) : bool =
        Seq.contains expected values

    /// <summary>Returns true when the sequence count equals the expected count.</summary>
    let hasCount (expected: int) (values: seq<'value>) : bool =
        Seq.length values = expected

    /// <summary>Returns true when the sequence contains duplicate values.</summary>
    let hasDuplicates (values: seq<'value>) : bool =
        let seen = Collections.Generic.HashSet<'value>()
        values |> Seq.exists (fun value -> seen.Add value |> not)

    /// <summary>Returns true when the sequence contains no duplicate values.</summary>
    let hasNoDuplicates (values: seq<'value>) : bool =
        not (hasDuplicates values)

    /// <summary>Returns true when the sequence contains exactly one item.</summary>
    let isSingle (values: seq<'value>) : bool =
        use enumerator = values.GetEnumerator()
        enumerator.MoveNext() && not (enumerator.MoveNext())

    /// <summary>Returns true when the sequence contains zero or one item.</summary>
    let atMostOne (values: seq<'value>) : bool =
        use enumerator = values.GetEnumerator()
        not (enumerator.MoveNext()) || not (enumerator.MoveNext())

    /// <summary>Returns true when the sequence contains at least one item.</summary>
    let atLeastOne (values: seq<'value>) : bool =
        not (Seq.isEmpty values)

    /// <summary>Returns true when the sequence contains more than one item.</summary>
    let moreThanOne (values: seq<'value>) : bool =
        use enumerator = values.GetEnumerator()
        enumerator.MoveNext() && enumerator.MoveNext()

    /// <summary>Inverts a predicate.</summary>
    let negate (predicate: 'input -> bool) (input: 'input) : bool =
        not (predicate input)

    /// <summary>Runs every check against the value and accumulates all failures.</summary>
    let all (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let failures =
                checks
                |> List.collect (fun check ->
                    match check value with
                    | Ok () -> []
                    | Error failures -> failures)

            if List.isEmpty failures then Ok () else Error failures

    /// <summary>Runs checks until one succeeds, or returns the accumulated failures when every check fails.</summary>
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

    /// <summary>Inverts a check. A successful inner check becomes a custom-code failure.</summary>
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
