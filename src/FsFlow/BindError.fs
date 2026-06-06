namespace FsFlow

#nowarn "0064" // SRTP dispatch witnesses intentionally constrain marker and output types.

open System.ComponentModel
open System.Threading.Tasks

/// <summary>
/// A marker that adapts a source error before <c>flow { }</c> binds it.
/// </summary>
/// <remarks>
/// Use <c>BindError.withError</c> for sources that fail with missingness or <c>unit</c>.
/// Use <c>BindError.map</c> for sources that already carry a meaningful error.
/// </remarks>
type BindError<'env, 'error, 'value> =
    private
    | BindError of Flow<'env, 'error, 'value>

/// <summary>Internal dispatch marker for <c>BindError.withError</c>.</summary>
/// <exclude/>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type BindErrorWithError =
    static member WithError
        (
            (source: Result<'value, unit>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        source
        |> Check.withError error
        |> Flow.fromResult
        |> BindError

    static member WithError((source: 'value option, error: 'error), _mthd: BindErrorWithError) : BindError<'env, 'error, 'value> =
        source
        |> OptionFlow.toResult error
        |> Flow.fromResult
        |> BindError

    static member WithError((source: 'value voption, error: 'error), _mthd: BindErrorWithError) : BindError<'env, 'error, 'value> =
        source
        |> OptionFlow.toResultValueOption error
        |> Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Flow<'env, unit, 'value>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        source
        |> Flow.mapError (fun () -> error)
        |> BindError

#if !FABLE_COMPILER
    static member WithError
        (
            (source: Async<Result<'value, unit>>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        async {
            let! result = source
            return Check.withError error result
        }
        |> Flow.fromAsync
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Async<'value option>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        async {
            let! value = source
            return OptionFlow.toResult error value
        }
        |> Flow.fromAsync
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Async<'value voption>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        async {
            let! value = source
            return OptionFlow.toResultValueOption error value
        }
        |> Flow.fromAsync
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Task<Result<'value, unit>>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! result = source
            return Check.withError error result
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Task<'value option>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! value = source
            return OptionFlow.toResult error value
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: Task<'value voption>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! value = source
            return OptionFlow.toResultValueOption error value
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: ValueTask<Result<'value, unit>>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! result = source.AsTask()
            return Check.withError error result
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: ValueTask<'value option>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! value = source.AsTask()
            return OptionFlow.toResult error value
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member WithError
        (
            (source: ValueTask<'value voption>, error: 'error),
            _mthd: BindErrorWithError
        ) : BindError<'env, 'error, 'value> =
        task {
            let! value = source.AsTask()
            return OptionFlow.toResultValueOption error value
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError
#endif

    static member inline Invoke (error: 'error) (source: 'source) : BindError<'env, 'error, 'value> =
        let inline call (mthd: ^marker, source: ^source, output: ^output) =
            ((^marker or ^source or ^output) :
                (static member WithError : (_ * _) * _ -> _) (source, error),
             mthd)

        call (
            Unchecked.defaultof<BindErrorWithError>,
            source,
            Unchecked.defaultof<BindError<'env, 'error, 'value>>
        )

/// <summary>Internal dispatch marker for <c>BindError.map</c>.</summary>
/// <exclude/>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type BindErrorMap =
    static member Map
        (
            (source: Result<'value, 'error1>, mapper: 'error1 -> 'error2),
            _mthd: BindErrorMap
        ) : BindError<'env, 'error2, 'value> =
        source
        |> Result.mapError mapper
        |> Flow.fromResult
        |> BindError

    static member Map
        (
            (source: Flow<'env, 'error1, 'value>, mapper: 'error1 -> 'error2),
            _mthd: BindErrorMap
        ) : BindError<'env, 'error2, 'value> =
        source
        |> Flow.mapError mapper
        |> BindError

#if !FABLE_COMPILER
    static member Map
        (
            (source: Async<Result<'value, 'error1>>, mapper: 'error1 -> 'error2),
            _mthd: BindErrorMap
        ) : BindError<'env, 'error2, 'value> =
        async {
            let! result = source
            return Result.mapError mapper result
        }
        |> Flow.fromAsync
        |> Flow.bind Flow.fromResult
        |> BindError

    static member Map
        (
            (source: Task<Result<'value, 'error1>>, mapper: 'error1 -> 'error2),
            _mthd: BindErrorMap
        ) : BindError<'env, 'error2, 'value> =
        task {
            let! result = source
            return Result.mapError mapper result
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError

    static member Map
        (
            (source: ValueTask<Result<'value, 'error1>>, mapper: 'error1 -> 'error2),
            _mthd: BindErrorMap
        ) : BindError<'env, 'error2, 'value> =
        task {
            let! result = source.AsTask()
            return Result.mapError mapper result
        }
        |> Flow.fromTask
        |> Flow.bind Flow.fromResult
        |> BindError
#endif

    static member inline Invoke (mapper: 'error1 -> 'error2) (source: 'source) : BindError<'env, 'error2, 'value> =
        let inline call (mthd: ^marker, source: ^source, output: ^output) =
            ((^marker or ^source or ^output) :
                (static member Map : (_ * _) * _ -> _) (source, mapper),
             mthd)

        call (
            Unchecked.defaultof<BindErrorMap>,
            source,
            Unchecked.defaultof<BindError<'env, 'error2, 'value>>
        )

/// <summary>Pipeable helpers for assigning or mapping errors before a source is bound by <c>flow { }</c>.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BindError =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let internal toFlow (BindError flow: BindError<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        flow

    /// <summary>Assigns an error to a missing or unit-error source before <c>flow { }</c> binds it.</summary>
    /// <param name="error">The error to use if the source fails.</param>
    /// <param name="source">The source to adapt.</param>
    /// <returns>A bind marker for the flow computation expression.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     let! user = maybeUser |> BindError.withError InvalidUser
    ///     do! isValid |> Check.isTrue |> BindError.withError InvalidInput
    /// }
    /// </code>
    /// </example>
    let inline withError (error: 'error) (source: 'source) : BindError<'env, 'error, 'value> =
        BindErrorWithError.Invoke error source

    /// <summary>Maps an existing source error before <c>flow { }</c> binds it.</summary>
    /// <param name="mapper">The error mapping function.</param>
    /// <param name="source">The source to adapt.</param>
    /// <returns>A bind marker for the flow computation expression.</returns>
    /// <example>
    /// <code>
    /// flow {
    ///     do! authorize user |> BindError.map Unauthorized
    /// }
    /// </code>
    /// </example>
    let inline map (mapper: 'error1 -> 'error2) (source: 'source) : BindError<'env, 'error2, 'value> =
        BindErrorMap.Invoke mapper source
