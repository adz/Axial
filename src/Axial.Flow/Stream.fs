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
    /// <summary>Creates a cold stream by repeatedly running an effectful state transition.</summary>
    /// <param name="step">Returns <c>Some(value, nextState)</c> or <c>None</c> when the stream is complete.</param>
    /// <param name="initialState">The state used for the first pull.</param>
    /// <example><code>FlowStream.unfoldFlow (fun n -> Flow.ok (if n &lt; 3 then Some(n, n + 1) else None)) 0</code></example>
    let unfoldFlow
        (step: 'state -> Flow<'env, 'error, ('value * 'state) option>)
        (initialState: 'state)
        : FlowStream<'env, 'error, 'value> =
        let rec pull environment cancellationToken state () =
            Flow.invoke (step state) environment cancellationToken
            |> Execution.bind (function
                | Some(value, nextState) -> Execution.ofValue(Next(value, pull environment cancellationToken nextState))
                | None -> Execution.ofValue Done)

        FlowStream(fun environment cancellationToken -> pull environment cancellationToken initialState ())

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

        FlowStream(fun _ _ ->
            let enumerator = values.GetEnumerator()
            RuntimeState.current().Scope.AddDisposable enumerator
            step enumerator ())

    /// <summary>Creates an empty stream.</summary>
    /// <example><code>FlowStream.empty&lt;unit, string, int&gt;</code></example>
    let empty<'env, 'error, 'value> : FlowStream<'env, 'error, 'value> = fromSeq Seq.empty

    /// <summary>Creates a stream containing one value.</summary>
    /// <example><code>FlowStream.singleton 42</code></example>
    let singleton value : FlowStream<'env, 'error, 'value> = fromSeq [ value ]

    /// <summary>Creates a one-element stream from an effectful value.</summary>
    /// <example><code>FlowStream.fromFlow (Flow.ok 42)</code></example>
    let fromFlow (flow: Flow<'env, 'error, 'value>) : FlowStream<'env, 'error, 'value> =
        unfoldFlow (fun consumed -> if consumed then Flow.ok None else flow |> Flow.map (fun value -> Some(value, true))) false

    /// <summary>Executes the stream and performs a synchronous action for each successful value.</summary>
    /// <param name="action">The function to execute for each value emitted by the stream.</param>
    /// <param name="stream">The stream to execute.</param>
    /// <returns>A flow that represents the execution of the stream. If the stream fails, the flow fails with the same cause.</returns>
    /// <example>
    /// <code>
    /// let stream = FlowStream.fromSeq ["a"; "b"; "c"]
    /// let flow = FlowStream.runForEach (printfn "%s") stream
    /// </code>
    /// </example>
    let runForEach
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

    /// <summary>Transforms the typed error channel of a stream.</summary>
    /// <example><code>stream |&gt; FlowStream.mapError DomainError</code></example>
    let mapError (mapper: 'error -> 'nextError) (FlowStream op) : FlowStream<'env, 'nextError, 'value> =
        let rec loop next () =
            next ()
            |> Execution.mapError mapper
            |> Execution.map (function Done -> Done | Next(value, tail) -> Next(value, loop tail))
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Keeps values that satisfy a predicate.</summary>
    /// <example><code>stream |&gt; FlowStream.filter (fun value -&gt; value &gt; 0)</code></example>
    let filter predicate (FlowStream op) =
        let rec loop next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) when predicate value -> Execution.ofValue(Next(value, loop tail))
                | Next(_, tail) -> loop tail ())
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Maps and filters values in one operation.</summary>
    /// <example><code>stream |&gt; FlowStream.choose id</code></example>
    let choose chooser (FlowStream op) =
        let rec loop next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) ->
                    match chooser value with
                    | Some selected -> Execution.ofValue(Next(selected, loop tail))
                    | None -> loop tail ())
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Runs an effect for each value before emitting the original value.</summary>
    /// <example><code>stream |&gt; FlowStream.tapFlow logValue</code></example>
    let tapFlow action (FlowStream op) =
        let rec loop env ct next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) ->
                    Flow.invoke (action value) env ct
                    |> Execution.map (fun () -> Next(value, loop env ct tail)))
        FlowStream(fun env ct -> loop env ct (fun () -> op env ct) ())

    /// <summary>Transforms every value with a Flow effect.</summary>
    /// <example><code>ids |&gt; FlowStream.mapFlow load</code></example>
    let mapFlow mapper (FlowStream op) =
        let rec loop env ct next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) -> Flow.invoke (mapper value) env ct |> Execution.map (fun mapped -> Next(mapped, loop env ct tail)))
        FlowStream(fun env ct -> loop env ct (fun () -> op env ct) ())

    /// <summary>Emits at most <paramref name="count"/> values.</summary>
    /// <example><code>stream |&gt; FlowStream.take 10</code></example>
    let take count (FlowStream op) =
        if count < 0 then invalidArg (nameof count) "Count cannot be negative."
        let rec loop remaining next () =
            if remaining = 0 then Execution.ofValue Done else
            next () |> Execution.map (function Done -> Done | Next(value, tail) -> Next(value, loop (remaining - 1) tail))
        FlowStream(fun env ct -> loop count (fun () -> op env ct) ())

    /// <summary>Skips the first <paramref name="count"/> values.</summary>
    /// <example><code>stream |&gt; FlowStream.skip 10</code></example>
    let skip count (FlowStream op) =
        if count < 0 then invalidArg (nameof count) "Count cannot be negative."
        let rec drop remaining next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(_, tail) when remaining > 0 -> drop (remaining - 1) tail ()
                | Next(value, tail) -> Execution.ofValue(Next(value, tail)))
        FlowStream(fun env ct -> drop count (fun () -> op env ct) ())

    /// <summary>Emits values while a predicate remains true.</summary>
    /// <example><code>stream |&gt; FlowStream.takeWhile (fun value -&gt; value &lt; 100)</code></example>
    let takeWhile predicate (FlowStream op) =
        let rec loop next () =
            next () |> Execution.map (function
                | Next(value, tail) when predicate value -> Next(value, loop tail)
                | _ -> Done)
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Skips values while a predicate remains true.</summary>
    /// <example><code>stream |&gt; FlowStream.skipWhile String.IsNullOrEmpty</code></example>
    let skipWhile predicate (FlowStream op) =
        let rec dropping next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) when predicate value -> dropping tail ()
                | Next(value, tail) -> Execution.ofValue(Next(value, tail)))
        FlowStream(fun env ct -> dropping (fun () -> op env ct) ())

    /// <summary>Emits each value paired with its zero-based index.</summary>
    /// <example><code>stream |&gt; FlowStream.indexed</code></example>
    let indexed (FlowStream op) =
        let rec loop index next () =
            next () |> Execution.map (function Done -> Done | Next(value, tail) -> Next((index, value), loop (index + 1) tail))
        FlowStream(fun env ct -> loop 0 (fun () -> op env ct) ())

    /// <summary>Emits successive accumulator states.</summary>
    /// <example><code>stream |&gt; FlowStream.scan (+) 0</code></example>
    let scan folder initial (FlowStream op) =
        let rec loop state next () =
            next () |> Execution.map (function
                | Done -> Done
                | Next(value, tail) -> let nextState = folder state value in Next(nextState, loop nextState tail))
        FlowStream(fun env ct -> loop initial (fun () -> op env ct) ())

    /// <summary>Suppresses consecutive duplicate values according to a projection.</summary>
    /// <example><code>stream |&gt; FlowStream.distinctUntilChangedBy id</code></example>
    let distinctUntilChangedBy projection (FlowStream op) =
        let rec loop previous next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) ->
                    let key = projection value
                    if previous = Some key then loop previous tail ()
                    else Execution.ofValue(Next(value, loop (Some key) tail)))
        FlowStream(fun env ct -> loop None (fun () -> op env ct) ())

    /// <summary>Concatenates two streams, evaluating the second only after the first ends.</summary>
    /// <example><code>first |&gt; FlowStream.append second</code></example>
    let append (FlowStream right) (FlowStream left) =
        let rec consumeLeft env ct next () =
            next () |> Execution.bind (function
                | Done -> right env ct
                | Next(value, tail) -> Execution.ofValue(Next(value, consumeLeft env ct tail)))
        FlowStream(fun env ct -> consumeLeft env ct (fun () -> left env ct) ())

    /// <summary>Maps each value to a stream and concatenates the resulting streams.</summary>
    /// <example><code>stream |&gt; FlowStream.collect FlowStream.fromSeq</code></example>
    let collect mapper (FlowStream outer) =
        let rec pullOuter env ct nextOuter () =
            nextOuter () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, outerTail) ->
                    let (FlowStream inner) = mapper value
                    pullInner env ct outerTail (fun () -> inner env ct) ())
        and pullInner env ct outerTail nextInner () =
            nextInner () |> Execution.bind (function
                | Done -> pullOuter env ct outerTail ()
                | Next(value, innerTail) -> Execution.ofValue(Next(value, pullInner env ct outerTail innerTail)))
        FlowStream(fun env ct -> pullOuter env ct (fun () -> outer env ct) ())

    /// <summary>Pairs values from two streams until either stream ends.</summary>
    /// <example><code>left |&gt; FlowStream.zip right</code></example>
    let zip (FlowStream right) (FlowStream left) =
        let rec loop leftNext rightNext () =
            leftNext () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(leftValue, leftTail) ->
                    rightNext () |> Execution.map (function
                        | Done -> Done
                        | Next(rightValue, rightTail) -> Next((leftValue, rightValue), loop leftTail rightTail)))
        FlowStream(fun env ct -> loop (fun () -> left env ct) (fun () -> right env ct) ())

    /// <summary>Folds a stream into one value inside Flow.</summary>
    /// <example><code>stream |&gt; FlowStream.runFold (+) 0</code></example>
    let runFold folder initial (FlowStream op) : Flow<'env, 'error, 'state> =
        let rec loop state next =
            next () |> Execution.bind (function Done -> Execution.ofValue state | Next(value, tail) -> loop (folder state value) tail)
        Flow(fun env ct -> loop initial (fun () -> op env ct))

    /// <summary>Collects all emitted values into a list.</summary>
    /// <example><code>stream |&gt; FlowStream.runCollect</code></example>
    let runCollect stream = runFold (fun values value -> value :: values) [] stream |> Flow.map List.rev

    /// <summary>Consumes a stream and ignores its values.</summary>
    /// <example><code>stream |&gt; FlowStream.runDrain</code></example>
    let runDrain stream = runFold (fun () _ -> ()) () stream

    /// <summary>Runs an effectful action for every stream value.</summary>
    /// <example><code>stream |&gt; FlowStream.runForEachFlow save</code></example>
    let runForEachFlow action (FlowStream op) : Flow<'env, 'error, unit> =
        let rec loop env ct next =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue ()
                | Next(value, tail) -> Flow.invoke (action value) env ct |> Execution.bind (fun () -> loop env ct tail))
        Flow(fun env ct -> loop env ct (fun () -> op env ct))
