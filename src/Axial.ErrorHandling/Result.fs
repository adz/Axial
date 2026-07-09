namespace Axial.ErrorHandling

/// <summary>Fail-fast helpers over the standard F# <c>Result</c> type.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =
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

    /// <summary>Computes a fallback result from the source error when the result fails.</summary>
    /// <remarks>The lazy counterpart to <c>orElse</c>, matching the <c>Flow.orElseWith</c> and
    /// <c>Validation.orElseWith</c> naming and shape: the fallback runs only on failure, and can inspect the error
    /// that caused it.</remarks>
    /// <example>
    /// <code>
    /// let result = Error "boom"
    /// result |> Result.orElseWith (fun error -> Ok (String.length error)) // Ok 4
    /// </code>
    /// </example>
    let orElseWith (fallback: 'error -> Result<'value, 'error>) (result: Result<'value, 'error>) : Result<'value, 'error> =
        match result with
        | Ok value -> Ok value
        | Error failure -> fallback failure

    /// <summary>Falls back to another result when the source result fails.</summary>
    /// <example>
    /// <code>
    /// let result = Error "boom"
    /// result |> Result.orElse (Ok 5) // Ok 5
    /// </code>
    /// </example>
    let orElse (fallback: Result<'value, 'error>) (result: Result<'value, 'error>) : Result<'value, 'error> =
        orElseWith (fun _ -> fallback) result

    /// <summary>Returns <c>Ok ()</c> when the condition is true, or the supplied error when it is false.</summary>
    /// <remarks>The condition is already computed and stands alone, so there is no subject value to preserve on
    /// success. Use <c>okIf</c>/<c>failIf</c> instead when the value under test should flow through.</remarks>
    let inline requireTrue (failure: 'error) (condition: bool) : Result<unit, 'error> =
        if condition then Ok () else Error failure

    /// <summary>Keeps the input value when the predicate holds, or returns the supplied error.</summary>
    /// <remarks>Mirrors <c>Option.filter</c>: predicate first, subject piped last. The error is attached
    /// separately with <c>orError</c> so this stays a pure filter, same shape as its <c>Option</c> counterpart.</remarks>
    let inline okIf (predicate: 'input -> bool) (input: 'input) : Result<'input, unit> =
        if predicate input then Ok input else Error ()

    /// <summary>Keeps the input value when the predicate does not hold, or returns the supplied error.</summary>
    /// <remarks>The inverse of <c>okIf</c>: fails when the predicate is true, succeeds otherwise.</remarks>
    let inline failIf (predicate: 'input -> bool) (input: 'input) : Result<'input, unit> =
        if predicate input then Error () else Ok input

    /// <summary>Replaces whatever error a result carries with the supplied typed error. <c>Ok</c> passes through unchanged.</summary>
    /// <remarks>The natural follow-up to <c>okIf</c>/<c>failIf</c>, and to any <c>Check</c> call whose
    /// <c>CheckFailure list</c> should become a domain error: <c>value |> Check.String.present |> Result.orError MyError</c>.</remarks>
    let inline orError (failure: 'error) (result: Result<'value, 'discardedError>) : Result<'value, 'error> =
        result |> mapError (fun _ -> failure)

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
        value
        |> okIf (fun value -> not (obj.ReferenceEquals(value, null)))
        |> orError failure

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
