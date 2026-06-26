namespace Axial.Flow

#if !FABLE_COMPILER
open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents a stateful schedule that can decide whether to continue and how long to delay.
/// </summary>
type Schedule<'env, 'input, 'output> =
    private
    | Schedule of ('input -> int -> Flow<'env, unit, 'output option * TimeSpan>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Schedule =
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
    /// <example>
    /// <code>
    /// let schedule = Schedule.spaced (TimeSpan.FromSeconds 1.0)
    /// </code>
    /// </example>
    let spaced (delay: TimeSpan) : Schedule<'env, 'input, int> =
        Schedule(fun _ attempt ->
            Flow.ok (Some attempt, delay))

    /// <summary>Creates a schedule that recurs with exponential backoff.</summary>
    /// <param name="baseDelay">The initial delay for the first retry.</param>
    /// <returns>A schedule that recurs indefinitely, doubling the delay each time (baseDelay * 2^attempt).</returns>
    /// <example>
    /// <code>
    /// let schedule = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    /// // Delays: 100ms, 200ms, 400ms, 800ms...
    /// </code>
    /// </example>
    let exponential (baseDelay: TimeSpan) : Schedule<'env, 'input, TimeSpan> =
        Schedule(fun _ attempt ->
            let delay = TimeSpan.FromTicks(baseDelay.Ticks * int64 (Math.Pow(2.0, float attempt)))
            Flow.ok (Some delay, delay))

    /// <summary>Adds random jitter to a schedule's delay.</summary>
    /// <param name="schedule">The base schedule to which jitter will be applied.</param>
    /// <returns>A new schedule where each delay is multiplied by a random factor between 0.5 and 1.5.</returns>
    /// <example>
    /// <code>
    /// let schedule = Schedule.spaced (TimeSpan.FromSeconds 1.0) |> Schedule.jittered
    /// </code>
    /// </example>
    let jittered (Schedule op) : Schedule<'env, 'input, 'output> =
        let random = Random()
        Schedule(fun input attempt ->
            Flow.map (fun (out, (delay: TimeSpan)) ->
                let jitter = random.NextDouble() + 0.5
                let jitteredDelay = TimeSpan.FromTicks(int64 (float delay.Ticks * jitter))
                out, jitteredDelay
            ) (op input attempt))

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
                                            (Execution.mapError (fun () -> e) (FlowInternal.invoke (Flow.Runtime.sleep delay) env ct))
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
                                (Execution.mapError (fun () -> Unchecked.defaultof<'error>) (FlowInternal.invoke (Flow.Runtime.sleep delay) env ct))
                        | None ->
                            Execution.ofValue lastValue)
                    (Execution.mapError (fun () -> Unchecked.defaultof<'error>) (FlowInternal.invoke (op lastValue attempt) env ct)))

        flow |> Flow.bind (loop 0)
#endif
