namespace Axial.Refined

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

    member _.Bind
        (
            value: int,
            binder: PositiveInt -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.positiveInt value |> Result.bind binder

    member _.Bind
        (
            values: seq<'value>,
            binder: NonEmptyList<'value> -> Result<'next, RefinementError>
        ) : Result<'next, RefinementError> =
        Refine.nonEmptyList values |> Result.bind binder

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
