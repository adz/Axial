namespace Axial.ErrorHandling

/// <summary>Structured errors returned by sequence cardinality helpers.</summary>
type CardinalityFailure =
    /// <summary>The sequence was expected to contain exactly one item.</summary>
    | ExpectedSingle of observedCount: int
    /// <summary>The sequence was expected to contain at most one item.</summary>
    | ExpectedAtMostOne of observedCount: int

/// <summary>Fail-fast helpers over the standard F# <c>Result</c> type.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =
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

    /// <summary>Creates an <c>Ok</c> result.</summary>
    let inline ok value =
        Ok value

    /// <summary>Creates an <c>Error</c> result.</summary>
    let inline error failure =
        Error failure

    /// <summary>Maps the success value of a result.</summary>
    let map mapper result =
        match result with
        | Ok value -> Ok(mapper value)
        | Error failure -> Error failure

    /// <summary>Maps the error value of a result.</summary>
    let mapError mapper result =
        match result with
        | Ok value -> Ok value
        | Error failure -> Error(mapper failure)

    /// <summary>Binds a result to the next fail-fast operation.</summary>
    let bind binder result =
        match result with
        | Ok value -> binder value
        | Error failure -> Error failure

    /// <summary>Runs a value check and returns <c>Ok ()</c> or the check failures.</summary>
    let require (check: Check<'input>) (input: 'input) : Result<unit, CheckFailure list> =
        check input

    /// <summary>Runs a value check and keeps the original input when it succeeds.</summary>
    let guard (check: Check<'input>) (input: 'input) : Result<'input, CheckFailure list> =
        input
        |> require check
        |> map (fun () -> input)

    /// <summary>Returns <c>Ok ()</c> when the condition is true, or the supplied error when it is false.</summary>
    let inline checkOr (failure: 'error) (condition: bool) : Result<unit, 'error> =
        if condition then Ok () else Error failure

    /// <summary>Keeps the input value when the predicate is true, or returns the supplied error.</summary>
    let inline keepIf (predicate: 'input -> bool) (failure: 'error) (input: 'input) : Result<'input, 'error> =
        if predicate input then Ok input else Error failure

    /// <summary>Replaces a unit error with the supplied typed error.</summary>
    let inline withError (failure: 'error) (result: Result<'value, unit>) : Result<'value, 'error> =
        result |> mapError (fun () -> failure)

    /// <summary>Converts a .NET <c>Try*</c> tuple into a unit-error result.</summary>
    let fromTry (tryResult: bool * 'value) : Result<'value, unit> =
        match tryResult with
        | true, value -> Ok value
        | false, _ -> Error ()

    /// <summary>Converts an F# <c>Choice</c> into a result.</summary>
    let fromChoice (choice: Choice<'value, 'error>) : Result<'value, 'error> =
        match choice with
        | Choice1Of2 value -> Ok value
        | Choice2Of2 failure -> Error failure

    /// <summary>Drops the error channel and returns <c>Some</c> for success.</summary>
    let toOption (result: Result<'value, 'error>) : 'value option =
        match result with
        | Ok value -> Some value
        | Error _ -> None

    /// <summary>Drops the error channel and returns <c>ValueSome</c> for success.</summary>
    let toValueOption (result: Result<'value, 'error>) : 'value voption =
        match result with
        | Ok value -> ValueSome value
        | Error _ -> ValueNone

    /// <summary>Returns the success value or the supplied fallback value.</summary>
    let defaultValue (fallback: 'value) (result: Result<'value, 'error>) : 'value =
        match result with
        | Ok value -> value
        | Error _ -> fallback

    /// <summary>Takes the value from an option when it is <c>Some</c>, or returns the supplied error.</summary>
    let someOr (failure: 'error) (value: 'value option) : Result<'value, 'error> =
        match value with
        | Some inner -> Ok inner
        | None -> Error failure

    /// <summary>Returns success when the option is <c>None</c>, or returns the supplied error.</summary>
    let noneOr (failure: 'error) (value: 'value option) : Result<unit, 'error> =
        match value with
        | None -> Ok ()
        | Some _ -> Error failure

    /// <summary>Takes the value from a value option when it is <c>ValueSome</c>, or returns the supplied error.</summary>
    let valueSomeOr (failure: 'error) (value: 'value voption) : Result<'value, 'error> =
        match value with
        | ValueSome inner -> Ok inner
        | ValueNone -> Error failure

    /// <summary>Returns success when the value option is <c>ValueNone</c>, or returns the supplied error.</summary>
    let valueNoneOr (failure: 'error) (value: 'value voption) : Result<unit, 'error> =
        match value with
        | ValueNone -> Ok ()
        | ValueSome _ -> Error failure

    /// <summary>Takes the value from a nullable when it has a value, or returns the supplied error.</summary>
    let nullableOr (failure: 'error) (value: System.Nullable<'value>) : Result<'value, 'error> =
        if value.HasValue then Ok value.Value else Error failure

    /// <summary>Keeps a non-null reference, or returns the supplied error.</summary>
    let notNullOr (failure: 'error) (value: 'value when 'value: null) : Result<'value, 'error> =
        keepIf Check.notNull failure value

    /// <summary>Takes the successful value from a result, or returns the supplied error.</summary>
    let okOr (failure: 'nextError) (result: Result<'value, 'error>) : Result<'value, 'nextError> =
        match result with
        | Ok value -> Ok value
        | Error _ -> Error failure

    /// <summary>Takes the error value from a result, or returns the supplied error when the result is successful.</summary>
    let errorOr (failure: 'nextError) (result: Result<'value, 'error>) : Result<'error, 'nextError> =
        match result with
        | Error failure -> Ok failure
        | Ok _ -> Error failure

    /// <summary>Takes the first item from a sequence, or returns the supplied error.</summary>
    let headOr (failure: 'error) (values: seq<'value>) : Result<'value, 'error> =
        use enumerator = values.GetEnumerator()
        if enumerator.MoveNext() then Ok enumerator.Current else Error failure

    /// <summary>Keeps a non-null, non-empty, non-whitespace string.</summary>
    let notBlank value : Result<string, CheckFailure list> =
        guard Check.String.present value

    /// <summary>Keeps a string whose length lies between the supplied inclusive bounds.</summary>
    let length minimum maximum value : Result<string, CheckFailure list> =
        guard (Check.String.lengthBetween minimum maximum) value

    /// <summary>Keeps a string whose length is at least the supplied minimum.</summary>
    let minLength minimum value : Result<string, CheckFailure list> =
        guard (Check.String.minLength minimum) value

    /// <summary>Keeps a string whose length is at most the supplied maximum.</summary>
    let maxLength maximum value : Result<string, CheckFailure list> =
        guard (Check.String.maxLength maximum) value

    /// <summary>Keeps a string whose length equals the supplied expected length.</summary>
    let exactLength expected value : Result<string, CheckFailure list> =
        guard (Check.String.exactLength expected) value

    /// <summary>Keeps a value between the supplied inclusive bounds.</summary>
    let inline range minimum maximum value =
        guard (Check.Number.between minimum maximum) value

    /// <summary>Keeps a value greater than the supplied exclusive lower bound.</summary>
    let inline greaterThan minimum value =
        guard (Check.Number.greaterThan minimum) value

    /// <summary>Keeps a value less than the supplied exclusive upper bound.</summary>
    let inline lessThan maximum value =
        guard (Check.Number.lessThan maximum) value

    /// <summary>Keeps a value greater than or equal to the supplied lower bound.</summary>
    let inline atLeast minimum value =
        guard (Check.Number.atLeast minimum) value

    /// <summary>Keeps a value less than or equal to the supplied upper bound.</summary>
    let inline atMost maximum value =
        guard (Check.Number.atMost maximum) value

    /// <summary>Takes the only item from a sequence.</summary>
    let single (values: seq<'value>) : Result<'value, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 1, value -> Ok value
        | count, _ -> Error(ExpectedSingle count)

    /// <summary>Takes zero or one item from a sequence.</summary>
    let atMostOne (values: seq<'value>) : Result<'value option, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 0, _ -> Ok None
        | 1, value -> Ok(Some value)
        | count, _ -> Error(ExpectedAtMostOne count)

    /// <summary>Keeps a sequence that contains at least one item.</summary>
    let atLeastOne (values: seq<'value>) : Result<seq<'value>, CheckFailure list> =
        guard Check.Collection.notEmpty values

    /// <summary>Keeps a sequence that contains more than one item.</summary>
    let moreThanOne (values: seq<'value>) : Result<seq<'value>, CheckFailure list> =
        guard (Check.Collection.minCount 2) values
