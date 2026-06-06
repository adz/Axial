namespace FsFlow

open System

/// <summary>
/// A reusable predicate result that succeeds with a value and carries <c>unit</c> as a placeholder
/// failure until the caller maps it into a domain-specific error.
/// </summary>
/// <remarks>
/// Most <c>Check</c> helpers succeed with <c>unit</c>. Constructors such as <c>fromPredicate</c>
/// can preserve a value, and the <c>Take</c> module provides value-returning validation helpers.
/// </remarks>
type Check<'value> = Result<'value, unit>

/// <summary>Structured errors returned by sequence cardinality helpers that preserve useful diagnostics.</summary>
type CardinalityFailure =
    /// <summary>The sequence was expected to contain exactly one item.</summary>
    | ExpectedExactlyOne of actualCount: int
    /// <summary>The sequence was expected to contain at most one item.</summary>
    | ExpectedAtMostOne of actualCount: int

/// <summary>
/// Predicate helpers that return <see cref="T:FsFlow.Check`1" /> values with a unit error, plus
/// bridge functions that turn those checks into application errors.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    let private cardinalityAtMostTwo (coll: seq<'value>) : int * 'value =
        use enumerator = coll.GetEnumerator()
        let mutable count = 0
        let mutable first = Unchecked.defaultof<'value>

        if enumerator.MoveNext() then
            count <- 1
            first <- enumerator.Current

            if enumerator.MoveNext() then
                count <- 2

        count, first

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
    /// <example>
    /// <code>
    /// Check.both (Check.isTrue true) (Check.some (Some 10)) // Ok ()
    /// Check.both (Check.isTrue true) (Check.isTrue false) // Error ()
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// Check.either (Check.isTrue false) (Check.some (Some 10)) // Ok ()
    /// Check.either (Check.isTrue false) (Check.isTrue false) // Error ()
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// [ Check.isTrue true; Check.isTrue true ] |> Check.all // Ok ()
    /// [ Check.isTrue true; Check.isTrue false ] |> Check.all // Error ()
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// [ Check.isTrue false; Check.isTrue true ] |> Check.any // Ok ()
    /// [ Check.isTrue false; Check.isTrue false ] |> Check.any // Error ()
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// true |> Check.isTrue // Ok ()
    /// false |> Check.isTrue // Error ()
    /// </code>
    /// </example>
    let isTrue (condition: bool) : Check<unit> =
        if condition then Ok () else Error ()

    /// <summary>Returns success when the condition is false.</summary>
    /// <param name="condition">The boolean condition to check.</param>
    /// <returns><c>Ok ()</c> when false; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// false |> Check.isFalse // Ok ()
    /// true |> Check.isFalse // Error ()
    /// </code>
    /// </example>
    let isFalse (condition: bool) : Check<unit> =
        if condition then Error () else Ok ()

    /// <summary>Returns success when the option is <c>Some</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok ()</c> for <c>Some</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// Some 5 |> Check.some // Ok ()
    /// None |> Check.some // Error ()
    /// </code>
    /// </example>
    let some (value: 'value option) : Check<unit> =
        match value with
        | Some _ -> Ok ()
        | None -> Error ()

    /// <summary>Returns success when the option is <c>None</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok ()</c> for <c>None</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// None |> Check.none // Ok ()
    /// Some 5 |> Check.none // Error ()
    /// </code>
    /// </example>
    let none (value: 'value option) : Check<unit> =
        match value with
        | None -> Ok ()
        | Some _ -> Error ()

    /// <summary>Returns success when the value option is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok ()</c> for <c>ValueSome</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// ValueSome 5 |> Check.valueSome // Ok ()
    /// ValueNone |> Check.valueSome // Error ()
    /// </code>
    /// </example>
    let valueSome (value: 'value voption) : Check<unit> =
        match value with
        | ValueSome _ -> Ok ()
        | ValueNone -> Error ()

    /// <summary>Returns success when the value option is <c>ValueNone</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok ()</c> for <c>ValueNone</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// ValueNone |> Check.valueNone // Ok ()
    /// ValueSome 5 |> Check.valueNone // Error ()
    /// </code>
    /// </example>
    let valueNone (value: 'value voption) : Check<unit> =
        match value with
        | ValueNone -> Ok ()
        | ValueSome _ -> Error ()

    /// <summary>Returns success when the nullable has a value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok ()</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// Nullable 5 |> Check.hasValue // Ok ()
    /// Nullable&lt;int&gt;() |> Check.hasValue // Error ()
    /// </code>
    /// </example>
    let hasValue (value: Nullable<'value>) : Check<unit> =
        if value.HasValue then Ok () else Error ()

    /// <summary>Returns success when the nullable has no value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok ()</c> when <c>HasValue</c> is false; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// Nullable&lt;int&gt;() |> Check.hasNoValue // Ok ()
    /// Nullable 5 |> Check.hasNoValue // Error ()
    /// </code>
    /// </example>
    let hasNoValue (value: Nullable<'value>) : Check<unit> =
        if value.HasValue then Error () else Ok ()

    /// <summary>Returns success when the reference is not null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok ()</c> for non-null values; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "hello" |> Check.notNull // Ok ()
    /// null |> Check.notNull // Error ()
    /// </code>
    /// </example>
    let notNull (value: 'value when 'value : null) : Check<unit> =
        if isNull value then Error () else Ok ()

    /// <summary>Returns success when the reference is null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok ()</c> for null values; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// null |> Check.isNull // Ok ()
    /// "hello" |> Check.isNull // Error ()
    /// </code>
    /// </example>
    let isNull (value: 'value when 'value : null) : Check<unit> =
        if isNull value then Ok () else Error ()

    /// <summary>Returns success when the sequence is not empty.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> for non-empty sequences; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 1 ] |> Check.notEmpty // Ok ()
    /// [] |> Check.notEmpty // Error ()
    /// </code>
    /// </example>
    let notEmpty (values: seq<'value>) : Check<unit> =
        if Seq.isEmpty values then Error () else Ok ()

    /// <summary>Returns success when the sequence is empty.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> for empty sequences; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [] |> Check.empty // Ok ()
    /// [ 1 ] |> Check.empty // Error ()
    /// </code>
    /// </example>
    let empty (values: seq<'value>) : Check<unit> =
        if Seq.isEmpty values then Ok () else Error ()

    /// <summary>Returns success when the string is not null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for non-empty strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "hello" |> Check.notNullOrEmpty // Ok ()
    /// "" |> Check.notNullOrEmpty // Error ()
    /// </code>
    /// </example>
    let notNullOrEmpty (value: string) : Check<unit> =
        if String.IsNullOrEmpty value then Error () else Ok ()

    /// <summary>Returns success when the string is null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for null or empty strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "" |> Check.nullOrEmpty // Ok ()
    /// "hello" |> Check.nullOrEmpty // Error ()
    /// </code>
    /// </example>
    let nullOrEmpty (value: string) : Check<unit> =
        if String.IsNullOrEmpty value then Ok () else Error ()

    /// <summary>Returns success when the string is not blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for non-blank strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "hello" |> Check.notBlank // Ok ()
    /// "  " |> Check.notBlank // Error ()
    /// </code>
    /// </example>
    let notBlank (value: string) : Check<unit> =
        if String.IsNullOrWhiteSpace value then Error () else Ok ()

    /// <summary>Returns success when the string is blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok ()</c> for null, empty, or whitespace strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "  " |> Check.blank // Ok ()
    /// "hello" |> Check.blank // Error ()
    /// </code>
    /// </example>
    let blank (value: string) : Check<unit> =
        if String.IsNullOrWhiteSpace value then Ok () else Error ()

    /// <summary>Returns success when the actual value equals the expected value.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok ()</c> when equal; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// actual |> Check.equalTo expected
    /// </code>
    /// </example>
    let equalTo (expected: 'value) (actual: 'value) : Check<unit> =
        if actual = expected then Ok () else Error ()

    /// <summary>Returns success when the actual value does not equal the expected value.</summary>
    /// <param name="expected">The value that should not match.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns><c>Ok ()</c> when values differ; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// actual |> Check.notEqualTo forbidden
    /// </code>
    /// </example>
    let notEqualTo (expected: 'value) (actual: 'value) : Check<unit> =
        if actual <> expected then Ok () else Error ()

    /// <summary>Returns success when the sequence contains the expected value.</summary>
    /// <param name="expected">The value to search for.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when the value is present; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 1; 2 ] |> Check.contains 2 // Ok ()
    /// [ 1; 2 ] |> Check.contains 3 // Error ()
    /// </code>
    /// </example>
    let contains (expected: 'value) (values: seq<'value>) : Check<unit> =
        if Seq.contains expected values then Ok () else Error ()

    /// <summary>Returns success when the sequence count equals the expected count.</summary>
    /// <param name="expected">The expected item count.</param>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when the count matches; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 1; 2 ] |> Check.hasCount 2 // Ok ()
    /// [ 1 ] |> Check.hasCount 2 // Error ()
    /// </code>
    /// </example>
    let hasCount (expected: int) (values: seq<'value>) : Check<unit> =
        if Seq.length values = expected then Ok () else Error ()

    /// <summary>Returns success when the sequence contains exactly one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when exactly one item is present; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 5 ] |> Check.exactlyOne // Ok ()
    /// [ 1; 2 ] |> Check.exactlyOne // Error ()
    /// </code>
    /// </example>
    let exactlyOne (values: seq<'value>) : Check<unit> =
        match cardinalityAtMostTwo values with
        | 1, _ -> Ok ()
        | _ -> Error ()

    /// <summary>Returns success when the sequence contains at most one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when zero or one item is present; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [] |> Check.atMostOne // Ok ()
    /// [ 1; 2 ] |> Check.atMostOne // Error ()
    /// </code>
    /// </example>
    let atMostOne (values: seq<'value>) : Check<unit> =
        match cardinalityAtMostTwo values with
        | 0, _ -> Ok ()
        | 1, _ -> Ok ()
        | _ -> Error ()

    /// <summary>Returns success when the sequence contains at least one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when one or more items are present; otherwise <c>Error ()</c>.</returns>
    let atLeastOne (values: seq<'value>) : Check<unit> =
        notEmpty values

    /// <summary>Returns success when the sequence contains more than one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when more than one item is present; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 1; 2 ] |> Check.moreThanOne // Ok ()
    /// [ 1 ] |> Check.moreThanOne // Error ()
    /// </code>
    /// </example>
    let moreThanOne (values: seq<'value>) : Check<unit> =
        match cardinalityAtMostTwo values with
        | 2, _ -> Ok ()
        | _ -> Error ()

    /// <summary>Returns success when the sequence contains duplicate values.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok ()</c> when a duplicate is found; otherwise <c>Error ()</c>.</returns>
    let hasDuplicates (values: seq<'value>) : Check<unit> =
        let seen = System.Collections.Generic.HashSet<'value>()
        values
        |> Seq.exists (fun value -> seen.Add value |> not)
        |> isTrue

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
