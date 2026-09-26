namespace Axial

open System
open System.ComponentModel
open System.Threading
open System.Threading.Tasks

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
            | Cause.Fail error -> $"{padding}Fail({formatError error})"
            | Cause.Die error ->
                $"{padding}Die({Platform.dieDescription error})"
            | Cause.Interrupt -> $"{padding}Interrupt"
            | Cause.Then(left, right) ->
                $"{padding}Then\n{loop (indent + 2) left}\n{loop (indent + 2) right}"
            | Cause.Both(left, right) ->
                $"{padding}Both\n{loop (indent + 2) left}\n{loop (indent + 2) right}"
            | Cause.Traced(inner, trace) ->
                $"{padding}Traced({trace})\n{loop (indent + 2) inner}"

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
                raise (InvalidOperationException($"Workflow failed with a composite cause that cannot be represented as Result: {rendered}"))

/// <summary>Unique identifier for a running fiber.</summary>
[<Struct>]
type FiberId =
    | FiberId of int64

    /// <summary>The id as <c>#n</c>, rendered without reflection.</summary>
    override this.ToString() =
        let (FiberId value) = this
        $"#{value}"

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
        /// <summary>The UTC timestamp when the fiber settled, if it has.</summary>
        mutable SettledAt: DateTimeOffset option
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
        /// <summary>The UTC timestamp when the fiber settled, if it had settled when the snapshot was taken.</summary>
        SettledAt: DateTimeOffset option
        /// <summary>The fiber status when the snapshot was taken.</summary>
        Status: FiberStatus
    }

    /// <summary>Renders the dump as a single line, measuring lifetime against <paramref name="now" /> for live fibers.</summary>
    member dump.RenderAt(now: DateTimeOffset) : string =
        let name =
            match dump.Name with
            | Some name -> $" \"{name}\""
            | None -> ""

        let lifetime =
            let settled = dump.SettledAt |> Option.defaultValue now
            let elapsed = settled - dump.StartedAt
            $"%.1f{max elapsed.TotalSeconds 0.0}s"

        let annotations =
            if Map.isEmpty dump.Annotations then
                ""
            else
                dump.Annotations
                |> Seq.map (fun (KeyValue(key, value)) -> $"{key}={value}")
                |> String.concat " "
                |> sprintf " [%s]"

        $"#{dump.Id.Value}{name} {dump.Status} {lifetime} (started {dump.StartedAt:o}){annotations}"

    /// <summary>Renders the dump without reflection (safe under NativeAOT), measuring lifetime to when it settled.</summary>
    override dump.ToString() = dump.RenderAt(dump.SettledAt |> Option.defaultValue dump.StartedAt)

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
            SettledAt = metadata.SettledAt
            Status = metadata.Status
        }

    /// <summary>Renders one dump as a single line, measuring lifetime against <paramref name="now" /> for live fibers.</summary>
    let renderAt (now: DateTimeOffset) (dump: FiberDump) : string = dump.RenderAt now

    /// <summary>
    /// Renders a set of dumps as an indented parent/child tree. Fibers whose parent is absent from
    /// <paramref name="dumps" /> (including root fibers) become top-level nodes.
    /// </summary>
    let renderTreeAt (now: DateTimeOffset) (dumps: FiberDump list) : string =
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
            lines.Add(prefix + renderAt now dump)

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
    /// <remarks>Use this to stack integrations — for example telemetry spans plus logging — from one edge-level install.</remarks>
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
        try observer.OnStart metadata with _ -> ()

    let internal notifyEnd (observer: FiberObserver) (metadata: FiberMetadata) (defect: exn option) : unit =
        try observer.OnEnd metadata defect with _ -> ()

    let internal notifyUnobservedDefect
        (observer: FiberObserver)
        (metadata: FiberMetadata option)
        (defect: exn)
        : unit =
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
        (this.Fiber.SettledAt |> Option.defaultValue this.Fiber.StartedAt) - this.Fiber.StartedAt

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
    member this.DumpAt(now: DateTimeOffset) : string =
        let snapshot = this.Snapshot()
        let header = $"Fiber dump @ {now:o} — {snapshot.Length} live fiber(s)"

        match snapshot with
        | [] -> header
        | dumps -> header + "\n" + FiberDump.renderTreeAt now dumps

    /// <summary>
    /// Renders the current live fibers as a human-readable parent/child tree, timestamped with the current UTC
    /// time. This is a debugger/console convenience over a diagnostic render, not part of workflow execution or
    /// any deterministic behavior a test would assert on; use <c>DumpAt</c> when the timestamp itself matters
    /// to a caller (for example, a test asserting on the rendered header).
    /// </summary>
    member this.Dump() : string = this.DumpAt(DateTimeOffset.UtcNow) // axial-allow-effect: clock

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

    /// Records that the fiber's outcome was consumed (<c>Fiber.join</c>, <c>Fiber.await</c>, <c>Fiber.interrupt</c>)
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
    /// reports any unobserved defect — the same mechanism as <c>TaskScheduler.UnobservedTaskException</c>.
    static member Attach(fiberHandle: obj, tracker: FiberDefectTracker) =
        sentinels.Add(fiberHandle, tracker)

    override this.Finalize() =
        try this.TryReport() with _ -> ()
