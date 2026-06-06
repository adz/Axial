namespace FsFlow

open System
open System.Text.RegularExpressions

/// <summary>
/// A reusable unit-error result carrier. A <c>Check</c> says that a condition failed without
/// choosing the final domain error yet.
/// </summary>
/// <remarks>
/// Simple <c>Check</c> helpers return <c>Result&lt;'value, unit&gt;</c> and can be mapped into an
/// application error with <c>Check.withError</c>. Helpers with useful built-in diagnostics return
/// typed <c>Result</c> values directly, such as <c>CardinalityFailure</c> or <c>RangeFailure</c>.
/// </remarks>
type Check<'value> = Result<'value, unit>

/// <summary>Structured errors returned by sequence cardinality helpers.</summary>
type CardinalityFailure =
    /// <summary>The sequence was expected to contain exactly one item.</summary>
    | ExpectedSingle of observedCount: int
    /// <summary>The sequence was expected to contain at most one item.</summary>
    | ExpectedAtMostOne of observedCount: int
    /// <summary>The sequence was expected to contain at least one item.</summary>
    | ExpectedAtLeastOne
    /// <summary>The sequence was expected to contain more than one item.</summary>
    | ExpectedMoreThanOne of observedCount: int

/// <summary>Structured errors returned by string length helpers.</summary>
type StringLengthFailure =
    /// <summary>The string was shorter than the minimum length.</summary>
    | ExpectedMinLength of minLength: int * actualLength: int
    /// <summary>The string was longer than the maximum length.</summary>
    | ExpectedMaxLength of maxLength: int * actualLength: int
    /// <summary>The string length did not match the expected length.</summary>
    | ExpectedExactLength of expectedLength: int * actualLength: int

/// <summary>Structured errors returned by comparison and numeric helpers.</summary>
type RangeFailure<'value> =
    /// <summary>The value was expected to be greater than the supplied lower bound.</summary>
    | ExpectedGreaterThan of minimumExclusive: 'value * actual: 'value
    /// <summary>The value was expected to be less than the supplied upper bound.</summary>
    | ExpectedLessThan of maximumExclusive: 'value * actual: 'value
    /// <summary>The value was expected to be greater than or equal to the supplied lower bound.</summary>
    | ExpectedAtLeast of minimumInclusive: 'value * actual: 'value
    /// <summary>The value was expected to be less than or equal to the supplied upper bound.</summary>
    | ExpectedAtMost of maximumInclusive: 'value * actual: 'value
    /// <summary>The value was expected to be between the supplied inclusive bounds.</summary>
    | ExpectedBetween of minimumInclusive: 'value * maximumInclusive: 'value * actual: 'value

