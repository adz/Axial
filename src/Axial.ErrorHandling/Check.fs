namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>Describes a failed value check.</summary>
type CheckFailure =
    private
    | CheckFailure of string

/// <summary>A typed value check that succeeds with <c>unit</c> or returns one or more check failures.</summary>
type Check<'value> = 'value -> Result<unit, CheckFailure list>

/// <summary>Pure, null-safe predicates for common structural checks.</summary>
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
