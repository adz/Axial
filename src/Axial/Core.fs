namespace Axial

open System
open System.ComponentModel
open System.Threading
open System.Threading.Tasks

#if !FABLE_COMPILER
/// <summary>A clock that advances only when a test or simulator asks it to.</summary>
/// <remarks>Both UTC timestamps and timed workflow operations use the same advance. On .NET, due synchronous
/// continuations run during advancement. On Fable, promise continuations run on a later microtask; await the
/// released fiber or a registration signal before advancing again.</remarks>
type ManualClock(initialUtc: DateTimeOffset) =
    let time = Platform.ManualTime()

    /// <summary>The number of delays waiting for the clock.</summary>
    member _.Sleepers = time.Sleepers

    /// <summary>The next monotonic deadline, when a delay is waiting.</summary>
    member _.NextDeadline = time.NextDeadline

    /// <summary>The latest currently registered deadline, for bounded explicit draining.</summary>
    member _.LastDeadline = time.LastDeadline

    /// <summary>Advances wall and monotonic time together, running continuations released by due delays before returning.</summary>
    member _.AdvanceBy(duration: TimeSpan) =
        if duration < TimeSpan.Zero then invalidArg (nameof duration) "Clock cannot move backwards."
        time.Advance duration

    /// <summary>Advances to the next waiting deadline; returns false when no delay is waiting.</summary>
    member _.AdvanceToNextDeadline() = time.AdvanceToNextDeadline()

    interface IClock with
        member _.UtcNow() = initialUtc + (time :> Platform.ITimeSource).Now()
        member _.Elapsed() = (time :> Platform.ITimeSource).Now()
        member _.Sleep(delay, cancellationToken) =
            task {
                let! exit = ((time :> Platform.ITimeSource).Sleep<unit>(delay, cancellationToken)).AsTask()
                match exit with
                | Exit.Success() -> ()
                | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException(cancellationToken))
                | Exit.Failure cause -> failwith (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Manual clock sleep failed: {0}", cause))
            } :> Task
#else
/// A single-threaded JavaScript manual clock; sleeps move only on explicit advancement.
/// Released promise continuations run on a later microtask; await their fiber or a signal before advancing again.
type ManualClock(initialUtc: DateTimeOffset) =
    let mutable now = TimeSpan.Zero
    let mutable sequence = 0L
    let sleepers = System.Collections.Generic.Dictionary<int64, TimeSpan * (unit -> unit)>()
    let advanceTo target =
        now <- max now target
        let ready =
            sleepers
            |> Seq.filter (fun entry -> fst entry.Value <= now)
            |> Seq.sortBy (fun entry -> fst entry.Value, entry.Key)
            |> Seq.toArray
        for entry in ready do sleepers.Remove entry.Key |> ignore
        for entry in ready do snd entry.Value ()
    member _.Sleepers = sleepers.Count
    member _.NextDeadline =
        if sleepers.Count = 0 then None else Some(sleepers.Values |> Seq.map fst |> Seq.min)
    member _.LastDeadline =
        if sleepers.Count = 0 then None else Some(sleepers.Values |> Seq.map fst |> Seq.max)
    member _.AdvanceBy(duration: TimeSpan) =
        if duration < TimeSpan.Zero then invalidArg (nameof duration) "Clock cannot move backwards."
        advanceTo (now + duration)
    member this.AdvanceToNextDeadline() =
        match this.NextDeadline with
        | Some deadline -> advanceTo deadline; true
        | None -> false
    interface IClock with
        member _.UtcNow() = initialUtc + now
        member _.Elapsed() = now
        member _.Sleep(delay, token) =
            let pending =
                Fable.Core.JS.Constructors.Promise.Create<unit>(fun resolve reject ->
                    if token.IsCancellationRequested then reject (box (OperationCanceledException()))
                    elif delay <= TimeSpan.Zero then resolve (box ())
                    else
                        let id = sequence
                        sequence <- sequence + 1L
                        let mutable registration: IDisposable option = None
                        let mutable settled = false
                        let finish canceled =
                            if not settled then
                                settled <- true
                                sleepers.Remove id |> ignore
                                registration |> Option.iter _.Dispose()
                                if canceled then reject (box (OperationCanceledException())) else resolve (box ())
                        sleepers.Add(id, (now + delay, fun () -> finish false))
                        if not (isNull (box token)) then
                            let registered = unbox<IDisposable> (box (token.Register(fun () -> finish true)))
                            registration <- Some registered
                            if settled then registered.Dispose())
            unbox<Task> pending
#endif

