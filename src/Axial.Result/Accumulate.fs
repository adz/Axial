namespace Axial.Result

/// <summary>Shared accumulation core for the applicative <c>result</c> builders.</summary>
/// <exclude/>
module internal Accumulate =
    /// Combines two independent results, appending both errors when both fail.
    let inline mergeSources
        (append: 'collection -> 'collection -> 'collection)
        (first: Result<'first, 'collection>)
        (second: Result<'second, 'collection>)
        : Result<'first * 'second, 'collection> =
        match first, second with
        | Ok firstValue, Ok secondValue -> Ok(firstValue, secondValue)
        | Error firstFailure, Error secondFailure -> Error(append firstFailure secondFailure)
        | Error failure, Ok _ -> Error failure
        | Ok _, Error failure -> Error failure

/// <summary>Applicative <c>result</c> builder collecting errors into a list. Reached through <c>result.list</c>.</summary>
/// <exclude/>
type ListResultBuilder() =
    member _.Return(value: 'value) : Result<'value, 'error list> =
        Ok value

    member _.ReturnFrom(result: Result<'value, 'error list>) : Result<'value, 'error list> =
        result

    member _.Zero() : Result<unit, 'error list> =
        Ok ()

    member _.Bind
        (
            result: Result<'value, 'error list>,
            binder: 'value -> Result<'next, 'error list>
        ) : Result<'next, 'error list> =
        Result.bind binder result

    member _.BindReturn(result: Result<'value, 'error list>, mapper: 'value -> 'next) : Result<'next, 'error list> =
        Result.map mapper result

    member _.MergeSources
        (
            first: Result<'first, 'error list>,
            second: Result<'second, 'error list>
        ) : Result<'first * 'second, 'error list> =
        Accumulate.mergeSources List.append first second

    member _.Source(result: Result<'value, 'error list>) : Result<'value, 'error list> =
        result

    member _.Delay(factory: unit -> Result<'value, 'error list>) : Result<'value, 'error list> =
        factory ()

    member _.Run(result: Result<'value, 'error list>) : Result<'value, 'error list> =
        result

/// <summary>Applicative <c>result</c> builder collecting errors into an array. Reached through <c>result.array</c>.</summary>
/// <exclude/>
type ArrayResultBuilder() =
    member _.Return(value: 'value) : Result<'value, 'error[]> =
        Ok value

    member _.ReturnFrom(result: Result<'value, 'error[]>) : Result<'value, 'error[]> =
        result

    member _.Zero() : Result<unit, 'error[]> =
        Ok ()

    member _.Bind
        (
            result: Result<'value, 'error[]>,
            binder: 'value -> Result<'next, 'error[]>
        ) : Result<'next, 'error[]> =
        Result.bind binder result

    member _.BindReturn(result: Result<'value, 'error[]>, mapper: 'value -> 'next) : Result<'next, 'error[]> =
        Result.map mapper result

    member _.MergeSources
        (
            first: Result<'first, 'error[]>,
            second: Result<'second, 'error[]>
        ) : Result<'first * 'second, 'error[]> =
        Accumulate.mergeSources Array.append first second

    member _.Source(result: Result<'value, 'error[]>) : Result<'value, 'error[]> =
        result

    member _.Delay(factory: unit -> Result<'value, 'error[]>) : Result<'value, 'error[]> =
        factory ()

    member _.Run(result: Result<'value, 'error[]>) : Result<'value, 'error[]> =
        result

/// <summary>
/// Lifting <c>Source</c> members for the applicative builders.
/// </summary>
/// <remarks>
/// These are extension members on purpose. Each builder already carries an intrinsic <c>Source</c> that accepts an
/// already-collected result unchanged, and the two overloads overlap: for <c>Result&lt;'value, 'error list&gt;</c>
/// both are applicable. F# prefers an intrinsic member over an extension member, so the identity overload wins for a
/// result that is already collected and the lifting overload below applies to every other error type. Declaring both
/// as intrinsic members makes the call ambiguous.
/// </remarks>
/// <exclude/>
[<AutoOpen>]
module AccumulateSourceExtensions =
    type ListResultBuilder with
        member _.Source(result: Result<'value, 'error>) : Result<'value, 'error list> =
            Result.mapError List.singleton result

    type ArrayResultBuilder with
        member _.Source(result: Result<'value, 'error>) : Result<'value, 'error[]> =
            Result.mapError Array.singleton result
