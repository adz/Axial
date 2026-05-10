namespace FsFlow

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents a cold stream of values that requires an environment, can fail with a typed error,
/// and supports backpressure.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success values in the stream.</typeparam>
type FlowStream<'env, 'error, 'value> =
    | FlowStream of ('env -> CancellationToken -> IAsyncEnumerable<Exit<'value, 'error>>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FlowStream =
    /// <summary>Creates a stream from a sequence of values.</summary>
    let fromSeq (values: seq<'value>) : FlowStream<'env, 'error, 'value> =
        FlowStream(fun _ _ ->
            { new IAsyncEnumerable<Exit<'value, 'error>> with
                member _.GetAsyncEnumerator(ct) =
                    let e = values.GetEnumerator()
                    { new IAsyncEnumerator<Exit<'value, 'error>> with
                        member _.Current = Exit.Success e.Current
                        member _.MoveNextAsync() = 
                            if ct.IsCancellationRequested then 
                                ValueTask<bool>(Task.FromCanceled<bool>(ct))
                            else
                                ValueTask<bool>(e.MoveNext())
                        member _.DisposeAsync() = 
                            e.Dispose()
                            ValueTask() }
            })

    /// <summary>Executes the stream and performs an action for each value.</summary>
    let runForEach 
        (environment: 'env) 
        (action: 'value -> unit) 
        (FlowStream op) 
        : Flow<'env, 'error, unit> =
        Flow(fun env cancellationToken ->
            ValueTask<Exit<unit, 'error>>(
                task {
                    let mutable failure = None
                    let enumerable = op env cancellationToken
                    let enumerator = enumerable.GetAsyncEnumerator(cancellationToken)
                    try
                        let mutable continuing = true
                        while failure.IsNone && continuing do
                            let! hasNext = enumerator.MoveNextAsync()
                            if hasNext then
                                match enumerator.Current with
                                | Exit.Success value -> action value
                                | Exit.Failure cause -> failure <- Some cause
                            else
                                continuing <- false
                    finally
                        let _ = enumerator.DisposeAsync()
                        ()

                    match failure with
                    | Some cause -> return Exit.Failure cause
                    | None -> return Exit.Success ()
                }))

    /// <summary>Maps the successful values of a stream.</summary>
    let map (f: 'v -> 'w) (FlowStream op) : FlowStream<'env, 'error, 'w> =
        FlowStream(fun env ct ->
            let enumerable = op env ct
            { new IAsyncEnumerable<Exit<'w, 'error>> with
                member _.GetAsyncEnumerator(innerCt) =
                    let enumerator = enumerable.GetAsyncEnumerator(innerCt)
                    { new IAsyncEnumerator<Exit<'w, 'error>> with
                        member _.Current = 
                            match enumerator.Current with
                            | Exit.Success v -> Exit.Success (f v)
                            | Exit.Failure c -> Exit.Failure c
                        member _.MoveNextAsync() = enumerator.MoveNextAsync()
                        member _.DisposeAsync() = enumerator.DisposeAsync() }
            })