/// <summary>
/// Pure validation helpers. Unprefixed names are predicates, <c>when*</c> names preserve the
/// original input on success, and <c>take*</c> names extract an inner value or return a
/// deliberately different success shape.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    let private unitIf condition : Check<unit> =
        if condition then Ok () else Error ()

    let private keepValue value result =
        match result with
        | Ok () -> Ok value
        | Error error -> Error error

    let private stringLength (value: string) =
        if isNull value then 0 else value.Length

    let private referenceIsNull (value: 'value when 'value : null) =
        Object.ReferenceEquals(value, null)

    let private cardinalityAtMostTwo (values: seq<'value>) : int * 'value =
        use enumerator = values.GetEnumerator()
        let mutable count = 0
        let mutable first = Unchecked.defaultof<'value>

        if enumerator.MoveNext() then
            count <- 1
            first <- enumerator.Current

            if enumerator.MoveNext() then
                count <- 2

        count, first

    let private hasDuplicateValue (values: seq<'value>) =
        let seen = Collections.Generic.HashSet<'value>()
        values |> Seq.exists (fun value -> seen.Add value |> not)

    /// <summary>Builds a check from a predicate while preserving the successful value.</summary>
    /// <param name="predicate">The predicate to evaluate.</param>
    /// <param name="value">The value to check.</param>
    /// <returns><c>Ok value</c> when the predicate returns true; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// 5 |> Check.fromPredicate (fun x -> x &gt; 0) // Ok 5
    /// -1 |> Check.fromPredicate (fun x -> x &gt; 0) // Error ()
    /// </code>
    /// </example>
    let fromPredicate (predicate: 'value -> bool) (value: 'value) : Check<'value> =
        if predicate value then Ok value else Error ()

    /// <summary>Converts a .NET <c>Try*</c> tuple into a check result.</summary>
    /// <param name="tryResult">A tuple containing the success flag and parsed value.</param>
    /// <returns><c>Ok value</c> when the flag is true; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// System.Int32.TryParse "42" |> Check.fromTry // Ok 42
    /// System.Int32.TryParse "x" |> Check.fromTry // Error ()
    /// </code>
    /// </example>
    let fromTry (tryResult: bool * 'value) : Check<'value> =
        match tryResult with
        | true, value -> Ok value
        | false, _ -> Error ()

    /// <summary>Converts an F# <c>Choice</c> into a <c>Result</c>.</summary>
    /// <param name="choice">The choice value to convert.</param>
    /// <returns><c>Ok</c> for <c>Choice1Of2</c>; <c>Error</c> for <c>Choice2Of2</c>.</returns>
    /// <example>
    /// <code>
    /// Choice1Of2 42 |> Check.fromChoice // Ok 42
    /// Choice2Of2 "missing" |> Check.fromChoice // Error "missing"
    /// </code>
    /// </example>
    let fromChoice (choice: Choice<'value, 'error>) : Result<'value, 'error> =
        match choice with
        | Choice1Of2 value -> Ok value
        | Choice2Of2 error -> Error error

    /// <summary>Returns success when the supplied check fails.</summary>
    /// <param name="check">The source check.</param>
    /// <returns>A unit-success check with inverted success/failure.</returns>
    /// <example>
    /// <code>
    /// Check.isTrue true |> Check.negate // Error ()
    /// Check.isTrue false |> Check.negate // Ok ()
    /// </code>
    /// </example>
    let negate (check: Check<'value>) : Check<unit> =
        match check with
        | Ok _ -> Error ()
        | Error () -> Ok ()

    /// <summary>Returns success when both checks succeed.</summary>
    /// <param name="left">The first check.</param>
    /// <param name="right">The second check.</param>
    /// <returns>A unit-success check that short-circuits on the first failure.</returns>
    let both (left: Check<'left>) (right: Check<'right>) : Check<unit> =
        match left with
        | Error () -> Error ()
        | Ok _ ->
            match right with
            | Ok _ -> Ok ()
            | Error () -> Error ()

    /// <summary>Returns success when either check succeeds.</summary>
    /// <param name="left">The first check.</param>
    /// <param name="right">The second check.</param>
    /// <returns>A unit-success check that short-circuits on the first success.</returns>
    let either (left: Check<'left>) (right: Check<'right>) : Check<unit> =
        match left with
        | Ok _ -> Ok ()
        | Error () ->
            match right with
            | Ok _ -> Ok ()
            | Error () -> Error ()

    /// <summary>Returns success when every check in the sequence succeeds.</summary>
    /// <param name="checks">The checks to evaluate.</param>
    /// <returns>A unit-success check that short-circuits on the first failure.</returns>
    let all (checks: seq<Check<'value>>) : Check<unit> =
        use enumerator = checks.GetEnumerator()

        let mutable result = Ok ()
        let mutable continueLoop = true

        while continueLoop && enumerator.MoveNext() do
            match enumerator.Current with
            | Ok _ -> ()
            | Error () ->
                result <- Error ()
                continueLoop <- false

        result

    /// <summary>Returns success when at least one check in the sequence succeeds.</summary>
    /// <param name="checks">The checks to evaluate.</param>
    /// <returns>A unit-success check that short-circuits on the first success.</returns>
    let any (checks: seq<Check<'value>>) : Check<unit> =
        use enumerator = checks.GetEnumerator()

        let mutable result = Error ()
        let mutable continueLoop = true

        while continueLoop && enumerator.MoveNext() do
            match enumerator.Current with
            | Ok _ ->
                result <- Ok ()
                continueLoop <- false
            | Error () -> ()

        result

    /// <summary>Returns success when the condition is true.</summary>
    /// <param name="condition">The boolean condition to check.</param>
    /// <returns><c>Ok ()</c> when true; otherwise <c>Error ()</c>.</returns>
    let isTrue (condition: bool) : Check<unit> =
        unitIf condition

    /// <summary>Keeps the boolean when it is true.</summary>
    /// <param name="condition">The boolean condition to check.</param>
    /// <returns><c>Ok true</c> when true; otherwise <c>Error ()</c>.</returns>
    let whenTrue (condition: bool) : Check<bool> =
        isTrue condition |> keepValue condition

    /// <summary>Returns success when the condition is false.</summary>
    /// <param name="condition">The boolean condition to check.</param>
    /// <returns><c>Ok ()</c> when false; otherwise <c>Error ()</c>.</returns>
    let isFalse (condition: bool) : Check<unit> =
        unitIf (not condition)

    /// <summary>Keeps the boolean when it is false.</summary>
    /// <param name="condition">The boolean condition to check.</param>
    /// <returns><c>Ok false</c> when false; otherwise <c>Error ()</c>.</returns>
    let whenFalse (condition: bool) : Check<bool> =
        isFalse condition |> keepValue condition

    /// <summary>Returns success when the option is <c>Some</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok ()</c> for <c>Some</c>; otherwise <c>Error ()</c>.</returns>
    let isSome (value: 'value option) : Check<unit> =
        match value with
        | Some _ -> Ok ()
        | None -> Error ()

    /// <summary>Keeps the option when it is <c>Some</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok option</c> for <c>Some</c>; otherwise <c>Error ()</c>.</returns>
    let whenSome (value: 'value option) : Check<'value option> =
        isSome value |> keepValue value

    /// <summary>Takes the value from an option when it is <c>Some</c>.</summary>
    /// <param name="value">The option to unwrap.</param>
    /// <returns><c>Ok value</c> for <c>Some value</c>; otherwise <c>Error ()</c>.</returns>
    let takeSome (value: 'value option) : Check<'value> =
        match value with
        | Some inner -> Ok inner
        | None -> Error ()

    /// <summary>Returns success when the option is <c>None</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok ()</c> for <c>None</c>; otherwise <c>Error ()</c>.</returns>
    let isNone (value: 'value option) : Check<unit> =
        match value with
        | None -> Ok ()
        | Some _ -> Error ()

    /// <summary>Keeps the option when it is <c>None</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok None</c> for <c>None</c>; otherwise <c>Error ()</c>.</returns>
    let whenNone (value: 'value option) : Check<'value option> =
        isNone value |> keepValue value

    /// <summary>Returns success when the value option is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok ()</c> for <c>ValueSome</c>; otherwise <c>Error ()</c>.</returns>
    let isValueSome (value: 'value voption) : Check<unit> =
        match value with
        | ValueSome _ -> Ok ()
        | ValueNone -> Error ()

    /// <summary>Keeps the value option when it is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok valueOption</c> for <c>ValueSome</c>; otherwise <c>Error ()</c>.</returns>
    let whenValueSome (value: 'value voption) : Check<'value voption> =
        isValueSome value |> keepValue value

    /// <summary>Takes the value from a value option when it is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to unwrap.</param>
    /// <returns><c>Ok value</c> for <c>ValueSome value</c>; otherwise <c>Error ()</c>.</returns>
    let takeValueSome (value: 'value voption) : Check<'value> =
        match value with
        | ValueSome inner -> Ok inner
        | ValueNone -> Error ()

    /// <summary>Returns success when the value option is <c>ValueNone</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok ()</c> for <c>ValueNone</c>; otherwise <c>Error ()</c>.</returns>
    let isValueNone (value: 'value voption) : Check<unit> =
        match value with
        | ValueNone -> Ok ()
        | ValueSome _ -> Error ()

    /// <summary>Keeps the value option when it is <c>ValueNone</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok ValueNone</c> for <c>ValueNone</c>; otherwise <c>Error ()</c>.</returns>
    let whenValueNone (value: 'value voption) : Check<'value voption> =
        isValueNone value |> keepValue value

    /// <summary>Returns success when the nullable has a value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok ()</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    let hasValue (value: Nullable<'value>) : Check<unit> =
        unitIf value.HasValue

    /// <summary>Keeps the nullable when it has a value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok nullable</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    let whenHasValue (value: Nullable<'value>) : Check<Nullable<'value>> =
        hasValue value |> keepValue value

    /// <summary>Takes the value from a nullable when it has a value.</summary>
    /// <param name="value">The nullable value to unwrap.</param>
    /// <returns><c>Ok value</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    let takeHasValue (value: Nullable<'value>) : Check<'value> =
        if value.HasValue then Ok value.Value else Error ()

    /// <summary>Returns success when the nullable has no value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok ()</c> when <c>HasValue</c> is false; otherwise <c>Error ()</c>.</returns>
    let hasNoValue (value: Nullable<'value>) : Check<unit> =
        unitIf (not value.HasValue)

    /// <summary>Keeps the nullable when it has no value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok nullable</c> when <c>HasValue</c> is false; otherwise <c>Error ()</c>.</returns>
    let whenHasNoValue (value: Nullable<'value>) : Check<Nullable<'value>> =
        hasNoValue value |> keepValue value

    /// <summary>Returns success when the reference is not null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok ()</c> for non-null values; otherwise <c>Error ()</c>.</returns>
    let notNull (value: 'value when 'value : null) : Check<unit> =
        unitIf (not (referenceIsNull value))

    /// <summary>Keeps the reference when it is not null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok value</c> when non-null; otherwise <c>Error ()</c>.</returns>
    let whenNotNull (value: 'value when 'value : null) : Check<'value> =
        notNull value |> keepValue value

    /// <summary>Returns success when the reference is null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok ()</c> for null values; otherwise <c>Error ()</c>.</returns>
    let isNull (value: 'value when 'value : null) : Check<unit> =
        unitIf (referenceIsNull value)

    /// <summary>Keeps the reference when it is null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok null</c> when null; otherwise <c>Error ()</c>.</returns>
    let whenNull (value: 'value when 'value : null) : Check<'value> =
        isNull value |> keepValue value

    /// <summary>Returns success when the result is <c>Ok</c>.</summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>Ok ()</c> when the result is <c>Ok</c>; otherwise <c>Error ()</c>.</returns>
    let isOk (result: Result<'value, 'error>) : Check<unit> =
        match result with
        | Ok _ -> Ok ()
        | Error _ -> Error ()

    /// <summary>Keeps the result when it is <c>Ok</c>.</summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>Ok result</c> when the result is <c>Ok</c>; otherwise <c>Error ()</c>.</returns>
    let whenOk (result: Result<'value, 'error>) : Check<Result<'value, 'error>> =
        isOk result |> keepValue result

    /// <summary>Takes the successful value from a result when it is <c>Ok</c>.</summary>
    /// <param name="result">The result to unwrap.</param>
    /// <returns><c>Ok value</c> when the result is <c>Ok value</c>; otherwise <c>Error ()</c>.</returns>
    let takeOk (result: Result<'value, 'error>) : Check<'value> =
        match result with
        | Ok value -> Ok value
        | Error _ -> Error ()

    /// <summary>Returns success when the result is <c>Error</c>.</summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>Ok ()</c> when the result is <c>Error</c>; otherwise <c>Error ()</c>.</returns>
    let isError (result: Result<'value, 'error>) : Check<unit> =
        match result with
        | Error _ -> Ok ()
        | Ok _ -> Error ()

    /// <summary>Keeps the result when it is <c>Error</c>.</summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>Ok result</c> when the result is <c>Error</c>; otherwise <c>Error ()</c>.</returns>
    let whenError (result: Result<'value, 'error>) : Check<Result<'value, 'error>> =
        isError result |> keepValue result

    /// <summary>Takes the error value from a result when it is <c>Error</c>.</summary>
    /// <param name="result">The result to unwrap.</param>
    /// <returns><c>Ok error</c> when the result is <c>Error error</c>; otherwise <c>Error ()</c>.</returns>
    let takeError (result: Result<'value, 'error>) : Check<'error> =
        match result with
        | Error error -> Ok error
        | Ok _ -> Error ()

    /// <summary>Returns success when the sequence is not empty.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> for non-empty sequences; otherwise <c>Error ()</c>.</returns>
    let notEmpty (values: seq<'value>) : Check<unit> =
        unitIf (not (Seq.isEmpty values))

    /// <summary>Keeps the collection when it is not empty.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> for non-empty collections; otherwise <c>Error ()</c>.</returns>
    let whenNotEmpty<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Check<'collection> =
        notEmpty (values :> seq<'value>) |> keepValue values

    /// <summary>Takes the first item from a sequence when it is not empty.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok value</c> for the first item; otherwise <c>Error ()</c>.</returns>
    let takeHead (values: seq<'value>) : Check<'value> =
        use enumerator = values.GetEnumerator()
        if enumerator.MoveNext() then Ok enumerator.Current else Error ()

    /// <summary>Returns success when the sequence is empty.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> for empty sequences; otherwise <c>Error ()</c>.</returns>
    let empty (values: seq<'value>) : Check<unit> =
        unitIf (Seq.isEmpty values)

    /// <summary>Keeps the collection when it is empty.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> for empty collections; otherwise <c>Error ()</c>.</returns>
    let whenEmpty<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Check<'collection> =
        empty (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the string is not null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for non-empty strings; otherwise <c>Error ()</c>.</returns>
    let notNullOrEmpty (value: string) : Check<unit> =
        unitIf (not (String.IsNullOrEmpty value))

    /// <summary>Keeps the string when it is not null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for non-empty strings; otherwise <c>Error ()</c>.</returns>
    let whenNotNullOrEmpty (value: string) : Check<string> =
        notNullOrEmpty value |> keepValue value

    /// <summary>Returns success when the string is null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for null or empty strings; otherwise <c>Error ()</c>.</returns>
    let nullOrEmpty (value: string) : Check<unit> =
        unitIf (String.IsNullOrEmpty value)

    /// <summary>Keeps the string when it is null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for null or empty strings; otherwise <c>Error ()</c>.</returns>
    let whenNullOrEmpty (value: string) : Check<string> =
        nullOrEmpty value |> keepValue value

    /// <summary>Returns success when the string is exactly empty, not null.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for <c>""</c>; otherwise <c>Error ()</c>.</returns>
    let emptyString (value: string) : Check<unit> =
        unitIf (not (referenceIsNull value) && value.Length = 0)

    /// <summary>Keeps the string when it is exactly empty, not null.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for <c>""</c>; otherwise <c>Error ()</c>.</returns>
    let whenEmptyString (value: string) : Check<string> =
        emptyString value |> keepValue value

    /// <summary>Returns success when the string has length greater than zero.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for non-null strings with length greater than zero; otherwise <c>Error ()</c>.</returns>
    let notEmptyString (value: string) : Check<unit> =
        unitIf (not (referenceIsNull value) && value.Length > 0)

    /// <summary>Keeps the string when it has length greater than zero.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for non-null strings with length greater than zero; otherwise <c>Error ()</c>.</returns>
    let whenNotEmptyString (value: string) : Check<string> =
        notEmptyString value |> keepValue value

    /// <summary>Returns success when the string is not blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for non-blank strings; otherwise <c>Error ()</c>.</returns>
    let notBlank (value: string) : Check<unit> =
        unitIf (not (String.IsNullOrWhiteSpace value))

    /// <summary>Keeps the string when it is not blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for non-blank strings; otherwise <c>Error ()</c>.</returns>
    let whenNotBlank (value: string) : Check<string> =
        notBlank value |> keepValue value

    /// <summary>Returns success when the string is blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for null, empty, or whitespace strings; otherwise <c>Error ()</c>.</returns>
    let blank (value: string) : Check<unit> =
        unitIf (String.IsNullOrWhiteSpace value)

    /// <summary>Keeps the string when it is blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for null, empty, or whitespace strings; otherwise <c>Error ()</c>.</returns>
    let whenBlank (value: string) : Check<string> =
        blank value |> keepValue value

    /// <summary>Returns success when the string length is at least the supplied minimum.</summary>
    /// <param name="minimum">The minimum accepted length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> when the length is high enough; otherwise a length failure.</returns>
    let minLength (minimum: int) (value: string) : Result<unit, StringLengthFailure> =
        let actual = stringLength value
        if actual >= minimum then Ok () else Error(ExpectedMinLength(minimum, actual))

    /// <summary>Keeps the string when its length is at least the supplied minimum.</summary>
    /// <param name="minimum">The minimum accepted length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> when the length is high enough; otherwise a length failure.</returns>
    let whenMinLength (minimum: int) (value: string) : Result<string, StringLengthFailure> =
        minLength minimum value |> keepValue value

    /// <summary>Returns success when the string length is at most the supplied maximum.</summary>
    /// <param name="maximum">The maximum accepted length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> when the length is low enough; otherwise a length failure.</returns>
    let maxLength (maximum: int) (value: string) : Result<unit, StringLengthFailure> =
        let actual = stringLength value
        if actual <= maximum then Ok () else Error(ExpectedMaxLength(maximum, actual))

    /// <summary>Keeps the string when its length is at most the supplied maximum.</summary>
    /// <param name="maximum">The maximum accepted length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> when the length is low enough; otherwise a length failure.</returns>
    let whenMaxLength (maximum: int) (value: string) : Result<string, StringLengthFailure> =
        maxLength maximum value |> keepValue value

    /// <summary>Returns success when the string length equals the supplied length.</summary>
    /// <param name="expected">The expected length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> when the length matches; otherwise a length failure.</returns>
    let exactLength (expected: int) (value: string) : Result<unit, StringLengthFailure> =
        let actual = stringLength value
        if actual = expected then Ok () else Error(ExpectedExactLength(expected, actual))

    /// <summary>Keeps the string when its length equals the supplied length.</summary>
    /// <param name="expected">The expected length.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> when the length matches; otherwise a length failure.</returns>
    let whenExactLength (expected: int) (value: string) : Result<string, StringLengthFailure> =
        exactLength expected value |> keepValue value

    /// <summary>Returns success when the string matches the supplied regular expression pattern.</summary>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> when the string matches; otherwise <c>Error ()</c>.</returns>
    let matchesRegex (pattern: string) (value: string) : Check<unit> =
        unitIf (not (referenceIsNull value) && Regex.IsMatch(value, pattern))

    /// <summary>Keeps the string when it matches the supplied regular expression pattern.</summary>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> when the string matches; otherwise <c>Error ()</c>.</returns>
    let whenMatchesRegex (pattern: string) (value: string) : Check<string> =
        matchesRegex pattern value |> keepValue value

    /// <summary>Returns success when the actual value equals the expected value.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok ()</c> when equal; otherwise <c>Error ()</c>.</returns>
    let equalTo (expected: 'value) (actual: 'value) : Check<unit> =
        unitIf (actual = expected)

    /// <summary>Keeps the actual value when it equals the expected value.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok actual</c> when equal; otherwise <c>Error ()</c>.</returns>
    let whenEqualTo (expected: 'value) (actual: 'value) : Check<'value> =
        equalTo expected actual |> keepValue actual

    /// <summary>Returns success when the actual value does not equal the expected value.</summary>
    /// <param name="expected">The value that should not match.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok ()</c> when values differ; otherwise <c>Error ()</c>.</returns>
    let notEqualTo (expected: 'value) (actual: 'value) : Check<unit> =
        unitIf (actual <> expected)

    /// <summary>Keeps the actual value when it does not equal the expected value.</summary>
    /// <param name="expected">The value that should not match.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok actual</c> when values differ; otherwise <c>Error ()</c>.</returns>
    let whenNotEqualTo (expected: 'value) (actual: 'value) : Check<'value> =
        notEqualTo expected actual |> keepValue actual

    /// <summary>Returns success when the sequence contains the expected value.</summary>
    /// <param name="expected">The value to search for.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when the value is present; otherwise <c>Error ()</c>.</returns>
    let contains (expected: 'value) (values: seq<'value>) : Check<unit> =
        unitIf (Seq.contains expected values)

    /// <summary>Keeps the collection when it contains the expected value.</summary>
    /// <param name="expected">The value to search for.</param>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when the value is present; otherwise <c>Error ()</c>.</returns>
    let whenContains<'collection, 'value when 'collection :> seq<'value> and 'value: equality>
        (expected: 'value)
        (values: 'collection)
        : Check<'collection> =
        contains expected (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the sequence count equals the expected count.</summary>
    /// <param name="expected">The expected item count.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when the count matches; otherwise <c>Error ()</c>.</returns>
    let hasCount (expected: int) (values: seq<'value>) : Check<unit> =
        unitIf (Seq.length values = expected)

    /// <summary>Keeps the collection when its count equals the expected count.</summary>
    /// <param name="expected">The expected item count.</param>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when the count matches; otherwise <c>Error ()</c>.</returns>
    let whenCount<'collection, 'value when 'collection :> seq<'value>>
        (expected: int)
        (values: 'collection)
        : Check<'collection> =
        hasCount expected (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the sequence contains duplicate values.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when a duplicate is found; otherwise <c>Error ()</c>.</returns>
    let hasDuplicates (values: seq<'value>) : Check<unit> =
        unitIf (hasDuplicateValue values)

    /// <summary>Keeps the collection when it contains duplicate values.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when a duplicate is found; otherwise <c>Error ()</c>.</returns>
    let whenHasDuplicates<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Check<'collection> =
        hasDuplicates (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the sequence contains no duplicate values.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when all values are unique; otherwise <c>Error ()</c>.</returns>
    let hasNoDuplicates (values: seq<'value>) : Check<unit> =
        unitIf (not (hasDuplicateValue values))

    /// <summary>Keeps the collection when it contains no duplicate values.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when all values are unique; otherwise <c>Error ()</c>.</returns>
    let whenHasNoDuplicates<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Check<'collection> =
        hasNoDuplicates (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the sequence contains exactly one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when exactly one item is present; otherwise a cardinality failure.</returns>
    let isSingle (values: seq<'value>) : Result<unit, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 1, _ -> Ok ()
        | count, _ -> Error(ExpectedSingle count)

    /// <summary>Keeps the collection when it contains exactly one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when exactly one item is present; otherwise a cardinality failure.</returns>
    let whenSingle<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        isSingle (values :> seq<'value>) |> keepValue values

    /// <summary>Takes the only item from a sequence when it contains exactly one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok value</c> when exactly one item is present; otherwise a cardinality failure.</returns>
    let takeSingle (values: seq<'value>) : Result<'value, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 1, value -> Ok value
        | count, _ -> Error(ExpectedSingle count)

    /// <summary>Returns success when the sequence contains at most one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when zero or one item is present; otherwise a cardinality failure.</returns>
    let atMostOne (values: seq<'value>) : Result<unit, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 0, _ -> Ok ()
        | 1, _ -> Ok ()
        | count, _ -> Error(ExpectedAtMostOne count)

    /// <summary>Keeps the collection when it contains at most one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when zero or one item is present; otherwise a cardinality failure.</returns>
    let whenAtMostOne<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        atMostOne (values :> seq<'value>) |> keepValue values

    /// <summary>Takes zero or one item from a sequence when it contains at most one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok None</c> for empty, <c>Ok (Some value)</c> for one item, or a cardinality failure.</returns>
    let takeAtMostOne (values: seq<'value>) : Result<'value option, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 0, _ -> Ok None
        | 1, value -> Ok(Some value)
        | count, _ -> Error(ExpectedAtMostOne count)

    /// <summary>Returns success when the sequence contains at least one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when one or more items are present; otherwise a cardinality failure.</returns>
    let atLeastOne (values: seq<'value>) : Result<unit, CardinalityFailure> =
        if Seq.isEmpty values then Error ExpectedAtLeastOne else Ok ()

    /// <summary>Keeps the collection when it contains at least one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when one or more items are present; otherwise a cardinality failure.</returns>
    let whenAtLeastOne<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        atLeastOne (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the sequence contains more than one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when more than one item is present; otherwise a cardinality failure.</returns>
    let moreThanOne (values: seq<'value>) : Result<unit, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 2, _ -> Ok ()
        | count, _ -> Error(ExpectedMoreThanOne count)

    /// <summary>Keeps the collection when it contains more than one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when more than one item is present; otherwise a cardinality failure.</returns>
    let whenMoreThanOne<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        moreThanOne (values :> seq<'value>) |> keepValue values

    /// <summary>Returns success when the actual value is greater than the supplied bound.</summary>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is greater; otherwise a range failure.</returns>
    let inline greaterThan minimum actual =
        if actual > minimum then Ok () else Error(ExpectedGreaterThan(minimum, actual))

    /// <summary>Keeps the value when it is greater than the supplied bound.</summary>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is greater; otherwise a range failure.</returns>
    let inline whenGreaterThan minimum actual =
        match greaterThan minimum actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the actual value is less than the supplied bound.</summary>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is less; otherwise a range failure.</returns>
    let inline lessThan maximum actual =
        if actual < maximum then Ok () else Error(ExpectedLessThan(maximum, actual))

    /// <summary>Keeps the value when it is less than the supplied bound.</summary>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is less; otherwise a range failure.</returns>
    let inline whenLessThan maximum actual =
        match lessThan maximum actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the actual value is greater than or equal to the supplied bound.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is at least the bound; otherwise a range failure.</returns>
    let inline atLeast minimum actual =
        if actual >= minimum then Ok () else Error(ExpectedAtLeast(minimum, actual))

    /// <summary>Keeps the value when it is greater than or equal to the supplied bound.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is at least the bound; otherwise a range failure.</returns>
    let inline whenAtLeast minimum actual =
        match atLeast minimum actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the actual value is less than or equal to the supplied bound.</summary>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is at most the bound; otherwise a range failure.</returns>
    let inline atMost maximum actual =
        if actual <= maximum then Ok () else Error(ExpectedAtMost(maximum, actual))

    /// <summary>Keeps the value when it is less than or equal to the supplied bound.</summary>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is at most the bound; otherwise a range failure.</returns>
    let inline whenAtMost maximum actual =
        match atMost maximum actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the actual value is between the inclusive bounds.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is in range; otherwise a range failure.</returns>
    let inline between minimum maximum actual =
        if actual >= minimum && actual <= maximum then
            Ok ()
        else
            Error(ExpectedBetween(minimum, maximum, actual))

    /// <summary>Keeps the value when it is between the inclusive bounds.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is in range; otherwise a range failure.</returns>
    let inline whenBetween minimum maximum actual =
        match between minimum maximum actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the numeric value is greater than zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is positive; otherwise a range failure.</returns>
    let inline positive actual =
        greaterThan LanguagePrimitives.GenericZero actual

    /// <summary>Keeps the numeric value when it is greater than zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is positive; otherwise a range failure.</returns>
    let inline whenPositive actual =
        match positive actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the numeric value is greater than or equal to zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is non-negative; otherwise a range failure.</returns>
    let inline nonNegative actual =
        atLeast LanguagePrimitives.GenericZero actual

    /// <summary>Keeps the numeric value when it is greater than or equal to zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is non-negative; otherwise a range failure.</returns>
    let inline whenNonNegative actual =
        match nonNegative actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the numeric value is less than zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is negative; otherwise a range failure.</returns>
    let inline negative actual =
        lessThan LanguagePrimitives.GenericZero actual

    /// <summary>Keeps the numeric value when it is less than zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is negative; otherwise a range failure.</returns>
    let inline whenNegative actual =
        match negative actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Returns success when the numeric value is less than or equal to zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok ()</c> when the value is non-positive; otherwise a range failure.</returns>
    let inline nonPositive actual =
        atMost LanguagePrimitives.GenericZero actual

    /// <summary>Keeps the numeric value when it is less than or equal to zero.</summary>
    /// <param name="actual">The value to check.</param>
    /// <returns><c>Ok actual</c> when the value is non-positive; otherwise a range failure.</returns>
    let inline whenNonPositive actual =
        match nonPositive actual with
        | Ok () -> Ok actual
        | Error error -> Error error

    /// <summary>Assigns the supplied application error to a unit-error check failure.</summary>
    /// <param name="error">The domain error to return when the check fails.</param>
    /// <param name="result">The source unit-error check.</param>
    /// <returns>The successful check value, or the supplied error when the check fails.</returns>
    /// <example>
    /// <code>
    /// "" |> Check.notBlank |> Check.withError "Name required" // Error "Name required"
    /// "Ada" |> Check.notBlank |> Check.withError "Name required" // Ok ()
    /// </code>
    /// </example>
    let withError (error: 'error) (result: Check<'value>) : Result<'value, 'error> =
        match result with
        | Ok value -> Ok value
        | Error () -> Error error
