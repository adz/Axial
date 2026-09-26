namespace Axial

open System
open System.Threading

/// <summary>
/// A single step of a <see cref="T:Axial.FlowStream`3" />: either the stream is exhausted, or it produced
/// one value plus a thunk that continues the stream.
/// </summary>
/// <typeparam name="value">The type of the success values in the stream.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
type StreamStep<'value, 'error> =
    | Done
    | Next of 'value * (unit -> Execution<StreamStep<'value, 'error>, 'error>)

    /// <summary>The step kind, rendered without reflection so it stays safe under NativeAOT.</summary>
    override this.ToString() =
        match this with
        | Done -> "Done"
        | Next(value, _) -> $"Next({OutcomeText.value (box value)})"

/// <summary>
/// Represents a cold stream of values that requires an environment, can fail with a typed error,
/// and supports backpressure.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success values in the stream.</typeparam>
type FlowStream<'env, 'error, 'value> =
    FlowStream of ('env -> CancellationToken -> Execution<StreamStep<'value, 'error>, 'error>)

/// <summary>A stream with no environment requirement and no typed failure.</summary>
type FlowStream<'value> = FlowStream<unit, Never, 'value>

/// <summary>A stream with no environment requirement.</summary>
type FlowStream<'error, 'value> = FlowStream<unit, 'error, 'value>

