namespace Axial.Flow

#if !FABLE_COMPILER
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
    /// <summary>Creates a stream from a synchronous sequence of values.</summary>
    /// <param name="values">The sequence of values to be emitted by the stream.</param>
    /// <returns>A <see cref="T:Axial.FlowStream`3"/> that yields each value from the sequence.</returns>
    /// <example>
    /// <code>
    /// let stream = FlowStream.fromSeq [1..10]
    /// </code>
    /// </example>
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

    /// <summary>Executes the stream and performs a synchronous action for each successful value.</summary>
    /// <param name="environment">The environment required to execute the stream.</param>
    /// <param name="action">The function to execute for each value emitted by the stream.</param>
    /// <param name="stream">The stream to execute.</param>
    /// <returns>A flow that represents the execution of the stream. If the stream fails, the flow fails with the same cause.</returns>
    /// <example>
    /// <code>
    /// let stream = FlowStream.fromSeq ["a"; "b"; "c"]
    /// let flow = FlowStream.runForEach () (printfn "%s") stream
    /// </code>
    /// </example>
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

    /// <summary>Transforms the successful values of a stream using the provided function.</summary>
    /// <param name="f">The function to transform each value.</param>
    /// <param name="stream">The stream whose values should be transformed.</param>
    /// <returns>A new stream that yields transformed values.</returns>
    /// <example>
    /// <code>
    /// let stream = FlowStream.fromSeq [1; 2; 3] |> FlowStream.map (fun n -> n * 2)
    /// </code>
    /// </example>
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
#endif
