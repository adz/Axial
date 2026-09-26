namespace Axial

open System

/// What a schedule knows when it decides whether to recur. Timestamps come from the runtime's time source, so
/// only differences between them are meaningful.
type internal ScheduleContext =
    {
        /// The number of decisions made before this one, starting at 0.
        Attempt: int
        /// When the first execution of the driven flow began.
        LoopStarted: TimeSpan
        /// When the execution that just finished began.
        ExecutionStarted: TimeSpan
        /// When the execution that just finished ended.
        ExecutionEnded: TimeSpan
    }

module internal ScheduleContext =
    /// A context with no timing information, for decisions that depend only on the attempt number.
    let ofAttempt attempt =
        { Attempt = attempt
          LoopStarted = TimeSpan.Zero
          ExecutionStarted = TimeSpan.Zero
          ExecutionEnded = TimeSpan.Zero }

/// <summary>
/// Decides, after each execution of a flow, whether to run it again and how long to wait first.
/// </summary>
/// <remarks>A schedule value holds no state, so the same value can drive any number of independent runs.</remarks>
type Schedule<'env, 'input, 'output> =
    internal
    | Schedule of ('input -> ScheduleContext -> Flow<'env, unit, 'output option * TimeSpan>)

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

    // Schedule compiles before the Flow module so that Flow.retry, Flow.repeat, and Flow.supervise can take a
    // schedule; these are the few Flow combinators the schedule constructors need.
    let private ok (value: 'value) : Flow<'env, 'error, 'value> = Flow(fun _ _ -> Execution.ofValue value)

    let private map (mapper: 'value -> 'next) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'next> =
        Flow(fun env ct -> FlowInternal.invoke flow env ct |> Execution.map mapper)

    let private bind
        (binder: 'value -> Flow<'env, 'error, 'next>)
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'next> =
        Flow(fun env ct -> FlowInternal.invoke flow env ct |> Execution.bind (fun value -> FlowInternal.invoke (binder value) env ct))

    /// Evaluates one decision of a schedule: whether to recur and after what delay. A schedule's own failure is a
    /// defect, never a typed error of the flow it drives.
    let internal decide
        (Schedule op: Schedule<'env, 'input, 'output>)
        (input: 'input)
        (context: ScheduleContext)
        (env: 'env)
        ct
        : Execution<'output option * TimeSpan, 'error> =
        Execution.fold
            Execution.ofValue
            (dieOnScheduleFailure >> Execution.ofCause)
            (FlowInternal.invoke (op input context) env ct)

    /// <summary>Creates a schedule that recurs a fixed number of times.</summary>
    /// <remarks>
    /// <paramref name="n"/> counts the schedule's own decisions, not the total number of flow executions:
    /// <c>Flow.retry</c> and <c>Flow.repeat</c> always run the source flow once before consulting the
    /// schedule at all, so <c>recurs 3</c> means 3 additional retries/repeats on top of that one free attempt —
    /// 4 executions in total, not 3. A <c>Schedule</c> value carries no state of its own (the attempt count lives
    /// in the <c>retry</c>/<c>repeat</c> call), so the same schedule value is safe to reuse across independent runs.
    /// </remarks>
    /// <param name="n">The maximum number of additional times to recur, on top of the source flow's one free
    /// initial attempt.</param>
    /// <returns>A schedule that recurs up to <paramref name="n"/> times, emitting the current attempt count (0 to n-1).</returns>
    /// <example>
    /// <code>
    /// let retryThreeTimes () = Schedule.recurs 3
    /// // Flow.retry runs the source flow once for free, then consults the schedule at
    /// // attempts 0, 1, 2 (three retries) before giving up: 4 executions in total.
    /// </code>
    /// </example>
    let recurs (n: int) : Schedule<'env, 'input, int> =
        Schedule(fun _ context ->
            if context.Attempt < n then
                ok (Some context.Attempt, TimeSpan.Zero)
            else
                ok (None, TimeSpan.Zero))

    /// <summary>Creates a schedule that recurs with a fixed delay between attempts.</summary>
    /// <param name="delay">The fixed time span to wait between each attempt.</param>
    /// <returns>A schedule that recurs indefinitely with the specified fixed delay, emitting the current attempt count.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="delay"/> is negative.</exception>
    /// <example>
    /// <code>
    /// let everySecond () = Schedule.spaced (TimeSpan.FromSeconds 1.0)
    /// </code>
    /// </example>
    let spaced (delay: TimeSpan) : Schedule<'env, 'input, int> =
        if delay < TimeSpan.Zero then
            invalidArg (nameof delay) "A spaced schedule requires a non-negative delay."

        Schedule(fun _ context ->
            ok (Some context.Attempt, delay))

    /// <summary>Creates a schedule that recurs with exponential backoff.</summary>
    /// <param name="baseDelay">The initial delay for the first retry.</param>
    /// <returns>A schedule that recurs indefinitely, doubling the delay each time (baseDelay * 2^attempt) and capping at <see cref="P:System.TimeSpan.MaxValue"/> instead of overflowing.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="baseDelay"/> is negative.</exception>
    /// <example>
    /// <code>
    /// let backoff () = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    /// // Delays: 100ms, 200ms, 400ms, 800ms...
    /// </code>
    /// </example>
    let exponential (baseDelay: TimeSpan) : Schedule<'env, 'input, TimeSpan> =
        if baseDelay < TimeSpan.Zero then
            invalidArg (nameof baseDelay) "Exponential backoff requires a non-negative base delay."

        Schedule(fun _ context ->
            let scaledTicks = float baseDelay.Ticks * Math.Pow(2.0, float context.Attempt)

            let delay =
                if scaledTicks >= float maxDelayTicks then
                    maxDelay
                else
                    TimeSpan.FromTicks(int64 scaledTicks)

            ok (Some delay, delay))

    /// <summary>Adds jitter to a schedule's delay using a caller-supplied sample source.</summary>
    /// <remarks>
    /// <paramref name="sample"/> is not validated: a value outside [0.0, 1.0) is not rejected, it just produces a
    /// jitter factor outside the documented 0.5–1.5 range. The result is still always a valid, non-negative
    /// <c>TimeSpan</c> — negative factors clamp to <see cref="P:System.TimeSpan.Zero"/> and overflowing ones clamp
    /// to <see cref="P:System.TimeSpan.MaxValue"/>, the same as the base schedule's own overflow handling. This is
    /// deliberate: <c>jitteredWith</c> never throws for a badly-behaved sample source.
    /// </remarks>
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
        Schedule(fun input context ->
            map (fun (out, (delay: TimeSpan)) ->
                let jitter = sample () + 0.5
                let scaledTicks = float delay.Ticks * jitter

                let jitteredDelay =
                    if scaledTicks >= float maxDelayTicks then
                        maxDelay
                    else
                        TimeSpan.FromTicks(max 0L (int64 scaledTicks))

                out, jitteredDelay
            ) (op input context))

    /// <summary>Continues while either schedule continues, waiting for the shorter of their delays.</summary>
    /// <remarks>
    /// Both schedules are consulted at every decision. The output pairs their outputs; a side that has stopped
    /// contributes <c>None</c>, and the delay is then the continuing side's delay. The combined schedule stops when
    /// both have stopped.
    /// </remarks>
    /// <param name="other">The schedule combined with the piped-in schedule; its output is the second element.</param>
    /// <param name="schedule">The piped-in schedule; its output is the first element.</param>
    /// <example>
    /// <code>
    /// // Exponential back-off capped at 30 s, retrying forever
    /// Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    /// |&gt; Schedule.union (Schedule.spaced (TimeSpan.FromSeconds 30.0))
    /// </code>
    /// </example>
    let union
        (other: Schedule<'env, 'input, 'otherOutput>)
        (schedule: Schedule<'env, 'input, 'output>)
        : Schedule<'env, 'input, 'output option * 'otherOutput option> =
        let (Schedule left) = schedule
        let (Schedule right) = other

        Schedule(fun input context ->
            left input context
            |> bind (fun (leftOutput, (leftDelay: TimeSpan)) ->
                right input context
                |> map (fun (rightOutput, (rightDelay: TimeSpan)) ->
                    match leftOutput, rightOutput with
                    | None, None -> None, TimeSpan.Zero
                    | Some _, None -> Some(leftOutput, None), leftDelay
                    | None, Some _ -> Some(None, rightOutput), rightDelay
                    | Some _, Some _ -> Some(leftOutput, rightOutput), min leftDelay rightDelay)))

    /// <summary>Continues while both schedules continue, waiting for the longer of their delays.</summary>
    /// <param name="other">The schedule combined with the piped-in schedule; its output is the second element.</param>
    /// <param name="schedule">The piped-in schedule; its output is the first element.</param>
    /// <example>
    /// <code>
    /// // At most 10 retries, with exponential back-off
    /// Schedule.recurs 10
    /// |&gt; Schedule.intersect (Schedule.exponential (TimeSpan.FromMilliseconds 200.0))
    /// </code>
    /// </example>
    let intersect
        (other: Schedule<'env, 'input, 'otherOutput>)
        (schedule: Schedule<'env, 'input, 'output>)
        : Schedule<'env, 'input, 'output * 'otherOutput> =
        let (Schedule left) = schedule
        let (Schedule right) = other

        Schedule(fun input context ->
            left input context
            |> bind (fun (leftOutput, (leftDelay: TimeSpan)) ->
                right input context
                |> map (fun (rightOutput, (rightDelay: TimeSpan)) ->
                    match leftOutput, rightOutput with
                    | Some leftValue, Some rightValue -> Some(leftValue, rightValue), max leftDelay rightDelay
                    | _ -> None, TimeSpan.Zero)))

    /// The delay before the next run of a fixed-rate schedule. Runs belong to ticks at <c>LoopStarted + n * period</c>.
    /// A run that ended at or after the next tick overran it, so the next run starts immediately; its own start then
    /// realigns the schedule, so missed ticks never pile up. A start up to a tenth of a period before a tick counts as
    /// that tick: a timer can fire slightly before its due time, and without the allowance an early
    /// start would be assigned to the previous tick and trigger a spurious overrun.
    let internal fixedRateDelay (period: TimeSpan) (context: ScheduleContext) : TimeSpan =
        let tolerance = period.Ticks / 10L
        let startOffset = (context.ExecutionStarted - context.LoopStarted).Ticks
        let tick = (startOffset + tolerance) / period.Ticks
        let nextTick = context.LoopStarted + TimeSpan.FromTicks((tick + 1L) * period.Ticks)

        if context.ExecutionEnded >= nextTick then
            TimeSpan.Zero
        else
            nextTick - context.ExecutionEnded

    /// <summary>Recurs at a fixed rate aligned to when the first run began, emitting the recurrence count.</summary>
    /// <remarks>
    /// Runs start at <c>start + n * period</c>, so the time a run takes does not shift later runs. A run that takes
    /// longer than a period is followed by one immediate run, after which the schedule realigns to the next
    /// boundary; missed ticks are skipped rather than run in a burst.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="period"/> is not positive.</exception>
    /// <example>
    /// <code>
    /// scanOnce |&gt; Flow.repeat (Schedule.fixedRate (TimeSpan.FromMilliseconds 50.0))
    /// </code>
    /// </example>
    let fixedRate (period: TimeSpan) : Schedule<'env, 'input, int> =
        if period <= TimeSpan.Zero then
            invalidArg (nameof period) "A fixed-rate schedule requires a positive period."

        Schedule(fun _ context -> ok (Some context.Attempt, fixedRateDelay period context))

    /// <summary>Continues only while the input satisfies a predicate.</summary>
    /// <remarks>
    /// With <c>Flow.retry</c> the input is the typed error, so this selects which errors are worth retrying; an
    /// error that fails the predicate stops the schedule and the flow fails with it. With <c>Flow.repeat</c> the
    /// input is the successful value.
    /// </remarks>
    /// <param name="predicate">Returns <c>true</c> for inputs that may recur.</param>
    /// <param name="schedule">The schedule consulted for inputs that satisfy the predicate.</param>
    /// <example>
    /// <code>
    /// Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    /// |&gt; Schedule.upTo 3
    /// |&gt; Schedule.whileInput HttpError.isTransient
    /// </code>
    /// </example>
    let whileInput
        (predicate: 'input -> bool)
        (Schedule op: Schedule<'env, 'input, 'output>)
        : Schedule<'env, 'input, 'output> =
        Schedule(fun input context ->
            if predicate input then op input context else ok (None, TimeSpan.Zero))

    /// <summary>Continues until the input satisfies a predicate.</summary>
    /// <remarks>The negation of <c>whileInput</c>: an input that satisfies the predicate stops the schedule.</remarks>
    /// <param name="predicate">Returns <c>true</c> for inputs that stop the schedule.</param>
    /// <param name="schedule">The schedule consulted for other inputs.</param>
    let untilInput
        (predicate: 'input -> bool)
        (schedule: Schedule<'env, 'input, 'output>)
        : Schedule<'env, 'input, 'output> =
        whileInput (predicate >> not) schedule

    /// <summary>Stops a schedule after at most <paramref name="n" /> recurrences, keeping its output and delays.</summary>
    /// <remarks>
    /// Like <c>recurs</c>, <paramref name="n" /> counts recurrences after the first run, so <c>upTo 3</c> with
    /// <c>Flow.retry</c> allows 4 executions in total.
    /// </remarks>
    /// <param name="n">The maximum number of recurrences.</param>
    /// <param name="schedule">The schedule to cap.</param>
    /// <example>
    /// <code>
    /// Schedule.exponential (TimeSpan.FromMilliseconds 100.0) |&gt; Schedule.upTo 5
    /// </code>
    /// </example>
    let upTo (n: int) (Schedule op: Schedule<'env, 'input, 'output>) : Schedule<'env, 'input, 'output> =
        if n < 0 then
            invalidArg (nameof n) "Schedule.upTo requires a non-negative count."

        Schedule(fun input context ->
            if context.Attempt < n then op input context else ok (None, TimeSpan.Zero))

/// <summary>How long a <see cref="T:Axial.Retry`1" /> waits between attempts.</summary>
[<RequireQualifiedAccess>]
type Backoff =
    /// <summary>Retries immediately.</summary>
    | NoDelay
    /// <summary>Waits the same delay before every retry.</summary>
    | Fixed of delay: TimeSpan
    /// <summary>Doubles the delay before each retry, starting at <c>initial</c> and never exceeding <c>max</c>.</summary>
    | Exponential of initial: TimeSpan * max: TimeSpan

/// <summary>
/// A retry described with named fields: how many retries, how long to wait, and which inputs to retry.
/// </summary>
/// <remarks>
/// Build one from <c>Retry.defaults</c> with record update syntax and pass it to <c>Flow.retry</c> through
/// <c>Retry.schedule</c>. It is a shorthand for the common case; anything a record cannot express (jitter,
/// elapsed-time limits, fixed-rate runs) is written as a <c>Schedule</c> directly.
/// </remarks>
/// <typeparam name="input">The input the retry decision sees: the typed error for <c>Flow.retry</c>, the defect for <c>Flow.supervise</c>.</typeparam>
type Retry<'input> =
    {
        /// <summary>The maximum number of retries after the first attempt; <c>3</c> allows 4 executions in total.</summary>
        Retries: int
        /// <summary>The delay before each retry.</summary>
        Backoff: Backoff
        /// <summary>Returns <c>true</c> for inputs that should be retried; others stop immediately.</summary>
        When: 'input -> bool
    }

/// <summary>Constructors for <see cref="T:Axial.Retry`1" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Retry =
    /// <summary>Three retries with exponential backoff from 100 ms, capped at 10 s, retrying every input.</summary>
    /// <example>
    /// <code>
    /// fetch |&gt; Flow.retry (Retry.schedule { Retry.defaults with Retries = 5; When = HttpError.isTransient })
    /// </code>
    /// </example>
    let defaults<'input> : Retry<'input> =
        { Retries = 3
          Backoff = Backoff.Exponential(TimeSpan.FromMilliseconds 100.0, TimeSpan.FromSeconds 10.0)
          When = fun _ -> true }

    /// <summary>Builds the schedule a retry record describes.</summary>
    /// <param name="retry">The retry description.</param>
    /// <returns>A schedule that emits the retry number, starting at 0.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown when <c>Retries</c> is negative or a delay is negative.</exception>
    let schedule (retry: Retry<'input>) : Schedule<'env, 'input, int> =
        let delayOf =
            match retry.Backoff with
            | Backoff.NoDelay -> fun (_: int) -> TimeSpan.Zero
            | Backoff.Fixed delay ->
                if delay < TimeSpan.Zero then
                    invalidArg (nameof retry) "A fixed backoff requires a non-negative delay."

                fun _ -> delay
            | Backoff.Exponential(initial, max) ->
                if initial < TimeSpan.Zero || max < TimeSpan.Zero then
                    invalidArg (nameof retry) "An exponential backoff requires non-negative delays."

                fun attempt ->
                    let scaled = float initial.Ticks * Math.Pow(2.0, float attempt)

                    if scaled >= float max.Ticks then max else TimeSpan.FromTicks(int64 scaled)

        Schedule.recurs retry.Retries
        |> Schedule.whileInput retry.When
        |> fun (Schedule op) ->
            Schedule(fun input context ->
                Flow(fun env ct ->
                    FlowInternal.invoke (op input context) env ct
                    |> Execution.map (fun (decision, _) -> decision, delayOf context.Attempt)))
