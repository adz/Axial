namespace Axial.Flow

open System

/// <summary>
/// Represents a stateful schedule that can decide whether to continue and how long to delay.
/// </summary>
type Schedule<'env, 'input, 'output> =
    internal
    | Schedule of ('input -> int -> Flow<'env, unit, 'output option * TimeSpan>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Schedule =
    // A schedule that fails to evaluate is a bug in the schedule, not a typed domain error of the
    // workflow it drives, so its unit failure becomes a defect instead of a fabricated 'error value.
    let rec private dieOnScheduleFailure (cause: Cause<unit>) : Cause<'error> =
        match cause with
        | Cause.Fail () -> Cause.Die(InvalidOperationException "Schedule evaluation failed.")
        | Cause.Die ex -> Cause.Die ex
        | Cause.Interrupt -> Cause.Interrupt
        | Cause.Then(left, right) -> Cause.Then(dieOnScheduleFailure left, dieOnScheduleFailure right)
        | Cause.Both(left, right) -> Cause.Both(dieOnScheduleFailure left, dieOnScheduleFailure right)
        | Cause.Traced(inner, trace) -> Cause.Traced(dieOnScheduleFailure inner, trace)

    // TimeSpan.MaxValue is a static field get that Fable cannot compile, so the cap is built from its tick count.
    let private maxDelayTicks = Int64.MaxValue

    let private maxDelay = TimeSpan.FromTicks maxDelayTicks

    let private invokeSchedule op input attempt env ct : Execution<'output option * TimeSpan, 'error> =
        Execution.fold
            Execution.ofValue
            (dieOnScheduleFailure >> Execution.ofCause)
            (FlowInternal.invoke (op input attempt) env ct)

    /// <summary>Creates a schedule that recurs a fixed number of times.</summary>
    /// <param name="n">The maximum number of times to recur.</param>
    /// <returns>A schedule that recurs up to <paramref name="n"/> times, emitting the current attempt count (0 to n-1).</returns>
    /// <example>
    /// <code>
    /// let schedule = Schedule.recurs 3
    /// // Will run for attempts 0, 1, 2 and then stop.
    /// </code>
    /// </example>
    let recurs (n: int) : Schedule<'env, 'input, int> =
        Schedule(fun _ attempt ->
            if attempt < n then
                Flow.ok (Some attempt, TimeSpan.Zero)
            else
                Flow.ok (None, TimeSpan.Zero))

    /// <summary>Creates a schedule that recurs with a fixed delay between attempts.</summary>
    /// <param name="delay">The fixed time span to wait between each attempt.</param>
    /// <returns>A schedule that recurs indefinitely with the specified fixed delay, emitting the current attempt count.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="delay"/> is negative.</exception>
    /// <example>
    /// <code>
    /// let schedule = Schedule.spaced (TimeSpan.FromSeconds 1.0)
    /// </code>
    /// </example>
    let spaced (delay: TimeSpan) : Schedule<'env, 'input, int> =
        if delay < TimeSpan.Zero then
            invalidArg (nameof delay) "A spaced schedule requires a non-negative delay."

        Schedule(fun _ attempt ->
            Flow.ok (Some attempt, delay))

    /// <summary>Creates a schedule that recurs with exponential backoff.</summary>
    /// <param name="baseDelay">The initial delay for the first retry.</param>
    /// <returns>A schedule that recurs indefinitely, doubling the delay each time (baseDelay * 2^attempt) and capping at <see cref="P:System.TimeSpan.MaxValue"/> instead of overflowing.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="baseDelay"/> is negative.</exception>
    /// <example>
    /// <code>
    /// let schedule = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    /// // Delays: 100ms, 200ms, 400ms, 800ms...
    /// </code>
    /// </example>
    let exponential (baseDelay: TimeSpan) : Schedule<'env, 'input, TimeSpan> =
        if baseDelay < TimeSpan.Zero then
            invalidArg (nameof baseDelay) "Exponential backoff requires a non-negative base delay."

        Schedule(fun _ attempt ->
            let scaledTicks = float baseDelay.Ticks * Math.Pow(2.0, float attempt)

            let delay =
                if scaledTicks >= float maxDelayTicks then
                    maxDelay
                else
                    TimeSpan.FromTicks(int64 scaledTicks)

            Flow.ok (Some delay, delay))

    /// <summary>Adds jitter to a schedule's delay using a caller-supplied sample source.</summary>
    /// <param name="sample">A function returning a value in [0.0, 1.0), sampled once per attempt. Supply a deterministic function for reproducible schedules and tests.</param>
    /// <param name="schedule">The base schedule to which jitter will be applied.</param>
    /// <returns>A new schedule where each delay is multiplied by <c>sample () + 0.5</c>, giving a factor between 0.5 and 1.5, capped at <see cref="P:System.TimeSpan.MaxValue"/>.</returns>
    /// <example>
    /// <code>
    /// let schedule = Schedule.spaced (TimeSpan.FromSeconds 1.0) |> Schedule.jitteredWith (fun () -> 0.25)
    /// // Every delay becomes 750ms.
    /// </code>
    /// </example>
    let jitteredWith (sample: unit -> float) (Schedule op) : Schedule<'env, 'input, 'output> =
        Schedule(fun input attempt ->
            Flow.map (fun (out, (delay: TimeSpan)) ->
                let jitter = sample () + 0.5
                let scaledTicks = float delay.Ticks * jitter

                let jitteredDelay =
                    if scaledTicks >= float maxDelayTicks then
                        maxDelay
                    else
                        TimeSpan.FromTicks(max 0L (int64 scaledTicks))

                out, jitteredDelay
            ) (op input attempt))

    /// <summary>Adds random jitter to a schedule's delay.</summary>
    /// <param name="schedule">The base schedule to which jitter will be applied.</param>
    /// <returns>A new schedule where each delay is multiplied by a random factor between 0.5 and 1.5.</returns>
    /// <example>
    /// <code>
    /// let schedule = Schedule.spaced (TimeSpan.FromSeconds 1.0) |> Schedule.jittered
    /// </code>
    /// </example>
    let jittered (schedule: Schedule<'env, 'input, 'output>) : Schedule<'env, 'input, 'output> =
        let random = Random()
        jitteredWith random.NextDouble schedule

    /// <summary>Retries a failing flow according to the supplied schedule.</summary>
    /// <param name="schedule">The schedule that determines when and if to retry based on the error.</param>
    /// <param name="flow">The workflow to retry if it fails.</param>
    /// <returns>A flow that will retry the original flow according to the schedule until it succeeds or the schedule stops.</returns>
    /// <example>
    /// <code>
    /// let flakyWork = Flow.fail "oops"
    /// let retried = flakyWork |> Schedule.retry (Schedule.recurs 3)
    /// </code>
    /// </example>
    let retry
        (schedule: Schedule<'env, 'error, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        let (Schedule op) = schedule

        let rec loop attempt =
            Flow(fun env ct ->
                Execution.fold
                    (fun v -> Execution.ofValue v)
                    (fun cause ->
                        match cause with
                        | Cause.Fail e ->
                            Execution.bind
                                (fun (decision, delay) ->
                                    match decision with
                                    | Some _ ->
                                        Execution.bind
                                            (fun () -> FlowInternal.invoke (loop (attempt + 1)) env ct)
                                            (FlowInternal.invoke (Flow.Runtime.sleep delay) env ct)
                                    | None ->
                                        Execution.ofCause cause)
                                (Execution.mapError (fun () -> e) (FlowInternal.invoke (op e attempt) env ct))
                        | _ ->
                            Execution.ofCause cause)
                    (FlowInternal.invoke flow env ct))

        loop 0

    /// <summary>Repeats a successful flow according to the supplied schedule.</summary>
    /// <param name="schedule">The schedule that determines when and if to repeat based on the successful value.</param>
    /// <param name="flow">The workflow to repeat if it succeeds.</param>
    /// <returns>A flow that repeats the original flow according to the schedule, returning the last successful value when it stops.</returns>
    /// <example>
    /// <code>
    /// let work = Flow.ok 42
    /// let repeated = work |> Schedule.repeat (Schedule.recurs 5)
    /// </code>
    /// </example>
    let repeat
        (schedule: Schedule<'env, 'value, 'output>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        let (Schedule op) = schedule

        let rec loop attempt lastValue =
            Flow(fun env ct ->
                Execution.bind
                    (fun (decision, (delay: TimeSpan)) ->
                        match decision with
                        | Some _ ->
                            Execution.bind
                                (fun () ->
                                    Execution.bind
                                        (fun nextValue -> FlowInternal.invoke (loop (attempt + 1) nextValue) env ct)
                                        (FlowInternal.invoke flow env ct))
                                (FlowInternal.invoke (Flow.Runtime.sleep delay) env ct)
                        | None ->
                            Execution.ofValue lastValue)
                    (invokeSchedule op lastValue attempt env ct))

        flow |> Flow.bind (loop 0)