/// An upstream event moved through an operator's internal queue.
[<RequireQualifiedAccess>]
type internal Pumped<'value, 'error> =
    | Item of 'value
    | Ended
    | Failed of Cause<'error>

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

    /// <summary>Creates a stream that takes values from a queue or hub subscription until it is shut down and drained.</summary>
    /// <remarks>
    /// Shutdown is the normal end of the stream, not a failure: after <c>Dequeue.shutdown</c> or <c>Hub.shutdown</c> the
    /// stream emits the remaining backlog and then completes. Each pull suspends while the queue is empty.
    /// Interrupting a pull leaves any value it would have received in the queue.
    /// </remarks>
    /// <example>
    /// <code>
    /// flow {
    ///     let! (jobs: Queue&lt;string&gt;) = Queue.bounded 8
    ///     do! jobs |&gt; Queue.offerAll [ "a"; "b" ] |&gt; Flow.ignore
    ///     do! Dequeue.shutdown jobs
    ///     return! jobs |&gt; FlowStream.fromDequeue |&gt; FlowStream.runCollect
    /// }
    /// </code>
    /// </example>
    let fromDequeue (queue: Dequeue<'value>) : FlowStream<'env, 'error, 'value> =
        let rec pull cancellationToken () : Execution<StreamStep<'value, 'error>, 'error> =
            QueueCore.take
                queue
                (fun () -> Execution.ofValue Done)
                (fun value -> Execution.ofValue(Next(value, pull cancellationToken)))
                cancellationToken

        FlowStream(fun _ cancellationToken -> pull cancellationToken ())

    /// <summary>Creates a stream from a synchronous sequence of values.</summary>
    /// <param name="values">The sequence of values to be emitted by the stream.</param>
    /// <returns>A <see cref="T:AxialStream`3"/> that yields each value from the sequence.</returns>
    /// <example>
    /// <code>
    /// FlowStream.fromSeq [1..10]
    /// |> FlowStream.runCollect
    /// |> Flow.run ()
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
    /// FlowStream.fromSeq ["a"; "b"; "c"]
    /// |> FlowStream.runForEach (printfn "%s")
    /// |> Flow.run ()
    /// </code>
    /// </example>
    let runForEach
        (action: 'value -> unit)
        (FlowStream op)
        : Flow<'env, 'error, unit> =
        Flow(fun env cancellationToken ->
            Execution.loop (fun () -> op env cancellationToken) (fun next ->
                next ()
                |> Execution.map (function
                    | Done -> Platform.Break ()
                    | Next(value, tail) ->
                        action value
                        Platform.Continue tail)))
        |> Flow.scoped

    /// <summary>Transforms the successful values of a stream using the provided function.</summary>
    /// <param name="f">The function to transform each value.</param>
    /// <param name="stream">The stream whose values should be transformed.</param>
    /// <returns>A new stream that yields transformed values.</returns>
    /// <example>
    /// <code>
    /// let stream = FlowStream.fromSeq [1; 2; 3] |> FlowStream.map (fun n -> n * 2)
    /// </code>
    /// </example>
    let map (f: 'v -> 'w) (stream: FlowStream<'env, 'error, 'v>) : FlowStream<'env, 'error, 'w> =
        let (FlowStream op) = stream
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
    let mapError (mapper: 'error -> 'nextError) (stream: FlowStream<'env, 'error, 'value>) : FlowStream<'env, 'nextError, 'value> =
        let (FlowStream op) = stream
        let rec loop next () =
            next ()
            |> Execution.mapError mapper
            |> Execution.map (function Done -> Done | Next(value, tail) -> Next(value, loop tail))
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Keeps values that satisfy a predicate.</summary>
    /// <example><code>stream |&gt; FlowStream.filter (fun value -&gt; value &gt; 0)</code></example>
    let filter predicate stream =
        let (FlowStream op) = stream
        let rec loop next () =
            Execution.loop next (fun next ->
                next () |> Execution.map (function
                    | Done -> Platform.Break Done
                    | Next(value, tail) when predicate value -> Platform.Break(Next(value, loop tail))
                    | Next(_, tail) -> Platform.Continue tail))
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Maps and filters values in one operation.</summary>
    /// <example><code>stream |&gt; FlowStream.choose id</code></example>
    let choose chooser stream =
        let (FlowStream op) = stream
        let rec loop next () =
            Execution.loop next (fun next ->
                next () |> Execution.map (function
                    | Done -> Platform.Break Done
                    | Next(value, tail) ->
                        match chooser value with
                        | Some selected -> Platform.Break(Next(selected, loop tail))
                        | None -> Platform.Continue tail))
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Runs an effect for each value before emitting the original value.</summary>
    /// <example><code>stream |&gt; FlowStream.tapFlow logValue</code></example>
    let tapFlow action stream =
        let (FlowStream op) = stream
        let rec loop env ct next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) ->
                    Flow.invoke (action value) env ct
                    |> Execution.map (fun () -> Next(value, loop env ct tail)))
        FlowStream(fun env ct -> loop env ct (fun () -> op env ct) ())

    /// <summary>Transforms every value with a Flow effect.</summary>
    /// <example><code>ids |&gt; FlowStream.mapFlow load</code></example>
    let mapFlow mapper stream =
        let (FlowStream op) = stream
        let rec loop env ct next () =
            next () |> Execution.bind (function
                | Done -> Execution.ofValue Done
                | Next(value, tail) -> Flow.invoke (mapper value) env ct |> Execution.map (fun mapped -> Next(mapped, loop env ct tail)))
        FlowStream(fun env ct -> loop env ct (fun () -> op env ct) ())

    /// <summary>Maps values with a continuously replenished, bounded set of child fibers.</summary>
    /// <remarks>
    /// Results are emitted in completion order. After each result is consumed, the next upstream value starts,
    /// so there are no strict batch barriers. At most the configured number of mappings are active or retained.
    /// The first failure observed stops the stream; the terminal consumer's child scope interrupts and awaits
    /// all remaining mappings before returning.
    /// </remarks>
    let mapFlowPar (parallelism: Parallelism) mapper stream =
        let (FlowStream op) = stream
        let bound = Parallelism.value parallelism

        let removeAt index values =
            values
            |> List.indexed
            |> List.choose (fun (current, value) -> if current = index then None else Some value)

        let rec fill env ct remaining upstreamDone next active =
            if remaining = 0 || upstreamDone then
                pullMapped env ct upstreamDone next active ()
            else
                next ()
                |> Execution.bind (function
                    | Done -> pullMapped env ct true next active ()
                    | Next(value, tail) ->
                        Flow.invoke (Flow.fork (mapper value)) env ct
                        |> Execution.bind (fun fiber -> fill env ct (remaining - 1) false tail (fiber :: active)))

        and pullMapped env ct upstreamDone next active () =
            match active with
            | [] -> Execution.ofValue Done
            | fibers ->
                Platform.awaitAnyExitTaskAsSuccess (fibers |> List.map _.ExitTask) ct
                |> Execution.bind (fun (index, exit) ->
                    let remaining = removeAt index fibers
                    match exit with
                    | Exit.Success mapped ->
                        Execution.ofValue(Next(mapped, fun () -> fill env ct (bound - remaining.Length) upstreamDone next remaining))
                    | Exit.Failure cause -> Execution.ofCause cause)

        FlowStream(fun env ct -> fill env ct bound false (fun () -> op env ct) [])

    // Moves upstream values into a one-slot queue from a producer fiber, so an operator can wait for "the next value
    // or a timer" instead of blocking on a pull. The producer is forked into the consumer's scope, so it stops when
    // the consumer finishes or stops early; a one-slot queue keeps upstream back-pressure.
    let private pump
        (op: 'env -> CancellationToken -> Execution<StreamStep<'value, 'error>, 'error>)
        (env: 'env)
        (ct: CancellationToken)
        : Execution<Queue<Pumped<'value, 'error>>, 'error> =
        let queue : Queue<Pumped<'value, 'error>> = QueueCore.create BackPressure (Some 1)

        let offer item pct =
            Flow.invoke (Queue.offer item queue) env pct

        let producer : Flow<'env, Never, unit> =
            Flow(fun env pct ->
                Execution.loop (fun () -> op env pct) (fun next ->
                    next ()
                    |> Execution.fold
                        (function
                            | Done -> offer Pumped.Ended pct |> Execution.map (fun _ -> Platform.Break())
                            | Next(value, tail) -> offer (Pumped.Item value) pct |> Execution.map (fun _ -> Platform.Continue tail))
                        (fun cause -> offer (Pumped.Failed cause) pct |> Execution.map (fun _ -> Platform.Break()))))

        Flow.invoke (Flow.forkDetached producer) env ct |> Execution.map (fun _ -> queue)

    let private takePumped queue env ct : Execution<Pumped<'value, 'error>, 'error> =
        Flow.invoke (Queue.take queue) env ct

    // The next pumped event, or None if none arrives within `timeout`. Interrupting the losing take never loses a value.
    let private takeWithin queue (timeout: TimeSpan) env ct : Execution<Pumped<'value, 'error> option, 'error> =
        if timeout <= TimeSpan.Zero then
            Flow.invoke (Queue.poll queue) env ct
        else
            Flow.invoke (Flow.race (Queue.take queue |> Flow.map Some) (Flow.sleep timeout |> Flow.map (fun () -> None))) env ct

    let private finished () : Execution<StreamStep<'value, 'error>, 'error> = Execution.ofValue Done

    /// <summary>Groups values into lists of at most <paramref name="size" />, emitting early when <paramref name="window" /> passes.</summary>
    /// <remarks>
    /// A group starts with the next value and is emitted when it holds <paramref name="size" /> values or when
    /// <paramref name="window" /> has passed since its first value, whichever comes first. No empty group is emitted.
    /// When upstream ends or fails, the partial group is emitted first. Use it to batch writes or progress updates
    /// without waiting indefinitely for a full batch.
    /// </remarks>
    /// <example><code>events |&gt; FlowStream.groupedWithin 100 (TimeSpan.FromSeconds 1.0)</code></example>
    let groupedWithin (size: int) (window: TimeSpan) stream : FlowStream<'env, 'error, 'value list> =
        if size <= 0 then invalidArg (nameof size) "Group size must be positive."
        if window <= TimeSpan.Zero then invalidArg (nameof window) "The window must be positive."
        let (FlowStream op) = stream

        FlowStream(fun env ct ->
            pump op env ct
            |> Execution.bind (fun queue ->
                let rec group () =
                    takePumped queue env ct
                    |> Execution.bind (function
                        | Pumped.Ended -> finished ()
                        | Pumped.Failed cause -> Execution.ofCause cause
                        | Pumped.Item first ->
                            let deadline = Platform.monotonicNow () + window

                            Execution.loop ([ first ], 1) (fun (values, count) ->
                                if count >= size then
                                    Execution.ofValue (Platform.Break(Next(List.rev values, group)))
                                else
                                    takeWithin queue (deadline - Platform.monotonicNow ()) env ct
                                    |> Execution.map (function
                                        | None -> Platform.Break(Next(List.rev values, group))
                                        | Some(Pumped.Item value) -> Platform.Continue(value :: values, count + 1)
                                        | Some Pumped.Ended -> Platform.Break(Next(List.rev values, finished))
                                        | Some(Pumped.Failed cause) -> Platform.Break(Next(List.rev values, fun () -> Execution.ofCause cause)))))

                group ()))

    /// <summary>Emits a value only once <paramref name="quiet" /> passes without a newer one.</summary>
    /// <remarks>
    /// Each value replaces the pending one and restarts the wait, so a burst produces only its last value. The
    /// pending value is emitted when upstream ends, and before a failure is propagated. Use it for search-as-you-type
    /// input.
    /// </remarks>
    /// <example><code>keystrokes |&gt; FlowStream.debounce (TimeSpan.FromMilliseconds 300.0)</code></example>
    let debounce (quiet: TimeSpan) stream : FlowStream<'env, 'error, 'value> =
        if quiet <= TimeSpan.Zero then invalidArg (nameof quiet) "The quiet period must be positive."
        let (FlowStream op) = stream

        FlowStream(fun env ct ->
            pump op env ct
            |> Execution.bind (fun queue ->
                let rec next () =
                    takePumped queue env ct
                    |> Execution.bind (function
                        | Pumped.Ended -> finished ()
                        | Pumped.Failed cause -> Execution.ofCause cause
                        | Pumped.Item value ->
                            Execution.loop value (fun pending ->
                                takeWithin queue quiet env ct
                                |> Execution.map (function
                                    | None -> Platform.Break(Next(pending, next))
                                    | Some(Pumped.Item newer) -> Platform.Continue newer
                                    | Some Pumped.Ended -> Platform.Break(Next(pending, finished))
                                    | Some(Pumped.Failed cause) -> Platform.Break(Next(pending, fun () -> Execution.ofCause cause)))))

                next ()))

    /// <summary>Emits at most one value per <paramref name="interval" />, keeping the latest value when faster.</summary>
    /// <remarks>
    /// The first value is emitted immediately. Values that arrive within <paramref name="interval" /> of the last
    /// emission replace each other, and the latest is emitted when the interval ends. The pending value is emitted
    /// when upstream ends, and before a failure is propagated. Use it for progress reporting, where only the
    /// current state matters.
    /// </remarks>
    /// <example><code>progress |&gt; FlowStream.throttle (TimeSpan.FromMilliseconds 100.0)</code></example>
    let throttle (interval: TimeSpan) stream : FlowStream<'env, 'error, 'value> =
        if interval <= TimeSpan.Zero then invalidArg (nameof interval) "The interval must be positive."
        let (FlowStream op) = stream

        FlowStream(fun env ct ->
            pump op env ct
            |> Execution.bind (fun queue ->
                let rec next (lastEmitted: TimeSpan option) () =
                    takePumped queue env ct
                    |> Execution.bind (function
                        | Pumped.Ended -> finished ()
                        | Pumped.Failed cause -> Execution.ofCause cause
                        | Pumped.Item value ->
                            let now = Platform.monotonicNow ()

                            match lastEmitted with
                            | Some emitted when now - emitted < interval ->
                                let deadline = emitted + interval

                                Execution.loop value (fun pending ->
                                    takeWithin queue (deadline - Platform.monotonicNow ()) env ct
                                    |> Execution.map (function
                                        | None -> Platform.Break(Next(pending, next (Some(Platform.monotonicNow ()))))
                                        | Some(Pumped.Item newer) -> Platform.Continue newer
                                        | Some Pumped.Ended -> Platform.Break(Next(pending, finished))
                                        | Some(Pumped.Failed cause) -> Platform.Break(Next(pending, fun () -> Execution.ofCause cause))))
                            | _ -> Execution.ofValue (Next(value, next (Some now))))

                next None ()))

    /// <summary>Maps each value to a flow, interrupting the previous flow when a newer value arrives.</summary>
    /// <remarks>
    /// Only the latest value's flow is kept: when upstream produces a new value while a flow is running, that flow is
    /// interrupted (and its cleanup awaited) and the new value's flow starts. Results are emitted as flows complete.
    /// When upstream ends, the running flow is allowed to finish. The first failure stops the stream. Use it for
    /// search-as-you-type and autocomplete, where a result for stale input is worthless.
    /// </remarks>
    /// <example><code>queries |&gt; FlowStream.debounce (TimeSpan.FromMilliseconds 200.0) |&gt; FlowStream.switchMapFlow search</code></example>
    let switchMapFlow (mapper: 'value -> Flow<'env, 'error, 'next>) stream : FlowStream<'env, 'error, 'next> =
        let (FlowStream op) = stream

        FlowStream(fun env ct ->
            pump op env ct
            |> Execution.bind (fun queue ->
                let start value = Flow.invoke (Flow.fork (mapper value)) env ct

                let rec idle () =
                    takePumped queue env ct
                    |> Execution.bind (function
                        | Pumped.Ended -> finished ()
                        | Pumped.Failed cause -> Execution.ofCause cause
                        | Pumped.Item value -> start value |> Execution.bind running)

                and running fiber =
                    Execution.loop fiber (fun fiber ->
                        let nextEvent = Queue.take queue |> Flow.map Choice1Of2
                        let completion = Fiber.await fiber |> Flow.map Choice2Of2

                        Flow.invoke (Flow.race nextEvent completion) env ct
                        |> Execution.bind (function
                            | Choice1Of2(Pumped.Item value) ->
                                Flow.invoke (Fiber.interrupt fiber) env ct
                                |> Execution.bind (fun _ -> start value)
                                |> Execution.map Platform.Continue
                            | Choice1Of2 Pumped.Ended ->
                                Flow.invoke (Fiber.await fiber) env ct
                                |> Execution.bind (function
                                    | Exit.Success result -> Execution.ofValue (Platform.Break(Next(result, finished)))
                                    | Exit.Failure cause -> Execution.ofCause cause)
                            | Choice1Of2(Pumped.Failed cause) ->
                                Flow.invoke (Fiber.interrupt fiber) env ct |> Execution.bind (fun _ -> Execution.ofCause cause)
                            | Choice2Of2(Exit.Success result) -> Execution.ofValue (Platform.Break(Next(result, idle)))
                            | Choice2Of2(Exit.Failure cause) -> Execution.ofCause cause))

                idle ()))

    /// <summary>Groups consecutive values into non-empty lists of at most <paramref name="size"/> elements.</summary>
    /// <remarks>The operator pulls and retains at most <paramref name="size"/> upstream values for each emitted list.</remarks>
    let chunkBySize size stream =
        let (FlowStream op) = stream
        if size <= 0 then invalidArg (nameof size) "Chunk size must be positive."

        let rec pullChunk next remaining values () =
            if remaining = 0 then
                Execution.ofValue(Next(List.rev values, fun () -> pullChunk next size [] ()))
            else
                next ()
                |> Execution.bind (function
                    | Done when values.IsEmpty -> Execution.ofValue Done
                    | Done -> Execution.ofValue(Next(List.rev values, fun () -> Execution.ofValue Done))
                    | Next(value, tail) -> pullChunk tail (remaining - 1) (value :: values) ())

        FlowStream(fun env ct -> pullChunk (fun () -> op env ct) size [] ())

    /// <summary>Emits at most <paramref name="count"/> values.</summary>
    /// <example><code>stream |&gt; FlowStream.take 10</code></example>
    let take count stream =
        let (FlowStream op) = stream
        if count < 0 then invalidArg (nameof count) "Count cannot be negative."
        let rec loop remaining next () =
            if remaining = 0 then Execution.ofValue Done else
            next () |> Execution.map (function Done -> Done | Next(value, tail) -> Next(value, loop (remaining - 1) tail))
        FlowStream(fun env ct -> loop count (fun () -> op env ct) ())

    /// <summary>Skips the first <paramref name="count"/> values.</summary>
    /// <example><code>stream |&gt; FlowStream.skip 10</code></example>
    let skip count stream =
        let (FlowStream op) = stream
        if count < 0 then invalidArg (nameof count) "Count cannot be negative."
        let drop next =
            Execution.loop (count, next) (fun (remaining, next) ->
                next () |> Execution.map (function
                    | Done -> Platform.Break Done
                    | Next(_, tail) when remaining > 0 -> Platform.Continue(remaining - 1, tail)
                    | Next(value, tail) -> Platform.Break(Next(value, tail))))
        FlowStream(fun env ct -> drop (fun () -> op env ct))

    /// <summary>Emits values while a predicate remains true.</summary>
    /// <example><code>stream |&gt; FlowStream.takeWhile (fun value -&gt; value &lt; 100)</code></example>
    let takeWhile predicate stream =
        let (FlowStream op) = stream
        let rec loop next () =
            next () |> Execution.map (function
                | Next(value, tail) when predicate value -> Next(value, loop tail)
                | _ -> Done)
        FlowStream(fun env ct -> loop (fun () -> op env ct) ())

    /// <summary>Skips values while a predicate remains true.</summary>
    /// <example><code>stream |&gt; FlowStream.skipWhile String.IsNullOrEmpty</code></example>
    let skipWhile predicate stream =
        let (FlowStream op) = stream
        let dropping next =
            Execution.loop next (fun next ->
                next () |> Execution.map (function
                    | Done -> Platform.Break Done
                    | Next(value, tail) when predicate value -> Platform.Continue tail
                    | Next(value, tail) -> Platform.Break(Next(value, tail))))
        FlowStream(fun env ct -> dropping (fun () -> op env ct))

    /// <summary>Emits each value paired with its zero-based index.</summary>
    /// <example><code>stream |&gt; FlowStream.indexed</code></example>
    let indexed stream =
        let (FlowStream op) = stream
        let rec loop index next () =
            next () |> Execution.map (function Done -> Done | Next(value, tail) -> Next((index, value), loop (index + 1) tail))
        FlowStream(fun env ct -> loop 0 (fun () -> op env ct) ())

    /// <summary>Emits successive accumulator states.</summary>
    /// <example><code>stream |&gt; FlowStream.scan (+) 0</code></example>
    let scan folder initial stream =
        let (FlowStream op) = stream
        let rec loop state next () =
            next () |> Execution.map (function
                | Done -> Done
                | Next(value, tail) -> let nextState = folder state value in Next(nextState, loop nextState tail))
        FlowStream(fun env ct -> loop initial (fun () -> op env ct) ())

    /// <summary>Suppresses consecutive duplicate values according to a projection.</summary>
    /// <example><code>stream |&gt; FlowStream.distinctUntilChangedBy id</code></example>
    let distinctUntilChangedBy projection stream =
        let (FlowStream op) = stream
        let rec loop previous next () =
            Execution.loop next (fun next ->
                next () |> Execution.map (function
                    | Done -> Platform.Break Done
                    | Next(value, tail) ->
                        let key = projection value
                        if previous = Some key then Platform.Continue tail
                        else Platform.Break(Next(value, loop (Some key) tail))))
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
    let collect mapper stream =
        let (FlowStream outer) = stream
        // The loop state is the outer continuation plus the current inner stream, if any. Empty inner streams
        // and outer values are passed over inside the loop rather than by recursion.
        let rec pull env ct outerNext innerNext () =
            Execution.loop (outerNext, innerNext) (fun (outerNext, innerNext) ->
                match innerNext with
                | Some next ->
                    next () |> Execution.map (function
                        | Done -> Platform.Continue(outerNext, None)
                        | Next(value, innerTail) -> Platform.Break(Next(value, pull env ct outerNext (Some innerTail))))
                | None ->
                    outerNext () |> Execution.map (function
                        | Done -> Platform.Break Done
                        | Next(value, outerTail) ->
                            let (FlowStream inner) = mapper value
                            Platform.Continue(outerTail, Some(fun () -> inner env ct))))
        FlowStream(fun env ct -> pull env ct (fun () -> outer env ct) None ())

    /// <summary>Pairs values from two streams until either stream ends.</summary>
    /// <example><code>left |&gt; FlowStream.zip right</code></example>
    let zip rightStream leftStream =
        let (FlowStream right) = rightStream
        let (FlowStream left) = leftStream
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
    let runFold folder initial (stream: FlowStream<'env, 'error, 'value>) : Flow<'env, 'error, 'state> =
        let (FlowStream op) = stream
        Flow(fun env ct ->
            Execution.loop (initial, fun () -> op env ct) (fun (state, next) ->
                next ()
                |> Execution.map (function
                    | Done -> Platform.Break state
                    | Next(value, tail) -> Platform.Continue(folder state value, tail))))
        |> Flow.scoped

    /// <summary>Collects all emitted values into a list.</summary>
    /// <example><code>stream |&gt; FlowStream.runCollect</code></example>
    let runCollect stream = runFold (fun values value -> value :: values) [] stream |> Flow.map List.rev

    /// <summary>Consumes a stream and ignores its values.</summary>
    /// <example><code>stream |&gt; FlowStream.runDrain</code></example>
    let runDrain stream = runFold (fun () _ -> ()) () stream

    /// <summary>Runs an effectful action for every stream value.</summary>
    /// <example><code>stream |&gt; FlowStream.runForEachFlow save</code></example>
    let runForEachFlow action (stream: FlowStream<'env, 'error, 'value>) : Flow<'env, 'error, unit> =
        let (FlowStream op) = stream
        Flow(fun env ct ->
            Execution.loop (fun () -> op env ct) (fun next ->
                next ()
                |> Execution.bind (function
                    | Done -> Execution.ofValue (Platform.Break ())
                    | Next(value, tail) -> Flow.invoke (action value) env ct |> Execution.map (fun () -> Platform.Continue tail))))
        |> Flow.scoped
