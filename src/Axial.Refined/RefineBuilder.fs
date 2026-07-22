namespace Axial.Refined

open System

module private RefineBuilderInternals =
    let bindParse parser text binder =
        parser text
        |> Result.mapError RefinementError.ParseFailed
        |> Result.bind binder

/// <summary>Computation expression builder for fail-fast structural refinement.</summary>
/// <exclude/>
type RefineBuilder() =
    member _.Return(value: 'value) : Result<'value, RefinementError> =
        Ok value

    member _.ReturnFrom(result: Result<'value, RefinementError>) : Result<'value, RefinementError> =
        result

    member _.ReturnFrom(result: Result<'value, ParseError>) : Result<'value, RefinementError> =
        result |> Result.mapError RefinementError.ParseFailed

    member _.Zero() : Result<unit, RefinementError> =
        Ok ()

    member _.Bind
        (
            result: Result<'value, RefinementError>,
            binder: 'value -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Result.bind binder result

    member _.Bind
        (
            result: Result<'value, ParseError>,
            binder: 'value -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        result
        |> Result.mapError RefinementError.ParseFailed
        |> Result.bind binder

    member _.Bind
        (
            value: string,
            binder: NonBlankString -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonBlankString value |> Result.bind binder

    member _.Bind(value: string, binder: int -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.int value binder

    member _.Bind(value: string, binder: int64 -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.long value binder

    member _.Bind(value: string, binder: decimal -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.decimal value binder

    member _.Bind(value: string, binder: float -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.float value binder

    member _.Bind(value: string, binder: bool -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.bool value binder

    member _.Bind(value: string, binder: Guid -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.guid value binder

    member _.Bind(value: string, binder: DateTime -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.dateTime value binder

    member _.Bind(value: string, binder: DateTimeOffset -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.dateTimeOffset value binder

#if NET8_0_OR_GREATER
    member _.Bind(value: string, binder: DateOnly -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.dateOnly value binder

    member _.Bind(value: string, binder: TimeOnly -> Result<'next, RefinementError>) : Result<'next, RefinementError> =
        RefineBuilderInternals.bindParse Parse.timeOnly value binder
#endif

    member _.Bind
        (
            value: string,
            binder: TrimmedString -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.trimmedString value |> Result.bind binder

    member _.Bind
        (
            value: string,
            binder: Slug -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.slug value |> Result.bind binder

    member _.Bind
        (
            input: string * int * int,
            binder: BoundedString -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let value, minLength, maxLength = input
        Refine.boundedString minLength maxLength value |> Result.bind binder

    member _.Bind
        (
            value: int,
            binder: PositiveInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.positiveInt value |> Result.bind binder

    member _.Bind
        (
            value: int,
            binder: NonZeroInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonZeroInt value |> Result.bind binder

    member _.Bind
        (
            value: int,
            binder: NonNegativeInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonNegativeInt value |> Result.bind binder

    member _.Bind
        (
            value: int,
            binder: NegativeInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.negativeInt value |> Result.bind binder

    member _.Bind
        (
            value: int,
            binder: NonPositiveInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonPositiveInt value |> Result.bind binder

    member _.Bind
        (
            values: seq<'value>,
            binder: NonEmptyList<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonEmptyList values |> Result.bind binder

    member _.Bind
        (
            values: seq<'value>,
            binder: NonEmptyArray<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonEmptyArray values |> Result.bind binder

    member _.Bind
        (
            values: seq<'value>,
            binder: DistinctList<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.distinctList values |> Result.bind binder

    member _.Bind
        (
            input: 'value list * int * int,
            binder: BoundedList<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let values, minCount, maxCount = input
        Refine.boundedList minCount maxCount values |> Result.bind binder

    member _.Bind
        (
            input: 'value array * int * int,
            binder: BoundedArray<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let values, minCount, maxCount = input
        Refine.boundedArray minCount maxCount values |> Result.bind binder

    member _.Bind
        (
            input: DateTimeOffset * DateTimeOffset,
            binder: DateTimeOffsetRange -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let start, finish = input
        Refine.dateTimeOffsetRange start finish |> Result.bind binder

#if NET8_0_OR_GREATER
    member _.Bind
        (
            input: DateOnly * DateOnly,
            binder: DateOnlyRange -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        let start, finish = input
        Refine.dateOnlyRange start finish |> Result.bind binder
#endif

    member _.Delay(factory: unit -> Result<'value, RefinementError>) : Result<'value, RefinementError> =
        factory ()

    member _.Run(result: Result<'value, RefinementError>) : Result<'value, RefinementError> =
        result

    member _.Combine
        (
            first: Result<unit, RefinementError>,
            second: Result<'value, RefinementError>
        ) : Result<'value, RefinementError> =
        Result.bind (fun () -> second) first
