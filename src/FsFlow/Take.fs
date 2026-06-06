namespace FsFlow

open System

/// <summary>
/// Value-returning validation helpers. <c>Take.whenX</c> keeps the original input when a property
/// holds, while bare <c>Take.x</c> extracts or narrows the value made available by that property.
/// </summary>
/// <remarks>
/// Most helpers return <see cref="T:FsFlow.Check`1" /> and can be assigned a domain error with
/// <c>Check.withError</c>. Cardinality helpers return <see cref="T:FsFlow.CardinalityFailure" />
/// so callers can preserve or map the count diagnostic.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Take =
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

    /// <summary>Keeps the option when it is <c>Some</c>.</summary>
    /// <param name="value">The option to check.</param>
    /// <returns><c>Ok option</c> for <c>Some</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// Some 5 |> Take.whenSome // Ok (Some 5)
    /// None |> Take.whenSome // Error ()
    /// </code>
    /// </example>
    let whenSome (value: 'value option) : Check<'value option> =
        match value with
        | Some _ -> Ok value
        | None -> Error ()

    /// <summary>Takes the value from an option when it is <c>Some</c>.</summary>
    /// <param name="value">The option to unwrap.</param>
    /// <returns><c>Ok value</c> for <c>Some value</c>; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// Some 5 |> Take.some // Ok 5
    /// None |> Take.some // Error ()
    /// </code>
    /// </example>
    let some (value: 'value option) : Check<'value> =
        OptionFlow.toUnitResult value

    /// <summary>Keeps the value option when it is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to check.</param>
    /// <returns><c>Ok valueOption</c> for <c>ValueSome</c>; otherwise <c>Error ()</c>.</returns>
    let whenValueSome (value: 'value voption) : Check<'value voption> =
        match value with
        | ValueSome _ -> Ok value
        | ValueNone -> Error ()

    /// <summary>Takes the value from a value option when it is <c>ValueSome</c>.</summary>
    /// <param name="value">The value option to unwrap.</param>
    /// <returns><c>Ok value</c> for <c>ValueSome value</c>; otherwise <c>Error ()</c>.</returns>
    let valueSome (value: 'value voption) : Check<'value> =
        OptionFlow.toUnitResultValueOption value

    /// <summary>Keeps the nullable when it has a value.</summary>
    /// <param name="value">The nullable value to check.</param>
    /// <returns><c>Ok nullable</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    let whenHasValue (value: Nullable<'value>) : Check<Nullable<'value>> =
        if value.HasValue then Ok value else Error ()

    /// <summary>Takes the value from a nullable when it has a value.</summary>
    /// <param name="value">The nullable value to unwrap.</param>
    /// <returns><c>Ok value</c> when <c>HasValue</c> is true; otherwise <c>Error ()</c>.</returns>
    let hasValue (value: Nullable<'value>) : Check<'value> =
        if value.HasValue then Ok value.Value else Error ()

    /// <summary>Keeps the reference when it is not null.</summary>
    /// <param name="value">The reference value to check.</param>
    /// <returns><c>Ok value</c> when non-null; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "hello" |> Take.whenNotNull // Ok "hello"
    /// null |> Take.whenNotNull // Error ()
    /// </code>
    /// </example>
    let whenNotNull (value: 'value when 'value : null) : Check<'value> =
        if isNull value then Error () else Ok value

    /// <summary>Keeps the collection when it is not empty.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> for non-empty collections; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// [ 1 ] |> Take.whenNotEmpty // Ok [1]
    /// [] |> Take.whenNotEmpty // Error ()
    /// </code>
    /// </example>
    let whenNotEmpty<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Check<'collection> =
        if Seq.isEmpty (values :> seq<'value>) then Error () else Ok values

    /// <summary>Keeps the string when it is not null or empty.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for non-empty strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "hello" |> Take.whenNotNullOrEmpty // Ok "hello"
    /// "" |> Take.whenNotNullOrEmpty // Error ()
    /// </code>
    /// </example>
    let whenNotNullOrEmpty (value: string) : Check<string> =
        if String.IsNullOrEmpty value then Error () else Ok value

    /// <summary>Keeps the string when it is not blank.</summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>Ok value</c> for non-blank strings; otherwise <c>Error ()</c>.</returns>
    /// <example>
    /// <code>
    /// "Ada" |> Take.whenNotBlank // Ok "Ada"
    /// "" |> Take.whenNotBlank // Error ()
    /// </code>
    /// </example>
    let whenNotBlank (value: string) : Check<string> =
        if String.IsNullOrWhiteSpace value then Error () else Ok value

    /// <summary>Keeps the collection when it contains exactly one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when exactly one item is present; otherwise a cardinality failure.</returns>
    /// <remarks>
    /// The check enumerates up to two items. If <paramref name="values" /> is a lazy sequence, the
    /// sequence may be enumerated once for the check and again by later code that consumes the
    /// returned value. Prefer reusable collections such as arrays or lists for this preserving form.
    /// </remarks>
    /// <example>
    /// <code>
    /// [ 5 ] |> Take.whenExactlyOne // Ok [5]
    /// [ 1; 2 ] |> Take.whenExactlyOne // Error (ExpectedExactlyOne 2)
    /// </code>
    /// </example>
    let whenExactlyOne<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        match cardinalityAtMostTwo (values :> seq<'value>) with
        | 1, _ -> Ok values
        | 0, _ -> Error(ExpectedExactlyOne 0)
        | count, _ -> Error(ExpectedExactlyOne count)

    /// <summary>Takes the only item from a sequence when it contains exactly one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok value</c> when exactly one item is present; otherwise a cardinality failure.</returns>
    /// <example>
    /// <code>
    /// [ 5 ] |> Take.exactlyOne // Ok 5
    /// [ 1; 2 ] |> Take.exactlyOne // Error (ExpectedExactlyOne 2)
    /// </code>
    /// </example>
    let exactlyOne (values: seq<'value>) : Result<'value, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 1, value -> Ok value
        | 0, _ -> Error(ExpectedExactlyOne 0)
        | count, _ -> Error(ExpectedExactlyOne count)

    /// <summary>Keeps the collection when it contains at most one item.</summary>
    /// <param name="values">The collection to check.</param>
    /// <returns><c>Ok values</c> when zero or one item is present; otherwise a cardinality failure.</returns>
    /// <remarks>
    /// The check enumerates up to two items. If <paramref name="values" /> is a lazy sequence, the
    /// sequence may be enumerated once for the check and again by later code that consumes the
    /// returned value. Prefer reusable collections such as arrays or lists for this preserving form.
    /// </remarks>
    let whenAtMostOne<'collection, 'value when 'collection :> seq<'value>>
        (values: 'collection)
        : Result<'collection, CardinalityFailure> =
        match cardinalityAtMostTwo (values :> seq<'value>) with
        | 0, _ -> Ok values
        | 1, _ -> Ok values
        | count, _ -> Error(ExpectedAtMostOne count)

    /// <summary>Takes zero or one item from a sequence when it contains at most one item.</summary>
    /// <param name="values">The sequence to check.</param>
    /// <returns><c>Ok None</c> for empty, <c>Ok (Some value)</c> for one item, or a cardinality failure.</returns>
    let atMostOne (values: seq<'value>) : Result<'value option, CardinalityFailure> =
        match cardinalityAtMostTwo values with
        | 0, _ -> Ok None
        | 1, value -> Ok(Some value)
        | count, _ -> Error(ExpectedAtMostOne count)