#endif

/// <summary>
/// Represents a handle to a workflow that has already been started.
/// </summary>
/// <remarks>
/// A fiber is the hot counterpart to a cold <c>Flow</c>, returned by <c>Flow.fork</c>. Wait for it with
/// <c>Fiber.join</c> (its value, re-raising its failure) or <c>Fiber.await</c> (its <c>Exit</c>), check it with
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

    /// The fiber's exit once it has settled.
    member internal _.Settled = settled.Value

#if !FABLE_COMPILER
/// <summary>
/// Represents delayed task work that can observe a runtime cancellation token when it is started.
/// </summary>
/// <remarks>
/// Bind a cold task directly in <c>flow { }</c>. When the task produces <c>Result&lt;'value,'error&gt;</c>,
/// the builder places <c>Error</c> in Flow's typed error channel for both <c>let!</c> and <c>return!</c>.
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
    let gate = obj()
    // Keys increase monotonically, so sorting them recovers registration order after removals.
    let finalizers = System.Collections.Generic.Dictionary<int64, Platform.Finalizer>()
    let mutable nextKey = 0L
    let mutable closed = false
    let mutable detach: unit -> unit = ignore

    /// Registers a finalizer and returns a key that <c>Unregister</c> accepts. Work that settles before the
    /// scope closes unregisters itself, so a long-lived scope retains only what is still outstanding.
    member internal _.Register(finalizer: Platform.Finalizer) : int64 =
        if isNull (box finalizer) then
            nullArg (nameof finalizer)

        lock gate (fun () ->
            if closed then
                raise (ObjectDisposedException(nameof Scope))
            else
                let key = nextKey
                nextKey <- nextKey + 1L
                finalizers[key] <- finalizer
                key)

    /// Removes a finalizer without running it. Has no effect once the scope has closed.
    member internal _.Unregister(key: int64) : unit =
        lock gate (fun () ->
            if not closed then
                finalizers.Remove key |> ignore)

    member this.AddFinalizer(finalizer: Platform.Finalizer) =
        this.Register finalizer |> ignore

    /// The number of finalizers still registered; used by tests to prove that settled work is released.
    member internal _.RegisteredCount = lock gate (fun () -> finalizers.Count)

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
        let child = new Scope()
        let key = this.Register(fun cancellationToken -> child.Close(cancellationToken))
        child.Detach <- fun () -> this.Unregister key
        child

    member internal _.Detach
        with set (value: unit -> unit) = detach <- value

    member _.Close(cancellationToken: CancellationToken) : Platform.Deed =
        let snapshot =
            lock gate (fun () ->
                if closed then
                    [||]
                else
                    closed <- true
                    let ordered = finalizers.Keys |> Seq.sort |> Seq.map (fun key -> finalizers[key]) |> Seq.toArray
                    finalizers.Clear()
                    ordered)

        // A closed child no longer needs its parent to close it. Unregistering twice is harmless.
        detach ()
        detach <- ignore

        Platform.runFinalizers snapshot cancellationToken

#if !FABLE_COMPILER
    interface IAsyncDisposable with
        member this.DisposeAsync() =
            ValueTask(this.Close(CancellationToken.None))

    interface IDisposable with
        member this.Dispose() =
            this.Close(CancellationToken.None).GetAwaiter().GetResult()
#endif

type internal RuntimeContext =
    {
        Scope: Scope
        Annotations: Map<string, string>
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
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeContext =
    let create (scope: Scope) : RuntimeContext =
        {
            Scope = scope
            Annotations = Map.empty
            AnnotationSink = fun _ _ -> ()
            TelemetryContext = Axial.Telemetry.TelemetryContext Map.empty
            TelemetrySink = ignore
            FiberId = FiberId.next ()
            Observer = FiberObserver.none
            Registries = []
            Tracer = None
        }

    let detached : RuntimeContext =
        create (new Scope())

    let withScope (scope: Scope) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Scope = scope }

    let withAnnotation (name: string) (value: string) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Annotations = runtime.Annotations |> Map.add name value }

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
#endif

