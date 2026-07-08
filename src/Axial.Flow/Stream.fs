namespace Axial.Flow

open System.Threading

/// <summary>
/// A single step of a <see cref="T:Axial.Flow.FlowStream`3" />: either the stream is exhausted, or it produced
/// one value plus a thunk that continues the stream.
/// </summary>
/// <typeparam name="value">The type of the success values in the stream.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
type StreamStep<'value, 'error> =
    | Done
    | Next of 'value * (unit -> Execution<StreamStep<'value, 'error>, 'error>)

/// <summary>
/// Represents a cold stream of values that requires an environment, can fail with a typed error,
/// and supports backpressure.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success values in the stream.</typeparam>
type FlowStream<'env, 'error, 'value> =
    FlowStream of ('env -> CancellationToken -> Execution<StreamStep<'value, 'error>, 'error>)

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
        let rec step (enumerator: System.Collections.Generic.IEnumerator<'value>) () : Execution<StreamStep<'value, 'error>, 'error> =
            if enumerator.MoveNext() then
                Execution.ofValue (Next(enumerator.Current, step enumerator))
            else
                enumerator.Dispose()
                Execution.ofValue Done

        FlowStream(fun _ _ -> step (values.GetEnumerator()) ())

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
        let rec loop (nextStep: unit -> Execution<StreamStep<'value, 'error>, 'error>) : Execution<unit, 'error> =
            Execution.bind
                (fun step ->
                    match step with
                    | Done -> Execution.ofValue ()
                    | Next(value, continuation) ->
                        action value
                        loop continuation)
                (nextStep ())

        Flow(fun env cancellationToken -> loop (fun () -> op env cancellationToken))

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
        let rec mapStep
            (nextStep: unit -> Execution<StreamStep<'v, 'error>, 'error>)
            () : Execution<StreamStep<'w, 'error>, 'error> =
            Execution.bind
                (fun step ->
                    match step with
                    | Done -> Execution.ofValue Done
                    | Next(value, continuation) -> Execution.ofValue (Next(f value, mapStep continuation)))
                (nextStep ())

        FlowStream(fun env ct -> mapStep (fun () -> op env ct) ())