[<RequireQualifiedAccess>]
module Cause =
    /// <summary>Transforms the error value of a failure cause using the provided function.</summary>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="cause">The original cause to transform.</param>
    /// <returns>A new cause with the transformed error value, or the original cause if it was not a <c>Fail</c>.</returns>
    let rec map (mapper: 'e -> 'f) (cause: Cause<'e>) : Cause<'f> =
        match cause with
        | Cause.Fail e -> Cause.Fail (mapper e)
        | Cause.Die ex -> Cause.Die ex
        | Cause.Interrupt -> Cause.Interrupt
        | Cause.Then(left, right) -> Cause.Then(map mapper left, map mapper right)
        | Cause.Both(left, right) -> Cause.Both(map mapper left, map mapper right)
        | Cause.Traced(inner, trace) -> Cause.Traced(map mapper inner, trace)

    /// <summary>Combines causes that happened sequentially.</summary>
    let thenCause (left: Cause<'error>) (right: Cause<'error>) : Cause<'error> =
        Cause.Then(left, right)

    /// <summary>Combines causes that happened concurrently.</summary>
    let both (left: Cause<'error>) (right: Cause<'error>) : Cause<'error> =
        Cause.Both(left, right)

    /// <summary>Attaches diagnostic trace text to a cause.</summary>
    let traced (trace: string) (cause: Cause<'error>) : Cause<'error> =
        Cause.Traced(cause, trace)

    /// <summary>Removes trace text from the top of a cause, returning the cause it annotates.</summary>
    /// <remarks>
    /// A trace describes where a cause passed; it does not change what happened. Match on the result to recognise a
    /// typed failure or a defect whether or not it was traced, as the recovery combinators such as
    /// <c>Flow.orElseWith</c> and <c>Flow.catch</c> do.
    /// </remarks>
    /// <example>
    /// <code>
    /// match Cause.untraced cause with
    /// | Cause.Fail error -&gt; Some error
    /// | _ -&gt; None
    /// </code>
    /// </example>
    let rec untraced (cause: Cause<'error>) : Cause<'error> =
        match cause with
        | Cause.Traced(inner, _) -> untraced inner
        | other -> other

    /// <summary>Returns every typed failure value contained in a cause tree.</summary>
    let rec failures (cause: Cause<'error>) : 'error list =
        match cause with
        | Cause.Fail error -> [ error ]
        | Cause.Die _ -> []
        | Cause.Interrupt -> []
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> failures left @ failures right
        | Cause.Traced(inner, _) -> failures inner

    /// <summary>Returns every defect exception contained in a cause tree.</summary>
    let rec defects (cause: Cause<'error>) : exn list =
        match cause with
        | Cause.Fail _ -> []
        | Cause.Die error -> [ error ]
        | Cause.Interrupt -> []
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> defects left @ defects right
        | Cause.Traced(inner, _) -> defects inner

    /// <summary>Returns whether the cause tree contains an interruption signal.</summary>
    let rec isInterrupted (cause: Cause<'error>) : bool =
        match cause with
        | Cause.Interrupt -> true
        | Cause.Then(left, right)
        | Cause.Both(left, right) -> isInterrupted left || isInterrupted right
        | Cause.Traced(inner, _) -> isInterrupted inner
        | Cause.Fail _
        | Cause.Die _ -> false

    /// <summary>Pretty prints a cause tree for diagnostics.</summary>
    let prettyPrint (formatError: 'error -> string) (cause: Cause<'error>) : string =
        let rec loop indent current =
            let padding = String.replicate indent " "

            match current with
            | Cause.Fail error -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Fail({1})", padding, formatError error))
            | Cause.Die error ->
                (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Die({1})", padding, Platform.dieDescription error))
            | Cause.Interrupt -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Interrupt", padding))
            | Cause.Then(left, right) ->
                (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Then\n{1}\n{2}", padding, loop (indent + 2) left, loop (indent + 2) right))
            | Cause.Both(left, right) ->
                (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Both\n{1}\n{2}", padding, loop (indent + 2) left, loop (indent + 2) right))
            | Cause.Traced(inner, trace) ->
                (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}Traced({1})\n{2}", padding, trace, loop (indent + 2) inner))

        loop 0 cause

[<RequireQualifiedAccess>]
module Exit =
    /// <summary>Transforms the success value of an exit outcome using the provided function.</summary>
    /// <param name="mapper">The function to transform the success value.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with the transformed success value.</returns>
    let map (mapper: 'v -> 'w) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> Exit.Success (mapper v)
        | Exit.Failure c -> Exit.Failure c

    /// <summary>Binds the success value of an exit outcome to a function that returns a new exit outcome.</summary>
    /// <param name="binder">The function that takes a success value and returns a new exit outcome.</param>
    /// <param name="exit">The exit outcome to bind.</param>
    /// <returns>The result of the binder function if the exit was successful; otherwise, the original failure.</returns>
    let bind (binder: 'v -> Exit<'w, 'e>) (exit: Exit<'v, 'e>) : Exit<'w, 'e> =
        match exit with
        | Exit.Success v -> binder v
        | Exit.Failure c -> Exit.Failure c

    /// <summary>Transforms the error value of a failed exit outcome using the provided function.</summary>
    /// <param name="mapper">The function to transform the error value.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with the transformed error value.</returns>
    let mapError (mapper: 'e -> 'f) (exit: Exit<'v, 'e>) : Exit<'v, 'f> =
        match exit with
        | Exit.Success v -> Exit.Success v
        | Exit.Failure c -> Exit.Failure (Cause.map mapper c)

    /// <summary>Transforms both success and failure outcomes of an exit using the provided functions.</summary>
    /// <param name="onSuccess">The function to transform the success value.</param>
    /// <param name="onFailure">The function to transform the failure cause.</param>
    /// <param name="exit">The exit outcome to transform.</param>
    /// <returns>A new exit outcome with transformed values.</returns>
    let mapBoth (onSuccess: 'v -> 'w) (onFailure: Cause<'e> -> Cause<'f>) (exit: Exit<'v, 'e>) : Exit<'w, 'f> =
        match exit with
        | Exit.Success v -> Exit.Success (onSuccess v)
        | Exit.Failure c -> Exit.Failure (onFailure c)

    /// <summary>Creates an exit outcome from a standard F# <c>Result</c>.</summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>An exit outcome representing the result.</returns>
    let fromResult (result: Result<'v, 'e>) : Exit<'v, 'e> =
        match result with
        | Ok v -> Exit.Success v
        | Error e -> Exit.Failure (Cause.Fail e)

    /// <summary>Converts an exit outcome to a standard F# <c>Result</c>.</summary>
    /// <param name="exit">The exit outcome to convert.</param>
    /// <returns>A <c>Result</c> representing the successful value or the domain failure.</returns>
    /// <exception cref="T:System.Exception">Re-throws the original exception if the exit was <c>Cause.Die</c>.</exception>
    /// <exception cref="T:System.OperationCanceledException">Throws if the exit was <c>Cause.Interrupt</c>.</exception>
    let toResult (exit: Exit<'v, 'e>) : Result<'v, 'e> =
        match exit with
        | Exit.Success v -> Ok v
        | Exit.Failure (Cause.Fail e) -> Error e
        | Exit.Failure (Cause.Die ex) -> raise ex
        | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException("Workflow was interrupted"))
        | Exit.Failure cause ->
            let defects = Cause.defects cause

            if not (List.isEmpty defects) then
                raise (AggregateException("Workflow failed with one or more defects.", defects))
            elif Cause.isInterrupted cause then
                raise (OperationCanceledException("Workflow was interrupted"))
            else
                let rendered = Cause.prettyPrint (fun error -> OutcomeText.value (box error)) cause
                raise (InvalidOperationException((System.String.Format(Globalization.CultureInfo.InvariantCulture, "Workflow failed with a composite cause that cannot be represented as Result: {0}", rendered))))

/// <summary>Unique identifier for a running fiber.</summary>
[<Struct>]
type FiberId =
    | FiberId of int64

    /// <summary>The id as <c>#n</c>, rendered without reflection.</summary>
    override this.ToString() =
        let (FiberId value) = this
        (System.String.Format(Globalization.CultureInfo.InvariantCulture, "#{0}", value))

    /// <summary>The numeric fiber identifier.</summary>
    member this.Value =
        let (FiberId value) = this
        value

/// <summary>Diagnostic metadata for a running fiber.</summary>
type FiberMetadata =
    {
        /// <summary>The unique fiber id.</summary>
        Id: FiberId
        /// <summary>The diagnostic name given at the fork site (<c>Flow.forkNamed</c>), if any.</summary>
        Name: string option
        /// <summary>The parent fiber id, if the fiber was forked from another fiber.</summary>
        ParentId: FiberId option
        /// <summary>The runtime annotations in scope at the fork site.</summary>
        Annotations: Map<string, string>
        /// <summary>The UTC timestamp when the fiber started.</summary>
        StartedAt: DateTimeOffset
        /// <summary>The monotonic reading when the fiber started.</summary>
        StartedTick: TimeSpan
        /// <summary>The UTC timestamp when the fiber settled, if it has.</summary>
        mutable SettledAt: DateTimeOffset option
        /// <summary>The monotonic reading when the fiber settled.</summary>
        mutable SettledTick: TimeSpan option
        /// <summary>The current fiber status.</summary>
        mutable Status: FiberStatus
    }

/// <summary>Structured diagnostic snapshot of a fiber, taken at a single point in time.</summary>
type FiberDump =
    {
        /// <summary>The fiber id.</summary>
        Id: FiberId
        /// <summary>The diagnostic name given at the fork site, if any.</summary>
        Name: string option
        /// <summary>The parent fiber id, if available.</summary>
        ParentId: FiberId option
        /// <summary>The runtime annotations in scope at the fork site.</summary>
        Annotations: Map<string, string>
        /// <summary>The UTC timestamp when the fiber started.</summary>
        StartedAt: DateTimeOffset
        /// <summary>The monotonic reading when the fiber started.</summary>
        StartedTick: TimeSpan
        /// <summary>The UTC timestamp when the fiber settled, if it had settled when the snapshot was taken.</summary>
        SettledAt: DateTimeOffset option
        /// <summary>The monotonic reading when the fiber settled.</summary>
        SettledTick: TimeSpan option
        /// <summary>The fiber status when the snapshot was taken.</summary>
        Status: FiberStatus
    }

    /// <summary>Renders the dump as a single line against one monotonic reading.</summary>
    member internal dump.RenderAtTick(nowTick: TimeSpan) : string =
        let name =
            match dump.Name with
            | Some name -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, " \"{0}\"", name))
            | None -> ""

        let lifetime =
            let settled = dump.SettledTick |> Option.defaultValue nowTick
            let elapsed = settled - dump.StartedTick
            (max elapsed.TotalSeconds 0.0).ToString("F1", Globalization.CultureInfo.InvariantCulture) + "s"

        let annotations =
            if Map.isEmpty dump.Annotations then
                ""
            else
                " ["
                + (dump.Annotations
                   |> Seq.map (fun (KeyValue(key, value)) -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}={1}", key, value)))
                   |> String.concat " ")
                + "]"

        let startedAt = dump.StartedAt.ToString("o")
        (System.String.Format(Globalization.CultureInfo.InvariantCulture, "#{0}{1} {2} {3} (started {4}){5}", dump.Id.Value, name, dump.Status, lifetime, startedAt, annotations))

    /// <summary>Renders the dump as a single line, measuring lifetime against <paramref name="clock" /> for live fibers.</summary>
    member dump.RenderAt(clock: IClock) : string =
        dump.RenderAtTick(dump.SettledTick |> Option.defaultWith clock.Elapsed)

    /// <summary>Renders the dump without reflection (safe under NativeAOT), measuring lifetime to when it settled.</summary>
    override dump.ToString() =
        let name = dump.Name |> Option.map (fun value -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, " \"{0}\"", value))) |> Option.defaultValue ""
        let lifetime = (dump.SettledTick |> Option.defaultValue dump.StartedTick) - dump.StartedTick
        let annotations =
            if Map.isEmpty dump.Annotations then ""
            else
                " ["
                + (dump.Annotations
                   |> Seq.map (fun (KeyValue(key, value)) -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "{0}={1}", key, value)))
                   |> String.concat " ")
                + "]"
        let seconds = (max lifetime.TotalSeconds 0.0).ToString("F1", Globalization.CultureInfo.InvariantCulture)
        let startedAt = dump.StartedAt.ToString("o")
        (System.String.Format(Globalization.CultureInfo.InvariantCulture, "#{0}{1} {2} {3}s (started {4}){5}", dump.Id.Value, name, dump.Status, seconds, startedAt, annotations))

/// <summary>Snapshot conversion and rendering for fiber dumps.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiberDump =
    /// <summary>Takes a dump from live fiber metadata.</summary>
    let ofMetadata (metadata: FiberMetadata) : FiberDump =
        {
            Id = metadata.Id
            Name = metadata.Name
            ParentId = metadata.ParentId
            Annotations = metadata.Annotations
            StartedAt = metadata.StartedAt
            StartedTick = metadata.StartedTick
            SettledAt = metadata.SettledAt
            SettledTick = metadata.SettledTick
            Status = metadata.Status
        }

    /// <summary>Renders one dump as a single line, measuring lifetime against <paramref name="now" /> for live fibers.</summary>
    let renderAt (clock: IClock) (dump: FiberDump) : string = dump.RenderAt clock

    /// <summary>
    /// Renders a set of dumps as an indented parent/child tree. Fibers whose parent is absent from
    /// <paramref name="dumps" /> (including root fibers) become top-level nodes.
    /// </summary>
    let renderTreeAt (clock: IClock) (dumps: FiberDump list) : string =
        let nowTick = clock.Elapsed()
        let ids = dumps |> List.map (fun dump -> dump.Id) |> Set.ofList

        let childrenOf =
            dumps
            |> List.choose (fun dump ->
                match dump.ParentId with
                | Some parentId when Set.contains parentId ids -> Some(parentId, dump)
                | _ -> None)
            |> List.groupBy fst
            |> List.map (fun (parentId, pairs) -> parentId, pairs |> List.map snd |> List.sortBy _.Id.Value)
            |> Map.ofList

        let roots =
            dumps
            |> List.filter (fun dump ->
                match dump.ParentId with
                | Some parentId -> not (Set.contains parentId ids)
                | None -> true)
            |> List.sortBy _.Id.Value

        let lines = ResizeArray<string>()

        let rec renderNode (prefix: string) (childPrefix: string) (dump: FiberDump) =
            lines.Add(prefix + dump.RenderAtTick nowTick)

            let children = childrenOf |> Map.tryFind dump.Id |> Option.defaultValue []

            children
            |> List.iteri (fun index child ->
                let isLast = index = children.Length - 1
                let connector = if isLast then "└─ " else "├─ "
                let continuation = if isLast then "   " else "│  "
                renderNode (childPrefix + connector) (childPrefix + continuation) child)

        for root in roots do
            renderNode "" "" root

        String.concat "\n" lines

/// <summary>
/// Runtime hooks observing fiber lifecycle events for diagnostics and telemetry.
/// </summary>
/// <remarks>
/// Installed once at the application edge with <c>Flow.withFiberObserver</c> and carried implicitly to every
/// descendant fork. All hooks default to no-ops, receive only diagnostic data (<c>FiberMetadata</c> and defect
/// exceptions, never typed exits), and must not throw; exceptions raised by hooks are swallowed so a
/// diagnostics hook can never alter a fiber's outcome.
/// </remarks>

type FiberObserver =
    {
        /// <summary>A fiber was forked. Receives the child fiber's metadata.</summary>
        OnStart: FiberMetadata -> unit
        /// <summary>
        /// A fiber settled. <c>FiberMetadata.Status</c> distinguishes success, failure, and interruption; the
        /// first <c>Cause.Die</c> defect in the exit, if any, is passed alongside.
        /// </summary>
        OnEnd: FiberMetadata -> exn option -> unit
        /// <summary>
        /// A <c>Cause.Die</c> defect became unobservable: a forked fiber died unobserved and no observation can
        /// happen anymore, or the runtime discarded a race/timeout loser's exit. The metadata is absent for
        /// discarded race/timeout losers, which are executions rather than fibers.
        /// </summary>
        OnUnobservedDefect: FiberMetadata option -> exn -> unit
    }

/// <summary>Standard fiber observers.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FiberObserver =
    /// <summary>The default observer: every hook is a no-op.</summary>
    let none : FiberObserver =
        {
            OnStart = ignore
            OnEnd = fun _ _ -> ()
            OnUnobservedDefect = fun _ _ -> ()
        }

    /// <summary>Combines two observers so every hook runs both, each guarded independently.</summary>
    /// <remarks>Use this to stack integrations, for example telemetry spans plus logging, from one edge-level install.</remarks>
    let compose (first: FiberObserver) (second: FiberObserver) : FiberObserver =
        {
            OnStart =
                fun metadata ->
                    (try first.OnStart metadata with _ -> ())
                    (try second.OnStart metadata with _ -> ())
            OnEnd =
                fun metadata defect ->
                    (try first.OnEnd metadata defect with _ -> ())
                    (try second.OnEnd metadata defect with _ -> ())
            OnUnobservedDefect =
                fun metadata defect ->
                    (try first.OnUnobservedDefect metadata defect with _ -> ())
                    (try second.OnUnobservedDefect metadata defect with _ -> ())
        }

    let internal notifyStart (observer: FiberObserver) (metadata: FiberMetadata) : unit =
        // axial-note AXG010: an observer's OnStart failure must not prevent the observed fiber from starting.
        try observer.OnStart metadata with _ -> ()

    let internal notifyEnd (observer: FiberObserver) (metadata: FiberMetadata) (defect: exn option) : unit =
        // axial-note AXG010: OnEnd is diagnostic; its failure must not replace the fiber's already settled exit.
        try observer.OnEnd metadata defect with _ -> ()

    let internal notifyUnobservedDefect
        (observer: FiberObserver)
        (metadata: FiberMetadata option)
        (defect: exn)
        : unit =
        // axial-note AXG010: the unobserved-defect sink is the last reporting destination; recursively reporting its failure would call the same sink.
        try observer.OnUnobservedDefect metadata defect with _ -> ()

/// <summary>A forked fiber that has settled, as recorded by a <see cref="T:Axial.FiberRegistry" />.</summary>
type SettledFiber =
    {
        /// <summary>The fiber's metadata at the moment it settled, including <c>SettledAt</c> and <c>Status</c>.</summary>
        Fiber: FiberDump
        /// <summary>
        /// For a fiber that failed, its cause rendered as text: typed errors with their own <c>ToString</c>, defects as
        /// exception type and message. <c>None</c> for fibers that succeeded or were interrupted.
        /// </summary>
        Failure: string option
    }

    /// <summary>How long the fiber ran.</summary>
    member this.Duration =
        (this.Fiber.SettledTick |> Option.defaultValue this.Fiber.StartedTick) - this.Fiber.StartedTick

/// <summary>Totals for every settled fiber that shared a name, as recorded by a <see cref="T:Axial.FiberRegistry" />.</summary>
type FiberStats =
    {
        /// <summary>The name given at the fork site, or <c>(unnamed)</c> for fibers forked without one.</summary>
        Name: string
        /// <summary>How many fibers with this name settled.</summary>
        Count: int
        /// <summary>How many of them failed with a typed error or a defect.</summary>
        Failed: int
        /// <summary>How many of them were interrupted.</summary>
        Interrupted: int
        /// <summary>The sum of their durations.</summary>
        TotalDuration: TimeSpan
        /// <summary>The longest single duration.</summary>
        MaxDuration: TimeSpan
    }

/// <summary>A defect that nobody observed, as recorded by a <see cref="T:Axial.FiberRegistry" />.</summary>
type UnobservedDefect =
    {
        /// <summary>The fiber that died, or <c>None</c> for a discarded race or timeout loser, which is not a fiber.</summary>
        Fiber: FiberDump option
        /// <summary>The defect's exception type and message.</summary>
        Defect: string
    }

/// The interrupt handle of every running forked fiber, keyed by fiber id. Filled by <c>Flow.fork</c> and emptied as
/// fibers settle, so it holds only live fibers. It lets <c>FiberRegistry</c> interrupt a fiber it only knows by id.
module internal FiberInterrupts =
    let private gate = obj ()
    let private sources = System.Collections.Generic.Dictionary<int64, CancellationTokenSource>()

    let register (id: FiberId) (source: CancellationTokenSource) =
        lock gate (fun () -> sources[id.Value] <- source)

    let remove (id: FiberId) =
        lock gate (fun () -> sources.Remove id.Value |> ignore)

    /// Signals the fiber to stop; returns false when it is no longer running.
    let signal (id: FiberId) : bool =
        let source = lock gate (fun () -> match sources.TryGetValue id.Value with | true, source -> Some source | _ -> None)

        match source with
        | Some source ->
            source.Cancel()
            true
        | None -> false

/// <summary>
/// Tracks every live forked fiber so the whole runtime can be dumped as a parent/child tree at any moment.
/// </summary>
/// <remarks>
/// Install with <c>Flow.withFiberRegistry</c> at the application edge; the registry's observer is composed
/// with any observer already installed. Settled fibers leave the registry when they settle, so
/// <c>Snapshot</c> covers live fibers only (plus any that settle mid-snapshot, which carry their
/// <c>SettledAt</c>). Root workflow executions are not forked fibers and do not appear; forked fibers whose
/// parent is the root render as top-level nodes.
/// </remarks>
/// <param name="historyCapacity">How many settled fibers and unobserved defects the registry remembers; totals per
/// fiber name are kept for every fiber.</param>
type FiberRegistry(historyCapacity: int) =
    do
        if historyCapacity < 0 then
            invalidArg (nameof historyCapacity) "The history capacity cannot be negative."

    let gate = obj()
    let live = System.Collections.Generic.Dictionary<int64, FiberMetadata>()
    let settled = System.Collections.Generic.Queue<SettledFiber>()
    let unobserved = System.Collections.Generic.Queue<UnobservedDefect>()
    let stats = System.Collections.Generic.Dictionary<string, FiberStats>()
    // Failure text arrives just before the fiber's OnEnd, which consumes it.
    let pendingFailures = System.Collections.Generic.Dictionary<int64, string>()
    let mutable started = 0L

    let enqueueBounded (queue: System.Collections.Generic.Queue<'a>) (item: 'a) =
        if historyCapacity > 0 then
            if queue.Count >= historyCapacity then
                queue.Dequeue() |> ignore

            queue.Enqueue item

    let recordEnd (metadata: FiberMetadata) (defect: exn option) =
        lock gate (fun () ->
            live.Remove metadata.Id.Value |> ignore

            let failure =
                match pendingFailures.TryGetValue metadata.Id.Value with
                | true, text ->
                    pendingFailures.Remove metadata.Id.Value |> ignore
                    Some text
                | _ when metadata.Status = FiberStatus.Failed -> defect |> Option.map Platform.dieDescription
                | _ -> None

            let fiber = { Fiber = FiberDump.ofMetadata metadata; Failure = failure }
            enqueueBounded settled fiber

            let name = metadata.Name |> Option.defaultValue "(unnamed)"
            let duration = fiber.Duration

            let previous =
                match stats.TryGetValue name with
                | true, value -> value
                | _ -> { Name = name; Count = 0; Failed = 0; Interrupted = 0; TotalDuration = TimeSpan.Zero; MaxDuration = TimeSpan.Zero }

            stats[name] <-
                { previous with
                    Count = previous.Count + 1
                    Failed = previous.Failed + (if metadata.Status = FiberStatus.Failed then 1 else 0)
                    Interrupted = previous.Interrupted + (if metadata.Status = FiberStatus.Interrupted then 1 else 0)
                    TotalDuration = previous.TotalDuration + duration
                    MaxDuration = max previous.MaxDuration duration })

    let observer =
        { FiberObserver.none with
            OnStart =
                fun metadata ->
                    lock gate (fun () ->
                        live[metadata.Id.Value] <- metadata
                        started <- started + 1L)
            OnEnd = recordEnd
            OnUnobservedDefect =
                fun metadata defect ->
                    lock gate (fun () ->
                        enqueueBounded
                            unobserved
                            { Fiber = metadata |> Option.map FiberDump.ofMetadata
                              Defect = Platform.dieDescription defect }) }

    /// <summary>Creates a registry that remembers the last 200 settled fibers and unobserved defects.</summary>
    new() = FiberRegistry(200)

    /// Records a failed fiber's rendered cause; called by the runtime just before the fiber's OnEnd.
    member internal _.RecordFailure (metadata: FiberMetadata) (text: string) =
        lock gate (fun () -> pendingFailures[metadata.Id.Value] <- text)

    /// <summary>How many settled fibers and unobserved defects the registry remembers.</summary>
    member _.HistoryCapacity = historyCapacity

    /// <summary>How many fibers have started since the registry was installed.</summary>
    member _.StartedCount : int64 = lock gate (fun () -> started)

    /// <summary>The most recently settled fibers, oldest first, up to <c>HistoryCapacity</c>.</summary>
    /// <remarks>
    /// Failed fibers carry their rendered cause when the registry was installed with <c>Flow.withFiberRegistry</c>;
    /// an observer composed by hand sees only defects.
    /// </remarks>
    member _.Settled() : SettledFiber list = lock gate (fun () -> List.ofSeq settled)

    /// <summary>Totals per fiber name for every fiber that settled since the registry was installed, ordered by name.</summary>
    member _.Stats() : FiberStats list =
        lock gate (fun () -> List.ofSeq stats.Values) |> List.sortBy _.Name

    /// <summary>The most recent defects that nobody observed, oldest first, up to <c>HistoryCapacity</c>.</summary>
    member _.UnobservedDefects() : UnobservedDefect list = lock gate (fun () -> List.ofSeq unobserved)

    /// <summary>The lifecycle observer that feeds the registry. Compose it if installing observers manually.</summary>
    member _.Observer : FiberObserver = observer

    /// <summary>The number of live fibers currently tracked.</summary>
    member _.LiveFiberCount : int = lock gate (fun () -> live.Count)

    /// <summary>Takes a structured dump of every live fiber, ordered by fiber id.</summary>
    member _.Snapshot() : FiberDump list =
        lock gate (fun () -> live.Values |> Seq.map FiberDump.ofMetadata |> List.ofSeq)
        |> List.sortBy _.Id.Value

    /// <summary>Signals the live fiber with <paramref name="id" /> to stop, without waiting for it.</summary>
    /// <remarks>
    /// For diagnostics screens and hang recovery. The fiber is interrupted as <c>Fiber.interrupt</c> would, and
    /// whoever joins or awaits it sees <c>Cause.Interrupt</c>. Only fibers tracked by this registry can be
    /// interrupted through it.
    /// </remarks>
    /// <returns><c>true</c> when the fiber was live and has been signalled.</returns>
    member _.Interrupt(id: FiberId) : bool =
        let tracked = lock gate (fun () -> live.ContainsKey id.Value)
        tracked && FiberInterrupts.signal id

    /// <summary>Signals every live fiber forked with <paramref name="name" /> to stop, without waiting for them.</summary>
    /// <returns>The number of fibers signalled.</returns>
    member this.InterruptByName(name: string) : int =
        let ids = lock gate (fun () -> [ for metadata in live.Values do if metadata.Name = Some name then metadata.Id ])
        ids |> List.filter this.Interrupt |> List.length

    /// <summary>Renders a snapshot of live fibers as a human-readable parent/child tree, timestamped at <paramref name="now" />.</summary>
    member this.DumpAt(clock: IClock) : string =
        let snapshot = this.Snapshot()
        let now = clock.UtcNow()
        let renderedNow = now.ToString("o")
        let header = (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Fiber dump @ {0} — {1} live fiber(s)", renderedNow, snapshot.Length))

        match snapshot with
        | [] -> header
        | dumps -> header + "\n" + FiberDump.renderTreeAt clock dumps

/// <summary>
/// Tracks a forked fiber's settled defect so it can be reported as unobserved exactly once, by whichever
/// detection mechanism (scope-close sweep or garbage-collection net) reaches finality first.
/// </summary>
type internal FiberDefectTracker(metadata: FiberMetadata, observer: FiberObserver) =
    let gate = obj()
    let mutable defect: exn option = None
    let mutable reported = false
    let mutable observed = false

#if !FABLE_COMPILER
    static let sentinels = System.Runtime.CompilerServices.ConditionalWeakTable<obj, FiberDefectTracker>()
#endif

    /// Records that the fiber's outcome was consumed (<c>Fiber.join</c>, <c>Fiber.awaitExit</c>, <c>Fiber.interrupt</c>)
    /// or deliberately detached at birth (<c>Flow.forkDetached</c>), so its defect is never reported as unobserved.
    member _.MarkObserved() =
        lock gate (fun () -> observed <- true)

    /// Records the defect the fiber settled with, if any.
    member _.Settled(settledDefect: exn option) =
        lock gate (fun () -> defect <- settledDefect)

    /// Reports the fiber's defect as unobserved if it has one, was never observed, and was not already
    /// reported. Safe to call from the scope sweep and the GC net concurrently.
    member _.TryReport() =
        let toReport =
            lock gate (fun () ->
                if not reported && not observed then
                    match defect with
                    | Some _ ->
                        reported <- true
                        defect
                    | None -> None
                else
                    None)

        match toReport with
        | Some exn -> FiberObserver.notifyUnobservedDefect observer (Some metadata) exn
        | None -> ()

#if !FABLE_COMPILER
    /// Keeps <paramref name="tracker" /> alive exactly as long as <paramref name="fiberHandle" /> is
    /// reachable. When a discarded handle is collected, the tracker becomes collectable and its finalizer
    /// reports any unobserved defect, the same mechanism as <c>TaskScheduler.UnobservedTaskException</c>.
    static member Attach(fiberHandle: obj, tracker: FiberDefectTracker) =
        sentinels.Add(fiberHandle, tracker)

    override this.Finalize() =
        // axial-note AXG010: GC finalization must not terminate the process if the last-chance defect reporter fails.
        try this.TryReport() with _ -> ()
#endif

/// <summary>
/// Represents a handle to a workflow that has already been started.
/// </summary>
/// <remarks>
/// A fiber is the hot counterpart to a cold <c>Flow</c>, returned by <c>Flow.fork</c>. Wait for it with
/// <c>Fiber.join</c> (its value, re-raising its failure) or <c>Fiber.awaitExit</c> (its <c>Exit</c>), check it with
/// <c>Fiber.poll</c>, and stop it with <c>Fiber.interrupt</c>.
/// </remarks>
/// <typeparam name="error">The failure type of the running workflow.</typeparam>
/// <typeparam name="value">The success type of the running workflow.</typeparam>
[<Sealed>]
type Fiber<'error, 'value>
    internal
    (
        metadata: FiberMetadata,
        exitTask: Platform.ExitTask<'value, 'error>,
        interruptSource: CancellationTokenSource,
        tracker: FiberDefectTracker,
        settled: Exit<'value, 'error> option ref
    ) =
    /// <summary>Diagnostic metadata for the running fiber.</summary>
    member _.Metadata = metadata

    /// The asynchronous operation that completes with the workflow's final exit outcome.
    member internal _.ExitTask = exitTask

    /// The cancellation source that <c>Fiber.interrupt</c> uses to signal interruption.
    member internal _.InterruptSource = interruptSource

    /// Marks the fiber's outcome as consumed; see <c>FiberDefectTracker.MarkObserved</c>.
    member internal _.MarkObserved() = tracker.MarkObserved()

    /// Reports the fiber's settled defect if its outcome was not consumed.
    member internal _.ReportUnobserved() = tracker.TryReport()

    /// The fiber's exit once it has settled.
    member internal _.Settled = settled.Value

#if !FABLE_COMPILER
/// <summary>
/// Represents delayed task work that can observe a runtime cancellation token when it is started.
/// </summary>
/// <remarks>
/// Bind a cold task directly in <c>flow { }</c>. When the task produces <c>Result&lt;'value,'error&gt;</c>,
/// use <c>Flow.fromColdTaskResult</c> to place <c>Error</c> in Flow's typed error channel.
/// A raw, already-started <c>Task</c> is not a Flow builder source.
/// </remarks>
/// <typeparam name="value">The type of the produced task value.</typeparam>
type ColdTask<'value> =
    | ColdTask of (CancellationToken -> Task<'value>)

/// <summary>
/// Functions for creating and executing cold tasks.
/// </summary>
/// <remarks>
/// A cold task is a task factory, not a task. Nothing runs until it is executed, and it can be
/// executed more than once. Prefer these over already-started <c>Task</c> values so a workflow
/// stays a description and observes the runtime's cancellation token.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module ColdTask =
    /// <summary>Creates a cold task from a cancellable factory.</summary>
    /// <param name="operation">The factory invoked on each execution.</param>
    let create (operation: CancellationToken -> Task<'value>) : ColdTask<'value> =
        ColdTask operation

    /// <summary>Creates a cold task from a factory that does not observe cancellation.</summary>
    /// <param name="factory">The factory invoked on each execution.</param>
    let fromTaskFactory (factory: unit -> Task<'value>) : ColdTask<'value> =
        create (fun _ -> factory ())

    /// <summary>Wraps a task that has already been started.</summary>
    /// <remarks>
    /// The work is in flight before this is called, so it cannot be cancelled by the runtime and
    /// every execution observes the same single result. Prefer <c>create</c> or
    /// <c>fromTaskFactory</c>.
    /// </remarks>
    /// <param name="startedTask">A task that is already running.</param>
    let awaitStartedTask (startedTask: Task<'value>) : ColdTask<'value> =
        fromTaskFactory (fun () -> startedTask)

    /// <summary>Creates a cold task from a cancellable value-task factory.</summary>
    /// <param name="factory">The factory invoked on each execution.</param>
    let fromValueTaskFactory
        (factory: CancellationToken -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun cancellationToken -> factory cancellationToken |> _.AsTask())

    /// <summary>Creates a cold task from a value-task factory that does not observe cancellation.</summary>
    /// <param name="factory">The factory invoked on each execution.</param>
    let fromValueTaskFactoryWithoutCancellation
        (factory: unit -> ValueTask<'value>)
        : ColdTask<'value> =
        create (fun _ -> factory () |> _.AsTask())

    /// <summary>Wraps a value task that has already been started.</summary>
    /// <remarks>Carries the same caveats as <c>awaitStartedTask</c>.</remarks>
    /// <param name="startedValueTask">A value task that is already running.</param>
    let awaitStartedValueTask (startedValueTask: ValueTask<'value>) : ColdTask<'value> =
        let startedTask = startedValueTask.AsTask()
        awaitStartedTask startedTask

    /// <summary>Executes the cold task with the supplied cancellation token.</summary>
    /// <param name="cancellationToken">The token passed to the cold task factory.</param>
    /// <param name="coldTask">The cold task to execute.</param>
    let run (cancellationToken: CancellationToken) (coldTask: ColdTask<'value>) : Task<'value> =
        let (ColdTask operation) = coldTask
        operation cancellationToken
#endif

type internal Execution<'value, 'error> = Platform.Execution<'value, 'error>

/// <summary>Options for adaptive flow placement.</summary>
type AdaptiveRunOnOptions =
    { /// <summary>The minimum sampled synchronous hold time above which later runs use the thread pool.</summary>
      Threshold: TimeSpan
      /// <summary>The positive number of invocation attempts sampled before placement is decided. Failed and interrupted attempts count as samples.</summary>
      Samples: int }

#if !FABLE_COMPILER
type AdaptiveRunOnState internal (options: AdaptiveRunOnOptions) =
    let gate = obj ()
    let mutable decision = 0
    let mutable samples = 0
    let mutable minimum = TimeSpan.MaxValue

    member internal _.Decision = System.Threading.Volatile.Read(&decision)

    member internal _.Observe(duration: TimeSpan) =
        if System.Threading.Volatile.Read(&decision) = 0 then
            lock gate (fun () ->
                if decision = 0 then
                    samples <- samples + 1
                    minimum <- min minimum duration
                    if samples >= options.Samples then
                        decision <- if minimum > options.Threshold then 2 else 1)
#endif

/// <summary>Specifies where a flow starts running.</summary>
/// <remarks>JavaScript has one thread, so every placement runs inline there.</remarks>
[<RequireQualifiedAccess>]
type RunOn =
    /// <summary>Starts work on the calling thread.</summary>
    | Inline
    /// <summary>Starts work on the .NET thread pool.</summary>
    | ThreadPool
    /// <summary>Starts work on a dedicated .NET thread.</summary>
    | DedicatedThread
#if !FABLE_COMPILER
    /// <summary>Starts work on the supplied .NET task scheduler.</summary>
    | Scheduler of TaskScheduler
    /// <summary>An adaptive placement created by <c>RunOn.adaptive</c> or <c>RunOn.adaptiveWith</c>.</summary>
    | Adaptive of AdaptiveRunOnState
#else
    /// <summary>An adaptive placement created by <c>RunOn.adaptive</c> or <c>RunOn.adaptiveWith</c>.</summary>
    | Adaptive
#endif

/// <summary>Creates adaptive flow placements.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module RunOn =
    /// <summary>Creates a placement that samples three invocation attempts, then permanently selects inline or thread-pool execution from their minimum synchronous hold time.</summary>
    let adaptive () =
#if FABLE_COMPILER
        RunOn.Adaptive
#else
        RunOn.Adaptive(AdaptiveRunOnState { Threshold = TimeSpan.FromTicks 500L; Samples = 3 })
#endif

    /// <summary>Creates a placement that samples the requested number of invocation attempts before deciding once from their minimum synchronous hold time. Failed and interrupted attempts count as samples.</summary>
    let adaptiveWith options =
        if options.Threshold < TimeSpan.Zero then invalidArg (nameof options) "Threshold must not be negative."
        if options.Samples <= 0 then invalidArg (nameof options) "Samples must be positive."
#if FABLE_COMPILER
        RunOn.Adaptive
#else
        RunOn.Adaptive(AdaptiveRunOnState options)
#endif

/// Process-wide identities for fiber-local handles, including handles of different value types.
module internal RuntimeIds =
    let private nextValue = ref 0L
    let next () = Platform.nextId nextValue

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal FiberId =
    let private nextValue = ref 0L

    let next () =
        FiberId(Platform.nextId nextValue)

/// <summary>
/// Owns finalizers for resources acquired during provisioning or runtime execution.
/// </summary>
/// <remarks>
/// Scopes aggregate cleanup in reverse registration order, prevent double-disposal, and surface
/// cleanup failures as defects rather than typed business errors.
/// </remarks>
type Scope() =
    // Never use the public scope as a monitor: callers can lock it independently of the runtime.
    let gate = obj ()
    // Keys increase monotonically, so sorting them recovers registration order after removals. Allocated on the
    // first registration: most parallel branch scopes never hold anything.
    let mutable finalizers: System.Collections.Generic.Dictionary<int64, Platform.Finalizer> = null
    // A single registration needs no dictionary. Keep its key so Unregister can remove it.
    let mutable firstFinalizer: Platform.Finalizer = Unchecked.defaultof<_>
    let mutable firstKey = 0L
    let mutable nextKey = 0L
    let mutable closed = false
    let mutable detach: unit -> unit = ignore
    // A branch scope's parent, until the branch first registers something and attaches to it; null otherwise.
    let mutable pendingParent: Scope = Unchecked.defaultof<Scope>

    /// A child of <paramref name="parent" /> that registers with it only when something first registers with the
    /// child, so a parallel branch that acquires nothing costs its parent no registration. Once the parent has
    /// closed, registering with the child fails as registering with the parent would.
    static member internal Branch(parent: Scope) : Scope =
        let branch = new Scope()
        branch.PendingParent <- parent
        branch

    member internal _.PendingParent
        with set (value: Scope) = pendingParent <- value

    /// Registers a finalizer and returns a key that <c>Unregister</c> accepts. Work that settles before the
    /// scope closes unregisters itself, so a long-lived scope retains only what is still outstanding.
    member internal this.Register(finalizer: Platform.Finalizer) : int64 =
        match this.TryRegister finalizer with
        | ValueSome key -> key
        | ValueNone -> raise (ObjectDisposedException(nameof Scope))

    /// Same as <c>Register</c>, but returns <c>ValueNone</c> instead of throwing once the scope has closed. Internal
    /// callers that fall back on a closed scope use this: Fable cannot tell <c>ObjectDisposedException</c> apart from
    /// other exceptions, so catching it there would catch everything.
    member internal this.TryRegister(finalizer: Platform.Finalizer) : int64 voption =
        if isNull (box finalizer) then
            nullArg (nameof finalizer)

        lock gate (fun () ->
            // Attaching takes the parent's lock while holding this one. Locks are only ever nested child-to-parent
            // (closing runs finalizers outside the lock), so this cannot deadlock.
            if not closed && not (isNull (box pendingParent)) then
                let parent = pendingParent
                pendingParent <- Unchecked.defaultof<Scope>

                match parent.TryRegister(fun cancellationToken -> this.Close cancellationToken) with
                | ValueSome key -> detach <- fun () -> parent.Unregister key
                | ValueNone -> closed <- true

            if closed then
                ValueNone
            else
                let key = nextKey
                nextKey <- nextKey + 1L
                if isNull finalizers then
                    if isNull (box firstFinalizer) then
                        firstKey <- key
                        firstFinalizer <- finalizer
                    else
                        finalizers <- System.Collections.Generic.Dictionary<int64, Platform.Finalizer>()
                        finalizers[firstKey] <- firstFinalizer
                        firstFinalizer <- Unchecked.defaultof<_>
                        finalizers[key] <- finalizer
                else
                    finalizers[key] <- finalizer
                ValueSome key)

    /// Removes a finalizer without running it. Has no effect once the scope has closed.
    member internal _.Unregister(key: int64) : unit =
        lock gate (fun () ->
            if not closed then
                if isNull finalizers then
                    if key = firstKey then firstFinalizer <- Unchecked.defaultof<_>
                else
                    finalizers.Remove key |> ignore)

    member this.AddFinalizer(finalizer: Platform.Finalizer) =
        this.Register finalizer |> ignore

    /// The number of finalizers still registered; used by tests to prove that settled work is released.
    member internal _.RegisteredCount =
        lock gate (fun () -> if isNull finalizers then (if isNull (box firstFinalizer) then 0 else 1) else finalizers.Count)

    member this.AddDisposable(resource: IDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ ->
            resource.Dispose()
            Platform.completedDeed ())

    member this.AddAsyncDisposable(resource: IAsyncDisposable) =
        if isNull (box resource) then
            nullArg (nameof resource)

        this.AddFinalizer(fun _ -> Platform.disposeAsyncDeed resource)

    member this.AddChild() =
        match this.TryAddChild() with
        | ValueSome child -> child
        | ValueNone -> raise (ObjectDisposedException(nameof Scope))

    /// Same as <c>AddChild</c>, but returns <c>ValueNone</c> once the scope has closed.
    member internal this.TryAddChild() : Scope voption =
        let child = new Scope()

        match this.TryRegister(fun cancellationToken -> child.Close(cancellationToken)) with
        | ValueSome key ->
            child.Detach <- fun () -> this.Unregister key
            ValueSome child
        | ValueNone -> ValueNone

    member internal _.Detach
        with set (value: unit -> unit) = detach <- value

    /// Closes the scope when nothing is registered with it, so a parallel branch whose scope the caller keeps does
    /// not leave an empty child behind in a long-lived scope. A scope that still holds registrations stays open and
    /// closes with its parent.
    member internal _.CloseIfEmpty() : unit =
        let emptied =
            lock gate (fun () ->
                if not closed && (if isNull finalizers then isNull (box firstFinalizer) else finalizers.Count = 0) then
                    closed <- true
                    pendingParent <- Unchecked.defaultof<Scope>
                    true
                else
                    false)

        if emptied then
            detach ()
            detach <- ignore

    member _.Close(cancellationToken: CancellationToken) : Platform.Deed =
        let snapshot =
            lock gate (fun () ->
                if closed then
                    [||]
                else
                    closed <- true
                    pendingParent <- Unchecked.defaultof<Scope>

                    if isNull finalizers then
                        if isNull (box firstFinalizer) then [||]
                        else
                            let only = firstFinalizer
                            firstFinalizer <- Unchecked.defaultof<_>
                            [| only |]
                    else
                        let ordered =
                            if finalizers.Count = 1 then
                                let mutable only = Unchecked.defaultof<Platform.Finalizer>
                                for entry in finalizers.Values do only <- entry
                                [| only |]
                            else
                                finalizers.Keys |> Seq.sort |> Seq.map (fun key -> finalizers[key]) |> Seq.toArray

                        finalizers.Clear()
                        ordered)

        // A closed child no longer needs its parent to close it. Unregistering twice is harmless.
        detach ()
        detach <- ignore

        Platform.runFinalizers snapshot cancellationToken

#if !FABLE_COMPILER
    interface IAsyncDisposable with
        member this.DisposeAsync() =
            ValueTask(Platform.Cancellation.uninterruptible this.Close)

    interface IDisposable with
        member this.Dispose() =
            (Platform.Cancellation.uninterruptible this.Close).GetAwaiter().GetResult()  // axial-allow-AXG001: IDisposable.Dispose is synchronous by contract
#endif

type internal FiberContext =
    {
        Annotations: Map<string, string>
        Locals: Map<int64, struct (string option * obj)>
    }

[<RequireQualifiedAccess>]
module internal FiberContext =
    let empty = { Annotations = Map.empty; Locals = Map.empty }

/// Pure decorators for runtime log destinations. Observation failures never escape into the workflow.
[<RequireQualifiedAccess>]
module LogSink =
    let internal enabled (sink: ILogSink) level =
        // axial-note AXG010: a broken observation gate cannot fail the application.
        try sink.IsEnabled level with _ -> false
    let internal write (sink: ILogSink) entry =
        // axial-note AXG010: log destinations are write-only observations.
        try sink.Write entry with _ -> ()
    let none : ILogSink =
        { new ILogSink with
            member _.IsEnabled _ = false
            member _.Write _ = () }
    let fromFunction (destination: LogEntry -> unit) : ILogSink =
        { new ILogSink with
            member _.IsEnabled _ = true
            member _.Write entry = destination entry }
    let console (minimum: LogLevel) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = level >= minimum
            member _.Write entry =
                Platform.logError (entry.Level.ToString() + ": " + entry.Message)
                entry.Error |> Option.iter (fun error ->
                    Platform.logError (error.ToString())) }
    let tee (first: ILogSink) (second: ILogSink) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = enabled first level || enabled second level
            member _.Write entry =
                if enabled first entry.Level then write first entry
                if enabled second entry.Level then write second entry }
    let minLevel (minimum: LogLevel) (sink: ILogSink) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = level >= minimum && enabled sink level
            member _.Write entry = if entry.Level >= minimum && enabled sink entry.Level then write sink entry }
    let filter (predicate: LogEntry -> bool) (sink: ILogSink) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = enabled sink level
            member _.Write entry = if predicate entry then write sink entry }
    let map (mapper: LogEntry -> LogEntry) (sink: ILogSink) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = enabled sink level
            member _.Write entry =
                let mapped = mapper entry
                if enabled sink mapped.Level then write sink mapped }

type internal PlatformServices =
    { Clock: IClock; Random: IRandom; Guid: IGuid
      /// The executor's view of <c>Clock</c>, built once per installed clock so timed primitives do not allocate on every read.
      Time: Platform.ITimeSource }
    static member Create(clock: IClock, random: IRandom, guid: IGuid) =
        { Clock = clock; Random = random; Guid = guid; Time = Platform.timeOfClock clock }

module internal PlatformDefaults =
    let clock : IClock =
        { new IClock with
            // axial-allow-AXG001: live runtime clock implementation.
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = Platform.monotonicNow ()
            member _.Sleep(delay, cancellationToken) =
#if FABLE_COMPILER
                let pending = Fable.Core.JS.Constructors.Promise.Create<unit>(fun resolve reject ->
                    let settled = ref false
                    let timer: obj ref = ref null
                    let registration: IDisposable option ref = ref None
                    let finish error =
                        if not settled.Value then
                            settled.Value <- true
                            Platform.cancelTimer timer.Value
                            registration.Value |> Option.iter _.Dispose()
                            match error with
                            | None -> resolve (box ())
                            | Some (error: exn) -> reject (box error)
                    let deadline = Platform.monotonicNow () + delay
                    let rec wait () =
                        let remaining = deadline - Platform.monotonicNow ()
                        if remaining <= TimeSpan.Zero then finish None
                        else timer.Value <- Platform.scheduleTimer wait (int (min 2147483647.0 (Math.Ceiling remaining.TotalMilliseconds)))
                    if cancellationToken.IsCancellationRequested then finish (Some(OperationCanceledException()))
                    else
                        let registered = unbox<IDisposable> (box (cancellationToken.Register(fun () -> finish (Some(OperationCanceledException())))))
                        registration.Value <- Some registered
                        if settled.Value then registered.Dispose()
                        else wait ())
                unbox<Task> pending
#else
                task {
                    let! exit = (Platform.sleepExecution delay cancellationToken : Platform.Execution<unit, unit>).AsTask()
                    match exit with
                    | Exit.Success () -> ()
                    | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException(cancellationToken))
                    | Exit.Failure _ -> failwith "Live clock sleep failed."
                } :> Task
#endif
        }
    let random : IRandom =
        // axial-allow-AXG001: live runtime random implementation.
        let generator = System.Random()
        let gate = obj()
        { new IRandom with
            member _.Next() = lock gate generator.Next
            member _.NextMax maximum = lock gate (fun () -> generator.Next maximum)
            member _.NextInt minimum maximum = lock gate (fun () -> generator.Next(minimum, maximum))
            member _.NextDouble() = lock gate generator.NextDouble
            member _.NextBytes buffer = lock gate (fun () -> generator.NextBytes buffer) }
    let guid : IGuid =
        { new IGuid with
            // axial-allow-AXG001: live runtime GUID implementation.
            member _.NewGuid() = System.Guid.NewGuid() }
    let live = PlatformServices.Create(clock, random, guid)
    let logSink = LogSink.console LogLevel.Information

type internal RuntimeContext =
    {
        Platform: PlatformServices
        LogSink: ILogSink
        Scope: Scope
        /// The scope of the root run (Flow.run, App). Work started with Flow.disconnect registers here: its caller
        /// does not wait for it, but the root does, so nothing outlives the application unreported.
        RootScope: Scope
        Context: FiberContext
        AnnotationSink: string -> string -> unit
        TelemetryContext: Axial.Telemetry.TelemetryContext
        TelemetrySink: Axial.Telemetry.Attribute -> unit
        FiberId: FiberId
        Observer: FiberObserver
        /// Registries installed with Flow.withFiberRegistry; a failed fiber hands each its rendered cause.
        Registries: FiberRegistry list
        /// Opaque ambient tracer slot. `Axial` has no dependency on `System.Diagnostics.DiagnosticSource`, so this
        /// is untyped here; `Axial.Telemetry` is the only package that boxes/unboxes it (as `ActivitySource`).
        Tracer: obj option
        /// The token of the innermost parallel branch (zipPar, race, timeout), whose combinator cancels it to stop
        /// the branch; default outside any branch.
        BranchToken: CancellationToken
        /// The token outside every enclosing branch. A fiber forked under BranchToken follows this one instead, so
        /// it outlives the branch when the caller keeps the branch's value; see forkToken.
        OuterToken: CancellationToken
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeContext =
    let create (scope: Scope) : RuntimeContext =
        {
            Platform = PlatformDefaults.live
            LogSink = PlatformDefaults.logSink
            Scope = scope
            RootScope = scope
            Context = FiberContext.empty
            AnnotationSink = fun _ _ -> ()
            TelemetryContext = Axial.Telemetry.TelemetryContext Map.empty
            TelemetrySink = ignore
            FiberId = FiberId.next ()
            Observer = FiberObserver.none
            Registries = []
            Tracer = None
            BranchToken = Platform.Cancellation.never
            OuterToken = Platform.Cancellation.never
        }

    let detached : RuntimeContext =
        create (new Scope())

    let withScope (scope: Scope) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Scope = scope }

    /// The token a fiber forked under <paramref name="token" /> follows. A branch's own token is cancelled when its
    /// combinator is decided (a race cancels both branches once one wins), so a fiber forked directly in a branch
    /// follows the token outside the branch; the branch's scope interrupts it if the branch is not kept.
    let forkToken (runtime: RuntimeContext) (token: CancellationToken) : CancellationToken =
#if FABLE_COMPILER
        // Fable's tokens are objects: the branch's token is the same object.
        if obj.ReferenceEquals(token, runtime.BranchToken) then runtime.OuterToken else token
#else
        if token.Equals runtime.BranchToken then runtime.OuterToken else token
#endif

    /// The runtime of a branch that runs under <paramref name="branchToken" /> inside code running under
    /// <paramref name="token" />.
    let inBranch (scope: Scope) (token: CancellationToken) (branchToken: CancellationToken) (runtime: RuntimeContext) =
        { runtime with
            Scope = scope
            BranchToken = branchToken
            OuterToken = forkToken runtime token }

    let withAnnotation (name: string) (value: string) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Context = { runtime.Context with Annotations = runtime.Context.Annotations |> Map.add name value } }

    let withAnnotationSink (sink: string -> string -> unit) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with AnnotationSink = sink }

    /// Tees annotations to the existing sink before the new one, so nested telemetry regions and
    /// user-installed sinks all receive them. Each sink is guarded: a throwing sink cannot fail the
    /// workflow or starve the other sinks.
    let withComposedAnnotationSink (sink: string -> string -> unit) (runtime: RuntimeContext) : RuntimeContext =
        let previous = runtime.AnnotationSink

        { runtime with
            AnnotationSink =
                fun name value ->
                    (try previous name value with _ -> ())
                    (try sink name value with _ -> ()) }

    let withTelemetryContext (context: Axial.Telemetry.TelemetryContext) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with TelemetryContext = context }

    let withTelemetrySink (sink: Axial.Telemetry.Attribute -> unit) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with TelemetrySink = sink }

    let withComposedTelemetrySink (sink: Axial.Telemetry.Attribute -> unit) (runtime: RuntimeContext) : RuntimeContext =
        let previous = runtime.TelemetrySink

        { runtime with
            TelemetrySink =
                fun attribute ->
                    (try previous attribute with _ -> ())
                    (try sink attribute with _ -> ()) }

    let withFiberId (fiberId: FiberId) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with FiberId = fiberId }

    let withObserver (observer: FiberObserver) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Observer = observer }

    let withRegistry (registry: FiberRegistry) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Registries = registry :: runtime.Registries }

    let withTracer (tracer: obj) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Tracer = Some tracer }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeState =
    let private currentRuntime = Platform.newCell RuntimeContext.detached

    let current () : RuntimeContext =
        currentRuntime |> Platform.getCellOrDefault (fun () -> RuntimeContext.detached)