/// <summary>
/// Represents a cold workflow that reads an environment, returns a typed result, and is executed
/// explicitly through one of its execution members such as <c>ToTask</c>, <c>ToAsync</c>, or <c>RunSynchronously</c>.
/// </summary>
/// <typeparam name="env">The type of the environment dependency.</typeparam>
/// <typeparam name="error">The type of the failure value.</typeparam>
/// <typeparam name="value">The type of the success value.</typeparam>
type Flow<'env, 'error, 'value> =
    internal
    | Flow of ('env -> CancellationToken -> Execution<'value, 'error>)

    member private this.ToExecution(environment: 'env, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let (Flow operation) = this
        let scope = new Scope()
        let runtime = RuntimeContext.create scope

        Platform.runScoped
            scope.Close
            cancellationToken
            (fun () -> RuntimeState.withRuntime runtime (fun () -> operation environment cancellationToken))
            (fun cleanupError executionError exit ->
                let causeOf (error: exn) : Cause<'error> =
                    if error :? OperationCanceledException then Cause.Interrupt else Cause.Die error

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
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>A value task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.StartAsValueTask(environment: 'env, ?cancellationToken: CancellationToken) : ValueTask<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken CancellationToken.None)

    /// <summary>Starts the workflow immediately and returns a task handle for its final exit.</summary>
    /// <remarks>The work is already in flight when this returns. Use <c>ToAsync</c> for a cold handle.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>A task that completes with the workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.StartAsTask(environment: 'env, ?cancellationToken: CancellationToken) : Task<Exit<'value, 'error>> =
        this.ToExecution(environment, defaultArg cancellationToken CancellationToken.None).AsTask()

    /// <summary>Starts the workflow and blocks until the final exit is available.</summary>
    /// <remarks>To bound how long the workflow may run, apply <c>Flow.timeout</c> to it: that interrupts the work
    /// and waits for its cleanup, where abandoning a blocked wait would leave it running.</remarks>
    /// <param name="environment">The environment used by the workflow.</param>
    /// <param name="cancellationToken">The optional cancellation token. Defaults to <see cref="F:System.Threading.CancellationToken.None" />.</param>
    /// <returns>The final workflow exit.</returns>
    /// <platforms>.NET only</platforms>
    member this.RunSynchronously(environment: 'env, ?cancellationToken: CancellationToken) : Exit<'value, 'error> =
        this.StartAsTask(environment, cancellationToken = defaultArg cancellationToken CancellationToken.None)
            .GetAwaiter()
            .GetResult()
#endif

[<EditorBrowsable(EditorBrowsableState.Never)>]
module internal FlowInternal =
    let invoke
        (Flow operation: Flow<'env, 'error, 'value>)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : Execution<'value, 'error> =
        Platform.guardStack (fun () -> operation environment cancellationToken)

    let create
        (operation: 'env -> CancellationToken -> Execution<'value, 'error>)
        : Flow<'env, 'error, 'value> =
        Flow operation

/// <summary>
/// Log levels used by runtime logging helpers and environment-provided logging functions.
/// </summary>
[<RequireQualifiedAccess>]
type LogLevel =
    | Trace
    | Debug
    | Information
    | Warning
    | Error
    | Critical

    /// <summary>The level name, rendered without reflection so it stays safe under NativeAOT.</summary>
    override this.ToString() =
        match this with
        | Trace -> "Trace"
        | Debug -> "Debug"
        | Information -> "Information"
        | Warning -> "Warning"
        | Error -> "Error"
        | Critical -> "Critical"

/// <summary>Describes acquisition of a value together with registration of its release in the current Flow scope.</summary>
/// <typeparam name="env">The environment required to acquire the value.</typeparam>
/// <typeparam name="error">The typed acquisition failure.</typeparam>
/// <typeparam name="value">The acquired value.</typeparam>
type Resource<'env, 'error, 'value> =
    internal
    | Resource of
        acquire: Flow<'env, 'error, 'value> *
        register: ('value -> Scope -> unit)

/// <summary>
/// Represents an error channel that cannot occur.
/// </summary>
type Never = private Never of unit

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
/// error channel — build the environment with a layer when a missing registration should be a typed
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