#if FABLE_COMPILER
    let withRuntime (runtime: RuntimeContext) (operation: unit -> Async<'value>) : Async<'value> =
        Platform.withCell currentRuntime runtime operation
#else
    let withRuntime (runtime: RuntimeContext) (operation: unit -> 'value) : 'value =
        Platform.withCell currentRuntime runtime operation

    /// Makes <paramref name="runtime" /> current without restoring the previous one. Only for callers that restore
    /// the whole <c>ExecutionContext</c> themselves, which costs one allocation instead of two.
    let enter (runtime: RuntimeContext) : unit = currentRuntime.Current.Value <- runtime
#endif

/// The only accessor for platform and log reads. Keep carrier changes local to this function.
module internal RuntimePlatform =
    let current () = RuntimeState.current ()

/// <summary>
/// Represents a cold workflow that reads an environment, returns a typed result, and is executed
/// explicitly through one of its execution members such as <c>ToTask</c>, <c>ToAsync</c>, or <c>RunSynchronously</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type internal ReleaseCauseException(cause: Cause<obj>) =
    inherit Exception("Resource release failed.")
    member _.Cause = cause

type Flow<'env, 'error, 'value> =
    internal
    | Flow of ('env -> CancellationToken -> Execution<'value, 'error>)
#if !FABLE_COMPILER
    // Only sequencing is trampolined. Primitive operations still use the platform's normal awaitable.
    | FlowDelay of (unit -> Flow<'env, 'error, 'value>)
    | FlowContinue of ('env -> CancellationToken -> FlowStep<'env, 'error, 'value>)
#endif

#if !FABLE_COMPILER
    member private this.Advance(environment: 'env, cancellationToken: CancellationToken) =
        // Consume tail continuations before executing their successors. A recursive bind must not leave
        // an awaiting parent task behind; only a source with real work remaining owns a nested execution.
        let mutable current = this
        let mutable searching = true
        let mutable terminal = Unchecked.defaultof<Execution<'value, 'error>>
        let mutable pending = Unchecked.defaultof<IFlowAwaiter<'env, 'error, 'value>>
        let mutable finished = false

        while searching do
            match current with
            | FlowDelay factory -> current <- factory ()
            | FlowContinue next ->
                match next environment cancellationToken with
                | FlowStep.Next successor -> current <- successor
                | FlowStep.Await awaiter ->
                    pending <- awaiter
                    searching <- false
                | FlowStep.Failed cause ->
                    terminal <- ValueTask<Exit<'value, 'error>>(Exit.Failure cause)
                    finished <- true
                    searching <- false
            | Flow operation ->
                terminal <- operation environment cancellationToken
                finished <- true
                searching <- false

        struct (finished, terminal, pending)

    // The user-code boundary of the trampoline: a Flow.delay factory, a bind continuation or a primitive that throws
    // while being advanced ends this flow with the cause Platform.causeOfThrown gives it.
    member private this.AdvanceObserved(environment: 'env, cancellationToken: CancellationToken) =
        try
            this.Advance(environment, cancellationToken)
        with error ->
            struct (
                true,
                ValueTask<Exit<'value, 'error>>(Exit.Failure(Platform.causeOfThrown cancellationToken error)),
                Unchecked.defaultof<IFlowAwaiter<'env, 'error, 'value>>
            )

    member private this.RunTrampoline(environment: 'env, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let struct (finished, terminal, pending) = this.AdvanceObserved(environment, cancellationToken)

        if finished then terminal
        else
            ValueTask<Exit<'value, 'error>>(
                task {
                    let mutable next = pending
                    let mutable result = Unchecked.defaultof<Exit<'value, 'error>>
                    let mutable running = true

                    while running do
                        do! next.Pending
                        let step =
                            try
                                next.Resume()
                            with error ->
                                FlowStep.Failed(Platform.causeOfThrown cancellationToken error)

                        let struct (finished, terminal, pending) =
                            match step with
                            | FlowStep.Next successor -> successor.AdvanceObserved(environment, cancellationToken)
                            | FlowStep.Failed cause ->
                                struct (true, ValueTask<Exit<'value, 'error>>(Exit.Failure cause), Unchecked.defaultof<_>)
                            | FlowStep.Await awaiter -> struct (false, Unchecked.defaultof<_>, awaiter)

                        if finished then
                            let! exit = terminal
                            result <- exit
                            running <- false
                        else
                            next <- pending

                    return result
                })
#endif

    member internal this.Invoke(environment: 'env, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        match this with
        | Flow operation -> Platform.guardStack (fun () -> operation environment cancellationToken)
#if !FABLE_COMPILER
        | _ -> Platform.guardStack (fun () -> this.RunTrampoline(environment, cancellationToken))
#endif

    member private this.ToExecution(environment: 'env, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let scope = new Scope()
        let runtime = RuntimeContext.create scope

        Platform.runScoped
            scope.Close
            cancellationToken
            (fun () ->
                RuntimeState.withRuntime runtime (fun () ->
                    // The root is already a fresh execution boundary. Keep the primitive fast path free of
                    // a stack-guard closure; nested sources still enter through Invoke.
                    match this with
                    | Flow operation -> operation environment cancellationToken
#if !FABLE_COMPILER
                    | _ -> this.RunTrampoline(environment, cancellationToken)
#endif
                ))
            (fun cleanupError executionError exit ->
                let rec causeOf (error: exn) : Cause<'error> =
                    match error with
                    | :? ReleaseCauseException as release -> Cause.map (fun _ -> Unchecked.defaultof<'error>) release.Cause
#if !FABLE_COMPILER
                    | :? AggregateException as aggregate when aggregate.InnerExceptions.Count > 0 ->
                        aggregate.InnerExceptions |> Seq.map causeOf |> Seq.reduce Cause.thenCause
#endif
                    // An OperationCanceledException is an interruption only when this run's token asked for it,
                    // as everywhere else (Platform.causeOfThrown); a foreign one is a defect.
                    | _ -> Platform.causeOfThrown cancellationToken error

                let primary =
                    match executionError, exit with
                    | Some error, _ -> Exit.Failure (causeOf error)
                    | None, Some result -> result
                    | None, None -> Exit.Failure (Cause.Die (InvalidOperationException "Flow execution produced no outcome."))

                match cleanupError, primary with
                | Some error, Exit.Failure cause ->
                    Exit.Failure (Cause.thenCause cause (causeOf error))
                | Some error, Exit.Success _ ->
                    Exit.Failure (causeOf error)
                | None, result ->
                    result)

    /// <summary>Builds a cold async that runs the workflow when it is started.</summary>
    /// <remarks>
    /// Nothing executes until the returned <c>Async</c> is run. Use <c>StartAsTask</c> or
    /// <c>StartAsValueTask</c> when the work should begin immediately.
    /// </remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token to use instead of <c>Async.CancellationToken</c>.</param>
    /// <returns>A cold async that completes with the workflow exit.</returns>
    /// <platforms>Fable compatible</platforms>
    member this.ToAsync(environment: 'env, ?cancellationToken: CancellationToken) : Async<Exit<'value, 'error>> =
        async {
            let! token =
                match cancellationToken with
                | Some token -> async.Return token
                | None -> Async.CancellationToken

            return! Platform.executionToAsync (this.ToExecution(environment, token))
        }

#if !FABLE_COMPILER
    /// <summary>Starts the workflow immediately and returns a value-task handle for its final exit.</summary>
    /// <remarks>The work is already in flight when this returns. Use <c>ToAsync</c> for a cold handle.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to a non-cancelable token.</param>
    /// <returns>A value task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.StartAsValueTask(environment: 'env, ?cancellationToken: CancellationToken) : ValueTask<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken Platform.Cancellation.never)

    /// <summary>Starts the workflow immediately and returns a task handle for its final exit.</summary>
    /// <remarks>The work is already in flight when this returns. Use <c>ToAsync</c> for a cold handle.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to a non-cancelable token.</param>
    /// <returns>A task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.StartAsTask(environment: 'env, ?cancellationToken: CancellationToken) : Task<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken Platform.Cancellation.never).AsTask()

    /// <summary>Starts the workflow and blocks until the final exit is available.</summary>
    /// <remarks>To bound how long the workflow may run, apply <c>Flow.timeout</c> to it: that interrupts the work
    /// and waits for its cleanup, where abandoning a blocked wait would leave it running.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to a non-cancelable token.</param>
    /// <returns>The final workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.RunSynchronously(environment: 'env, ?cancellationToken: CancellationToken) : Exit<'value, 'error> =
        let pending = this.StartAsValueTask(environment, ?cancellationToken = cancellationToken)
        if pending.IsCompletedSuccessfully then pending.Result
        else pending.AsTask().GetAwaiter().GetResult()  // axial-allow-AXG001: RunSynchronously is the documented blocking runner
#endif

#if !FABLE_COMPILER
// A pending bind keeps the typed source result in its frame. The runner awaits its Task directly, avoiding an
// intermediate Task<Flow<...>> and its state machine for every asynchronous bind.
and [<Struct; NoEquality; NoComparison>] internal FlowStep<'env, 'error, 'value> =
    | Next of successor: Flow<'env, 'error, 'value>
    | Await of pending: IFlowAwaiter<'env, 'error, 'value>
    | Failed of cause: Cause<'error>

and internal IFlowAwaiter<'env, 'error, 'value> =
    abstract Pending: Task
    abstract Resume: unit -> FlowStep<'env, 'error, 'value>
#endif

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal FlowInternal =
    let invoke
        (source: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Execution<'value, 'error> =
        source.Invoke(environment, cancellationToken)

    let create
        (operation: 'env -> CancellationToken -> Execution<'value, 'error>)
        : Flow<'env, 'error, 'value> =
        Flow operation

/// <summary>
/// Represents an error channel that cannot occur.
/// </summary>
type Never = private Never of unit

/// <summary>Uses the fact that a <see cref="T:Axial.Never" /> value cannot exist.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Never =
    /// <summary>Converts a <c>Never</c> to any type.</summary>
    /// <remarks>
    /// No <c>Never</c> value exists, so this function is never called with one. Pass it where a mapping from the
    /// error channel is required, to widen a flow, layer, cause, or exit that cannot fail into one with a real error
    /// type: <c>Exit.mapError Never.absurd</c>. <c>Flow.widenError</c> and <c>Layer.widenError</c> cover the common
    /// cases.
    /// </remarks>
    /// <example>
    /// <code>
    /// let exit : Exit&lt;int, string&gt; = Exit.Success 1 |> Exit.mapError Never.absurd
    /// </code>
    /// </example>
    let absurd (never: Never) : 'a =
        match never with
        | Never() -> invalidOp "A Never value cannot exist."

/// <summary>Describes acquisition of a value together with registration of its release in the current Flow scope.</summary>
/// <remarks>The release is a flow that cannot fail with a typed error. It runs without interruption, with the environment the value was acquired with; a defect it raises becomes a defect of the flow that closes the scope.</remarks>
/// <typeparam name="env">The environment required to acquire the value.</typeparam>
/// <typeparam name="error">The typed acquisition failure.</typeparam>
/// <typeparam name="value">The acquired value.</typeparam>
type Resource<'env, 'error, 'value> =
    internal
    | Resource of
        acquire: Flow<'env, 'error, 'value> *
        release: ('value -> Flow<'env, Never, unit>)

/// <summary>A flow that requires no environment and cannot fail with a typed error.</summary>
type Flow<'value> = Flow<unit, Never, 'value>

/// <summary>A flow that requires no environment and can fail with a typed error.</summary>
type Flow<'error, 'value> = Flow<unit, 'error, 'value>

/// <summary>A flow that reads an environment and cannot fail with a typed error.</summary>
type EnvFlow<'env, 'value> = Flow<'env, Never, 'value>

/// <summary>A flow that requires no environment and uses exceptions as recoverable typed errors.</summary>
type ExnFlow<'value> = Flow<unit, exn, 'value>

/// <summary>A flow that reads an environment and uses exceptions as recoverable typed errors.</summary>
type ExnEnvFlow<'env, 'value> = Flow<'env, exn, 'value>

/// <summary>Reads services from an <see cref="T:System.IServiceProvider" /> environment.</summary>
/// <remarks>
/// This is the host boundary. Use it in glue and adapters where dynamic container lookup is the
/// intended behaviour; application workflows should declare what they need instead. Missing
/// registrations are configuration defects and fail through <c>Cause.Die</c> rather than the typed
/// error channel. Build the environment with a layer when a missing registration should be a typed
/// startup error.
/// </remarks>
type ServiceProvider =
    /// <summary>Resolves a service from the <c>IServiceProvider</c> in the environment.</summary>
    /// <typeparam name="service">The service type being requested.</typeparam>
    /// <typeparam name="env">The environment type.</typeparam>
    /// <typeparam name="error">The workflow error type.</typeparam>
    /// <returns>A flow that succeeds with the requested service instance.</returns>
    /// <example>
    /// <code>
    /// let orders = ServiceProvider.get&lt;IOrderRepository, _, _&gt; ()
    /// </code>
    /// </example>
    static member get<'service, 'env, 'error when 'env :> IServiceProvider> () : Flow<'env, 'error, 'service> =
        Flow(fun environment _ ->
            match Platform.resolveService<'service> (environment :> IServiceProvider) with
            | Some service -> Platform.ofExit (Exit.Success service)
            | None -> Platform.serviceResolutionUnavailable<'service, 'error> ())
